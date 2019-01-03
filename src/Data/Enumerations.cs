// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Jupyter.Core
{

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ExecuteStatus
    {
        [EnumMember(Value="ok")]
        Ok,

        [EnumMember(Value="error")]
        Error,

        [EnumMember(Value="abort")]
        Abort
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ExecutionState
    {
        [EnumMember(Value="busy")]
        Busy,

        [EnumMember(Value="idle")]
        Idle,

        [EnumMember(Value="starting")]
        Starting
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum StreamName
    {
        [EnumMember(Value="stdin")]
        StandardIn,

        [EnumMember(Value="stdout")]
        StandardOut,

        [EnumMember(Value="stderr")]
        StandardError
    }

    public enum Transport
    {
        [EnumMember(Value="tcp")]
        Tcp
    }

    public enum SignatureScheme
    {
        [EnumMember(Value="hmac-sha256")]
        HmacSha256
    }

}
