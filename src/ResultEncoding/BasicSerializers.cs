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
