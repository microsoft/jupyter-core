using System.Collections.Immutable;
using System.Collections.Generic;

namespace Microsoft.Jupyter.Core
{

    public class SymbolToTextResultEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.PlainText;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is Symbol symbol)
            {
                return $"{symbol.Name}:\n{symbol.Documentation.Value.Summary ?? ""}"
                    .ToEncodedData();
            }
            else return null;
        }
    }

    public class SymbolToHtmlResultEncoder : IResultEncoder
    {
        private readonly ImmutableDictionary<SymbolKind, string>
            Icons = new Dictionary<SymbolKind, string>
            {
                [SymbolKind.Magic] = "fa-magic",
                [SymbolKind.Other] = "fa-terminal"
            }.ToImmutableDictionary();

        public string MimeType => MimeTypes.Html;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is Symbol symbol)
            {

                return (
                    $"<h4><i class=\"fa fas {Icons[symbol.Kind]}\"></i> {symbol.Name}</h4>" +
                    $"<p>{symbol.Documentation.Value.Summary ?? ""}</p>"
                ).ToEncodedData();

            }
            else return null;
        }
    }

}
