// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Net;
using System.Runtime.Serialization;

namespace Microsoft.Jupyter.Core
{

    public class IpAddressConverter : JsonConverter<IPAddress>
    {
        public override IPAddress ReadJson(JsonReader reader, Type objectType, IPAddress existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }
            else if (reader.TokenType == JsonToken.String)
            {
                return IPAddress.Parse((string) serializer.Deserialize(reader, typeof(string)));
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public override void WriteJson(JsonWriter writer, IPAddress value, JsonSerializer serializer) =>
            writer.WriteValue(value.ToString());
    }

}
