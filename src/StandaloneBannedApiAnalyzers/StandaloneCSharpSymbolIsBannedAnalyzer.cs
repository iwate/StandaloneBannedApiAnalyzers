
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace StandaloneBannedApiAnalyzers
{
    #pragma warning disable RS1036
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class StandaloneCSharpSymbolIsBannedAnalyzer : SymbolIsBannedAnalyzer<SyntaxKind>
    {
        protected override SyntaxKind XmlCrefSyntaxKind => SyntaxKind.XmlCrefAttribute;

        protected override ImmutableArray<SyntaxKind> BaseTypeSyntaxKinds => ImmutableArray.Create(SyntaxKind.BaseList);

        protected override SymbolDisplayFormat SymbolDisplayFormat => SymbolDisplayFormat.CSharpShortErrorMessageFormat;

        protected override SyntaxNode GetReferenceSyntaxNodeFromXmlCref(SyntaxNode syntaxNode) => ((XmlCrefAttributeSyntax)syntaxNode).Cref;

        protected override IEnumerable<SyntaxNode> GetTypeSyntaxNodesFromBaseType(SyntaxNode syntaxNode) => ((BaseListSyntax)syntaxNode).Types.Select(t => (SyntaxNode)t.Type);
    }
}