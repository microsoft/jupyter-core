using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using Markdig;

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

    internal static class MarkdownExtensions
    {
        public static string ToMarkdownHtml(this string markdown, string heading) =>
            !string.IsNullOrEmpty(markdown)
            ? heading + Markdown.ToHtml(markdown)
            : string.Empty;

        public static string ToMarkdownHtml(this IEnumerable<string> markdown, string heading) =>
            markdown != null && markdown.Any()
            ? string.Join(string.Empty, markdown.Select(m => m.ToMarkdownHtml(heading)))
            : string.Empty;
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
                    $"<p>{symbol.Documentation.Summary ?? ""}</p>" +
                    symbol.Documentation.Description.ToMarkdownHtml("<h5>Description</h5>") +
                    symbol.Documentation.Remarks.ToMarkdownHtml("<h5>Remarks</h5>") +
                    symbol.Documentation.Examples.ToMarkdownHtml("<h5>Example</h5>")
                ).ToEncodedData();

            }
            else return null;
        }
    }

}
