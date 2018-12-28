// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{
    public struct ExecutionResult
    {
        public ExecuteStatus Status;
        public object Output;
    }

    public static class MimeTypes
    {
        public const string PlainText = "text/plain";
        public const string Json = "application/json";
        public const string Html = "text/html";
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
        public Dictionary<string, string> Metadata;

        public static MimeBundle Empty() =>
            new MimeBundle
            {
                Data = new Dictionary<string, string>(),
                Metadata = new Dictionary<string, string>()
            };
    }

    public struct EncodedData
    {
        public string MimeType;
        public string Data;
        public string Metadata;
    }

    public interface IResultEncoder
    {
        IEnumerable<EncodedData> Encode(object displayable);
    }

    public class JsonResultEncoder : IResultEncoder
    {
        private readonly ILogger logger;
        private readonly JsonConverter[] converters;

        public JsonResultEncoder(ILogger logger = null, JsonConverter[] converters = null)
        {
            this.logger = logger;
            this.converters = converters ?? new JsonConverter[] {};
        }
        public IEnumerable<EncodedData> Encode(object displayable)
        {
            try
            {
                var serialized = JsonConvert.SerializeObject(displayable, converters);
                return new[]
                {
                    new EncodedData
                    {
                        MimeType = "application/json",
                        Data = serialized
                    }
                };
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to serialize display data of type {Type}.", displayable.GetType().ToString());
                return new EncodedData[] { };
            }
        }
    }

    public class PlainTextResultEncoder : IResultEncoder
    {
        public IEnumerable<EncodedData> Encode(object displayable) =>
            new[]
            {
                new EncodedData
                {
                    MimeType = MimeTypes.PlainText,
                    Data = displayable.ToString()
                }
            };
    }

    public class ListResultEncoder : IResultEncoder
    {
        public IEnumerable<EncodedData> Encode(object displayable)
        {
            if (displayable is string)
            {
                return new EncodedData[] { };
            }
            else if (displayable is IEnumerable enumerable)
            {
                var list = String.Join("\n",
                    from object item in enumerable
                    select $"<li>{item}</li>"
                );
                return new[]
                {
                    new EncodedData
                    {
                        MimeType = MimeTypes.PlainText,
                        Data = String.Join("\n",
                            enumerable.Cast<object>().Select(item => item.ToString())
                        )
                    },
                    new EncodedData
                    {
                        MimeType = MimeTypes.Html,
                        Data = $"<ul>{list}</ul>"
                    }
                };
            }
            else return new EncodedData[] {};
        }
    }
}
