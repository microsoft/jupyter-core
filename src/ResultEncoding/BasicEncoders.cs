using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{

    public class JsonResultEncoder : IResultEncoder
    {
        private readonly ILogger? logger;
        private readonly JsonConverter[] converters;

        public string MimeType => MimeTypes.Json;

        public JsonResultEncoder(ILogger? logger = null, IEnumerable<JsonConverter>? converters = null)
        {
            this.logger = logger;
            this.converters = converters?.ToArray() ?? new JsonConverter[] {};
        }

        public EncodedData? Encode(object displayable)
        {
            if (displayable == null) return null;

            try
            {
                var serialized = JsonConvert.SerializeObject(displayable, converters);
                return serialized.ToEncodedData();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to serialize display data of type {Type}.", displayable.GetType().ToString());
                return null;
            }
        }
    }

    public class PlainTextResultEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.PlainText;
        public EncodedData? Encode(object displayable) =>
            Extensions.ToEncodedData(displayable?.ToString());
    }

    public class ListToTextResultEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.PlainText;
        public EncodedData? Encode(object displayable)
        {
            if (displayable == null) return null;

            if (displayable is string)
            {
                return null;
            }
            else if (displayable is IEnumerable enumerable)
            {
                return String.Join(", ",
                    enumerable.Cast<object>().Select(item => item.ToString())
                ).ToEncodedData();
            }
            else return null;
        }
    }

    public class ListToHtmlResultEncoder : IResultEncoder
    {
        public string MimeType => MimeTypes.Html;
        public EncodedData? Encode(object displayable)
        {
            if (displayable == null) return null;

            if (displayable is string)
            {
                return null;
            }
            else if (displayable is IEnumerable enumerable)
            {
                var list = String.Join("",
                    from object item in enumerable
                    select $"<li>{item}</li>"
                );
                return $"<ul>{list}</ul>".ToEncodedData();
            }
            else return null;
        }
    }

    public class FuncResultEncoder : IResultEncoder
    {
        private Func<object, EncodedData?> encode;
        private readonly string mimeType;

        public string MimeType => mimeType;

        public FuncResultEncoder(string mimeType, Func<object, EncodedData?> encode)
        {
            this.mimeType = mimeType;
            this.encode = encode;
        }

        public FuncResultEncoder(string mimeType, Func<object, string> encode)
        : this(mimeType, displayable => encode(displayable).ToEncodedData()) {}

        public EncodedData? Encode(object displayable)
        {
            if (displayable == null) return null;
            return encode(displayable);
        }
    }

}
