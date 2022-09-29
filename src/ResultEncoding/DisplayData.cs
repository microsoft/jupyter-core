using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{

    public static class MimeTypes
    {
        public const string PlainText = "text/plain";
        public const string Json = "application/json";
        public const string Html = "text/html";
        public const string Markdown = "text/markdown";
    }

    /// <summary>
    ///     Represents a Jupyter protocol MIME bundle, a pair of dictionaries
    ///     keyed by MIME types.
    /// </summary>
    /// <remarks>
    ///     Both the data and metadata dictionaries are string-valued, even though
    ///     the Jupyter protocol allows for arbitrary JSON objects with strings
    ///     being a special case. We adopt this restriction as some clients (in
    ///     particular, jupyter_client) do not properly handle the more general case.
    /// </remarks>
    internal struct MimeBundle
    {
        public Dictionary<string, string> Data;
        public Dictionary<string, Dictionary<string, JToken>> Metadata;

        public static MimeBundle Empty() =>
            new MimeBundle
            {
                Data = new(),
                Metadata = new()
            };
    }

    public struct EncodedData
    {
        public string Data;
        public Dictionary<string, JToken> Metadata;
    }

    public interface IResultEncoder
    {
        string MimeType { get; }
        EncodedData? Encode(object displayable);
    }

}
