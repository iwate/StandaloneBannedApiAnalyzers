using System;

namespace StandaloneBannedApiAnalyzers
{
    internal static class DocumentationCommentIdParser
    {
        private static readonly char[] s_nameDelimiters = { ':', '.', '(', ')', '{', '}', '[', ']', ',', '\'', '@', '*', '`', '~' };

        public static (string ParentName, string SymbolName)? ParseDeclaredSymbolId(string id)
        {
            if (id == null)
                return null;

            if (id.Length < 2)
                return null;

            int index = 0;
            return ParseDeclaredId(id, ref index);
        }

        private static (string ParentName, string SymbolName)? ParseDeclaredId(string id, ref int index)
        {
            var kindChar = PeekNextChar(id, index);

            switch (kindChar)
            {
                case 'E': // Events
                case 'F': // Fields
                case 'M': // Methods
                case 'P': // Properties
                case 'T': // Types
                case 'N': // Namespaces
                    break;
                default:
                    // Documentation comment id must start with E, F, M, N, P or T.
                    return null;
            }

            index++;
            if (PeekNextChar(id, index) == ':')
                index++;

            string parentName = "";

            // process dotted names
            while (true)
            {
                var symbolName = ParseName(id, ref index);

                // has type parameters?
                if (PeekNextChar(id, index) == '`')
                {
                    index++;

                    // method type parameters?
                    if (PeekNextChar(id, index) == '`')
                        index++;

                    ReadNextInteger(id, ref index);
                }

                if (PeekNextChar(id, index) == '.')
                {
                    index++;
                    parentName = symbolName;
                    continue;
                }
                else
                {
                    return (parentName, symbolName);
                }
            }
        }

        private static char PeekNextChar(string id, int index)
            => index >= id.Length ? '\0' : id[index];

        private static string ParseName(string id, ref int index)
        {
            string name;

            var span = id.AsSpan();
            int delimiterOffset = id.IndexOfAny(s_nameDelimiters, index);
            if (delimiterOffset >= 0)
            {
                name = span.Slice(index, delimiterOffset-index).ToString();
                index = delimiterOffset;
            }
            else
            {
                name = span.Slice(index).ToString();
                index = id.Length;
            }

            return name.Replace('#', '.');
        }

        private static void ReadNextInteger(string id, ref int index)
        {
            while (index < id.Length && char.IsDigit(id[index]))
                index++;
        }
    }
}