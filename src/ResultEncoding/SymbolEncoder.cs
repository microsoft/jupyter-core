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
                return $"{symbol.Name}:\n{symbol.Documentation.Summary ?? ""}"
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

        public string MimeType => MimeTypes.PlainText;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is Symbol symbol)
            {

                return
                    $"<h3><i class=\"fas {Icons[symbol.Kind]}\"></i>{symbol.Name}</h3>" +

            }
            else return null;
        }
    }

}
