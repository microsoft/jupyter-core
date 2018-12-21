using System;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{
    public class DynValueConverter : JsonConverter<DynValue>
    {
        public override DynValue ReadJson(JsonReader reader, Type objectType, DynValue existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override void WriteJson(JsonWriter writer, DynValue value, JsonSerializer serializer)
        {
            var obj = value.ToObject();
            JToken token;
            switch (obj)
            {
                case MoonSharp.Interpreter.Closure closure:
                    token = JToken.FromObject(new Dictionary<string, object>
                    {
                        ["@closure"] = closure.ToString()
                    });
                    token.WriteTo(writer);
                    return;

                default:
                    // See https://github.com/JamesNK/Newtonsoft.Json/issues/386#issuecomment-421161191
                    // for why this works to pass through.
                    token = JToken.FromObject(obj);
                    token.WriteTo(writer);
                    return;
            }
        }
    }
}