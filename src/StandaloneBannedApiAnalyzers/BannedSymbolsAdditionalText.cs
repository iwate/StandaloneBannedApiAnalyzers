using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace StandaloneBannedApiAnalyzers
{
    public class BannedSymbolsAdditionalText : AdditionalText
    {
        private readonly string _bannedSymbols;
        public BannedSymbolsAdditionalText(string bannedsymbols)
        {
            _bannedSymbols = bannedsymbols;
        }
        public override SourceText GetText(CancellationToken cancellationToken = new CancellationToken())
        {
            return SourceText.From(_bannedSymbols);
        }

        public override string Path { get; } = "BannedSymbols.txt";
    }
}