using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StandaloneBannedApiAnalyzers.Extensions;

namespace StandaloneBannedApiAnalyzers
{
    public abstract class SymbolIsBannedAnalyzerBase<TSyntaxKind> : DiagnosticAnalyzer where TSyntaxKind : struct
    {
        protected abstract Dictionary<(string ContainerName, string SymbolName), ImmutableArray<BanFileEntry>> ReadBannedApis(CompilationStartAnalysisContext compilationContext);

        protected abstract DiagnosticDescriptor SymbolIsBannedRule { get; }

        protected abstract TSyntaxKind XmlCrefSyntaxKind { get; }

        protected abstract SyntaxNode GetReferenceSyntaxNodeFromXmlCref(SyntaxNode syntaxNode);

        protected abstract ImmutableArray<TSyntaxKind> BaseTypeSyntaxKinds { get; }

        protected abstract IEnumerable<SyntaxNode> GetTypeSyntaxNodesFromBaseType(SyntaxNode syntaxNode);

        protected abstract SymbolDisplayFormat SymbolDisplayFormat { get; }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Analyzer needs to get callbacks for generated code, and might report diagnostics in generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var bannedApis = ReadBannedApis(compilationContext);
            if (bannedApis == null || bannedApis.Count == 0)
                return;

            compilationContext.RegisterSemanticModelAction(context =>
            {
                VisitTree(context);
            });
            return;

            void VisitTree(SemanticModelAnalysisContext context)
            {
                var stack = new Stack<SyntaxNode>();
                stack.Push(context.SemanticModel.SyntaxTree.GetRoot());
                while(stack.Count > 0) {
                    var node = stack.Pop();
                    var type = node switch 
                    {
                        IdentifierNameSyntax => context.SemanticModel.GetTypeInfo(node).Type,
                        ObjectCreationExpressionSyntax => context.SemanticModel.GetTypeInfo(node).Type,
                        MemberAccessExpressionSyntax m => context.SemanticModel.GetTypeInfo(m.Parent).Type, 
                        _ => null
                    };
                    
                    if (type != null)
                    {
                        VerifyType(context.ReportDiagnostic, type, node);
                    }

                    foreach (var child in node.ChildNodes())
                    {
                        stack.Push(child);
                    }
                }
            }

            bool IsBannedSymbol(ISymbol symbol, out BanFileEntry entry)
            {
                if (symbol is { ContainingSymbol.Name: string parentName } &&
                    bannedApis.TryGetValue((parentName, symbol.Name), out var entries))
                {
                    foreach (var bannedFileEntry in entries)
                    {
                        foreach (var bannedSymbol in bannedFileEntry.Symbols)
                        {
                            if (SymbolEqualityComparer.Default.Equals(symbol, bannedSymbol))
                            {
                                entry = bannedFileEntry;
                                return true;
                            }
                        }
                    }
                }

                entry = null;
                return false;
            }

            bool VerifyType(Action<Diagnostic> reportDiagnostic, ITypeSymbol type, SyntaxNode syntaxNode)
            {
                do
                {
                    if (!VerifyTypeArguments(reportDiagnostic, type, syntaxNode, out type))
                    {
                        return false;
                    }

                    if (type == null)
                    {
                        // Type will be null for arrays and pointers.
                        return true;
                    }

                    if (IsBannedSymbol(type, out var entry))
                    {
                        reportDiagnostic(
                            syntaxNode.CreateDiagnostic(
                                SymbolIsBannedRule,
                                type.ToDisplayString(SymbolDisplayFormat),
                                string.IsNullOrWhiteSpace(entry.Message) ? "" : ": " + entry.Message));
                        return false;
                    }

                    foreach (var currentNamespace in GetContainingNamespaces(type))
                    {
                        if (IsBannedSymbol(currentNamespace, out entry))
                        {
                            reportDiagnostic(
                                syntaxNode.CreateDiagnostic(
                                    SymbolIsBannedRule,
                                    currentNamespace.ToDisplayString(),
                                    string.IsNullOrWhiteSpace(entry.Message) ? "" : ": " + entry.Message));
                            return false;
                        }
                    }

                    type = type.ContainingType;
                }
                while (!(type is null));

                return true;

                static IEnumerable<INamespaceSymbol> GetContainingNamespaces(ISymbol symbol)
                {
                    INamespaceSymbol currentNamespace = symbol.ContainingNamespace;

                    while (currentNamespace is { IsGlobalNamespace: false })
                    {
                        foreach (var constituent in currentNamespace.ConstituentNamespaces)
                            yield return constituent;

                        currentNamespace = currentNamespace.ContainingNamespace;
                    }
                }
            }

            bool VerifyTypeArguments(Action<Diagnostic> reportDiagnostic, ITypeSymbol type, SyntaxNode syntaxNode, out ITypeSymbol originalDefinition)
            {
                switch (type)
                {
                    case INamedTypeSymbol namedTypeSymbol:
                        originalDefinition = namedTypeSymbol.ConstructedFrom;
                        foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                        {
                            if (typeArgument.TypeKind != TypeKind.TypeParameter &&
                                typeArgument.TypeKind != TypeKind.Error &&
                                !VerifyType(reportDiagnostic, typeArgument, syntaxNode))
                            {
                                return false;
                            }
                        }

                        break;

                    case IArrayTypeSymbol arrayTypeSymbol:
                        originalDefinition = null;
                        return VerifyType(reportDiagnostic, arrayTypeSymbol.ElementType, syntaxNode);

                    case IPointerTypeSymbol pointerTypeSymbol:
                        originalDefinition = null;
                        return VerifyType(reportDiagnostic, pointerTypeSymbol.PointedAtType, syntaxNode);

                    default:
                        originalDefinition = type?.OriginalDefinition;
                        break;

                }

                return true;
            }
        }

        protected sealed class BanFileEntry
        {
            public TextSpan Span { get; }
            public SourceText SourceText { get; }
            public string Path { get; }
            public string DeclarationId { get; }
            public string Message { get; }

            private readonly Lazy<ImmutableArray<ISymbol>> _lazySymbols;
            public ImmutableArray<ISymbol> Symbols => _lazySymbols.Value;

            public BanFileEntry(Compilation compilation, string text, TextSpan span, SourceText sourceText, string path)
            {
                // Split the text on semicolon into declaration ID and message
                var index = text.IndexOf(';');

                if (index == -1)
                {
                    DeclarationId = text.Trim();
                    Message = "";
                }
                else if (index == text.Length - 1)
                {
                    DeclarationId = text.AsSpan().Slice(0, text.Length-1).Trim().ToString();
                    Message = "";
                }
                else
                {
                    var textSpan = text.AsSpan();
                    DeclarationId = textSpan.Slice(0, index).Trim().ToString();
                    Message = textSpan.Slice(index+1).Trim().ToString();
                }

                Span = span;
                SourceText = sourceText;
                Path = path;

                _lazySymbols = new Lazy<ImmutableArray<ISymbol>>(
                    () => DocumentationCommentId.GetSymbolsForDeclarationId(DeclarationId, compilation)
                        .SelectMany(ExpandConstituentNamespaces).ToImmutableArray());

                static IEnumerable<ISymbol> ExpandConstituentNamespaces(ISymbol symbol)
                {
                    if (symbol is not INamespaceSymbol namespaceSymbol)
                    {
                        yield return symbol;
                        yield break;
                    }

                    foreach (var constituent in namespaceSymbol.ConstituentNamespaces)
                        yield return constituent;
                }
            }

            public Location Location => Location.Create(Path, Span, SourceText.Lines.GetLinePositionSpan(Span));
        }
    }
}