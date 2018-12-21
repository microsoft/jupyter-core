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

    public struct DisplayData
    {
        public Dictionary<string, object> Data;
        public Dictionary<string, object> Metadata;

        public static DisplayData Empty() => new DisplayData
        {
            Data = new Dictionary<string, object>(),
            Metadata = new Dictionary<string, object>()
        };
    }

    public interface IDisplaySerializer
    {
        DisplayData? Serialize(object displayable);
    }

    public class JsonDisplaySerializer : IDisplaySerializer
    {
        private readonly ILogger logger;
        private readonly JsonConverter[] converters;

        public JsonDisplaySerializer(ILogger logger = null, JsonConverter[] converters = null)
        {
            this.logger = logger;
            this.converters = converters ?? new JsonConverter[] {};
        }
        public DisplayData? Serialize(object displayable)
        {
            try
            {
                var serialized = JsonConvert.SerializeObject(displayable, converters);
                return new DisplayData
                {
                    Data = new Dictionary<string, object>
                    {
                        ["application/json"] = new JRaw(serialized)
                    },
                    Metadata = new Dictionary<string, object>()
                };
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to serialize display data of type {Type}.", displayable.GetType().ToString());
                return null;
            }
        }
    }

    public class PlainTextDisplaySerializer : IDisplaySerializer
    {
        public DisplayData? Serialize(object displayable) =>
            new DisplayData
            {
                Data = new Dictionary<string, object>
                {
                    ["text/plain"] = displayable.ToString()
                },
                Metadata = new Dictionary<string, object>()
            };
    }

    public class ListDisplaySerializer : IDisplaySerializer
    {
        public DisplayData? Serialize(object displayable)
        {
            if (displayable is string)
            {
                return null;
            }
            else if (displayable is IEnumerable enumerable)
            {
                var list = String.Join("\n",
                    from object item in enumerable
                    select $"<li>{item}</li>"
                );
                return new DisplayData
                {
                    Data = new Dictionary<string, object>
                    {
                        ["text/plain"] = String.Join("\n",
                            enumerable.Cast<object>().Select(item => item.ToString())
                        ),
                        ["text/html"] = $"<ul>{list}</ul>"
                    },
                    Metadata = new Dictionary<string, object>()
                };
            }
            else return null;
        }
    }
}
