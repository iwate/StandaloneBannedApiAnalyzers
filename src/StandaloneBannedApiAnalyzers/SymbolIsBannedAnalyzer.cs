using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StandaloneBannedApiAnalyzers
{
    internal static class SymbolIsBannedAnalyzer
    {
        public static readonly DiagnosticDescriptor SymbolIsBannedRule = new DiagnosticDescriptor(
            id: "RS0030",
            title: "Do not use banned APIs",
            messageFormat: "The symbol '{0}' is banned in this project{1}",
            category: "ApiDesign",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The symbol has been marked as banned in this project, and an alternate should be used instead.",
            helpLinkUri: "https://github.com/iwate/StandaloneBannedApiAnalyzers/blob/main/StandaloneBannedApiAnalyzers.Help.md");

        public static readonly DiagnosticDescriptor DuplicateBannedSymbolRule = new DiagnosticDescriptor(
            id: "RS0031",
            title: "he list of banned symbols contains a duplicate",
            messageFormat: "The symbol '{0}' is listed multiple times in the list of banned APIs",
            category: "ApiDesign",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "The list of banned symbols contains a duplicate.",
            helpLinkUri: "https://github.com/iwate/StandaloneBannedApiAnalyzers/blob/main/StandaloneBannedApiAnalyzers.Help.md");
    }


    public abstract class SymbolIsBannedAnalyzer<TSyntaxKind> : SymbolIsBannedAnalyzerBase<TSyntaxKind>
        where TSyntaxKind : struct
    {
        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(SymbolIsBannedAnalyzer.SymbolIsBannedRule, SymbolIsBannedAnalyzer.DuplicateBannedSymbolRule);

        protected sealed override DiagnosticDescriptor SymbolIsBannedRule => SymbolIsBannedAnalyzer.SymbolIsBannedRule;

#pragma warning disable RS1013 // 'compilationContext' does not register any analyzer actions, except for a 'CompilationEndAction'. Consider replacing this start/end action pair with a 'RegisterCompilationAction' or moving actions registered in 'Initialize' that depend on this start action to 'compilationContext'.
        protected sealed override Dictionary<(string ContainerName, string SymbolName), ImmutableArray<BanFileEntry>> ReadBannedApis(
            CompilationStartAnalysisContext compilationContext)
        {
            var compilation = compilationContext.Compilation;

            var query =
                from additionalFile in compilationContext.Options.AdditionalFiles
                let fileName = Path.GetFileName(additionalFile.Path)
                where fileName != null && fileName.StartsWith("BannedSymbols.", StringComparison.Ordinal) && fileName.EndsWith(".txt", StringComparison.Ordinal)
                orderby additionalFile.Path // Additional files are sorted by DocumentId (which is a GUID), make the file order deterministic
                let sourceText = additionalFile.GetText(compilationContext.CancellationToken)
                where sourceText != null
                from line in sourceText.Lines
                let text = line.ToString()
                let commentIndex = text.IndexOf("//", StringComparison.Ordinal)
                let textWithoutComment = commentIndex == -1 ? text : text.AsSpan().Slice(0, commentIndex).ToString()
                where !string.IsNullOrWhiteSpace(textWithoutComment)
                let trimmedTextWithoutComment = textWithoutComment.TrimEnd()
                let span = commentIndex == -1 ? line.Span : new TextSpan(line.Span.Start, trimmedTextWithoutComment.Length)
                let entry = new BanFileEntry(compilation, trimmedTextWithoutComment, span, sourceText, additionalFile.Path)
                where !string.IsNullOrWhiteSpace(entry.DeclarationId)
                select entry;

            var entries = query.ToList();

            if (entries.Count == 0)
                return null;

            var errors = new List<Diagnostic>();

            // Report any duplicates.
            var groups = entries.GroupBy(e => TrimForErrorReporting(e.DeclarationId));
            foreach (var group in groups)
            {
                if (group.Count() >= 2)
                {
                    var groupList = group.ToList();
                    var firstEntry = groupList[0];
                    for (int i = 1; i < groupList.Count; i++)
                    {
                        var nextEntry = groupList[i];
                        errors.Add(Diagnostic.Create(
                            SymbolIsBannedAnalyzer.DuplicateBannedSymbolRule,
                            nextEntry.Location, new[] { firstEntry.Location },
                            firstEntry.Symbols.FirstOrDefault()?.ToDisplayString() ?? ""));
                    }
                }
            }

            if (errors.Count != 0)
            {
                compilationContext.RegisterCompilationEndAction(
                    endContext =>
                    {
                        foreach (var error in errors)
                            endContext.ReportDiagnostic(error);
                    });
            }

            var result = new Dictionary<(string ContainerName, string SymbolName), List<BanFileEntry>>();

            foreach (var entry in entries)
            {
                var parsed = DocumentationCommentIdParser.ParseDeclaredSymbolId(entry.DeclarationId);
                if (parsed is null)
                    continue;

                if (!result.TryGetValue(parsed.Value, out var existing))
                {
                    existing = new();
                    result.Add(parsed.Value, existing);
                }

                existing.Add(entry);
            }

            return result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray());

            static string TrimForErrorReporting(string declarationId)
            {
                if (declarationId.Length < 2) {
                    return declarationId;
                }
                else {
                    var span = declarationId.AsSpan();
                    if (span[1] == ':') {
                        return span.Slice(2).ToString();
                    }
                    else {
                        return span.Slice(1).ToString();
                    }
                }
            }
        }
    }
}