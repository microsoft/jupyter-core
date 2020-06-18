using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    /// Specifies the text alignment to use for a table cell.
    /// </summary>
    public enum TableCellAlignment
    {
        /// <summary>
        /// Align text to the left of the cell.
        /// </summary>
        Left,

        /// <summary>
        /// Align text to the right of the cell.
        /// </summary>
        Right,

        /// <summary>
        /// No alignment specified.
        /// </summary>
        None
    }

    public static class TableCellAlignmentExtensions
    {
        /// <summary>
        /// Constructs an HTML style attribute implementing the specified <see cref="TableCellAlignment"/>.
        /// </summary>
        /// <param name="alignment">The desired cell alignment.</param>
        /// <returns>
        /// A string containing the full HTML style attribute name and value, or an empty string
        /// if the alignment is specified as <see cref="TableCellAlignment.None"/>.
        /// </returns>
        /// <remarks>
        /// This method uses <c>start</c> and <c>end</c> values for the <c>text-align</c> CSS attribute,
        /// which respect the reading direction (LTR or RTL) of the surrounding HTML. For example, if the 
        /// specified alignment is <see cref="TableCellAlignment.Left"/>, the text will be aligned to the left
        /// in LTR and to the right in RTL.
        /// </remarks>
        public static string ToStyleAttribute(this TableCellAlignment alignment) => alignment switch
        {
            TableCellAlignment.Left => "style=\"text-align: start;\"",
            TableCellAlignment.Right => "style=\"text-align: end;\"",
            _ => string.Empty
        };

        /// <summary>
        /// Delegates to either <see cref="string.PadLeft(int)"/> or <see cref="string.PadRight(int)"/>, depending
        /// on the specified <see cref="TableCellAlignment"/> value.
        /// </summary>
        /// <param name="cell">A string representing the table cell contents.</param>
        /// <param name="totalWidth">The total width of the cell, in characters.</param>
        /// <param name="alignment">Specifies the desired text alignment in the cell</param>
        /// <returns>
        /// The padded string returned from <see cref="string.PadLeft(int)"/> or <see cref="string.PadRight(int)"/>.
        /// </returns>
        /// <remarks>
        /// If the specified alignment is <see cref="TableCellAlignment.None"/>, the text will be aligned to the left.
        /// </remarks>
        public static string PadCell(this string cell, int totalWidth, TableCellAlignment alignment) => alignment switch
        {
            TableCellAlignment.Left => cell.PadRight(totalWidth),
            TableCellAlignment.Right => cell.PadLeft(totalWidth),
            _ => cell.PadRight(totalWidth)
        };
    }
    
    public interface ITable
    {
        string[] Headers { get; }
        string[][] Cells { get; }
    }

    public class Table<TRow> : ITable
    {
        [JsonIgnore]
        public List<(string, Func<TRow, string>)> Columns;

        [JsonProperty("rows")]
        public List<TRow> Rows;

        [JsonIgnore]
        public string[] Headers =>
            Columns
                .Select(header => header.Item1)
                .ToArray();

        [JsonIgnore]
        public string[][] Cells =>
            Rows
                .Select(
                    row => Columns
                        .Select(column => column.Item2(row))
                        .ToArray()
                )
                .ToArray();
    }

    public class TableToHtmlDisplayEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.Html;
        
        public TableCellAlignment TableCellAlignment { get; set; } = TableCellAlignment.Left;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is ITable table)
            {
                var headers = table.Headers;
                var cells = table.Cells;

                return new EncodedData
                {
                    Data =
                        "<table>" +
                            "<thead>" +
                                "<tr>" +
                                    String.Join("",
                                        headers.Select(
                                            header => $"<th {TableCellAlignment.ToStyleAttribute()}>{header}</th>"
                                        )
                                    ) +
                                "</tr>" +
                            "</thead>" +
                            "<tbody>" +
                                String.Join("",
                                    cells.Select(row =>
                                        "<tr>" +
                                        String.Join("",
                                            row.Select(
                                                cell => $"<td {TableCellAlignment.ToStyleAttribute()}>{cell}</td>"
                                            )
                                        ) +
                                        "</tr>"
                                    )
                                ) +
                            "</tbody>" +
                        "</table>"
                };
            } else return null;
        }
    }

    public class TableToTextDisplayEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.PlainText;
        
        public TableCellAlignment TableCellAlignment { get; set; } = TableCellAlignment.Left;

        public EncodedData? Encode(object displayable)
        {
            if (displayable is ITable table)
            {
                var headers = table.Headers;
                var cells = table.Cells;

                // For the text, we need to find how wide each column is.
                var widths = headers.Select(column => 0).ToArray();
                var nCols = widths.Length;
                foreach (var row in cells)
                {
                    foreach (var idx in Enumerable.Range(0, nCols))
                    {
                        if (row[idx].Length > widths[idx])
                        {
                            widths[idx] = row[idx].Length;
                        }
                    }
                }

                var text = new StringBuilder();
                text.Append(String.Join(" ",
                    headers.Select((header, idxCol) =>
                        header.PadCell(widths[idxCol], TableCellAlignment)
                    )
                ).TrimEnd());
                text.Append(Environment.NewLine);
                text.Append(String.Join(" ",
                    widths.Select(width => new String('-', width))
                ).TrimEnd());
                text.Append(Environment.NewLine);
                foreach (var row in cells)
                {
                    text.Append(String.Join(" ",
                        row.Select(
                            (cell, idxCol) => cell.PadCell(widths[idxCol], TableCellAlignment)
                        )
                    ).TrimEnd());
                    text.Append(Environment.NewLine);
                }


                return text.ToString().ToEncodedData();
            } else return null;
        }
    }
}