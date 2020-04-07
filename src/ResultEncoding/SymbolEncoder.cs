using System.Collections.Immutable;
using System.Collections.Generic;

namespace Microsoft.Jupyter.Core
{

    public class MagicSymbolToTextResultEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.PlainText;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is MagicSymbol symbol)
            {
                return $"{symbol.Name}:\n{symbol.Documentation.Summary ?? ""}"
                    .ToEncodedData();
            }
            else return null;
        }
    }

    public class MagicSymbolToHtmlResultEncoder : IResultEncoder
    {
        internal readonly ImmutableDictionary<SymbolKind, string>
            Icons = new Dictionary<SymbolKind, string>
            {
                [SymbolKind.Magic] = "fa-magic",
                [SymbolKind.Callable] = "fa-terminal",
                [SymbolKind.LocalDeclaration] = "fa-stream",
                [SymbolKind.Other] = "fa-code"
            }.ToImmutableDictionary();

        public string MimeType => MimeTypes.Html;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is MagicSymbol symbol)
            {

                return (
                    $"<h4><i class=\"fa fas {Icons[symbol.Kind]}\"></i> {symbol.Name}</h4>" +
                    $"<p>{symbol.Documentation.Summary ?? ""}</p>"
                ).ToEncodedData();

            }
            else return null;
        }
    }

}
