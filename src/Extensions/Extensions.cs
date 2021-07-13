// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Linq;
using NetMQ;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      Provides extension methods used throughout the Jupyter Core library.
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        ///      Given some raw data, attempts to decode it into an object of
        ///      a given type, returning <c>false</c> if the deserialization
        ///      fails.
        /// </summary>
        public static bool TryAs<T>(this JToken rawData, out T converted)
        where T: class
        {
            try
            {
                converted = rawData.ToObject<T>();
                return true;
            }
            catch
            {
                converted = null;
                return false;
            }
        }
    }
}
