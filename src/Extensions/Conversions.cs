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

namespace Microsoft.Jupyter.Core
{
    public static partial class Extensions
    {

        public static byte[] HexToBytes(this string hex)
        {
            var bytes = new byte[hex.Length / 2];
            foreach (var idxHexPair in Enumerable.Range(0, hex.Length / 2))
            {
                bytes[idxHexPair] = Convert.ToByte(hex.Substring(2 * idxHexPair, 2), 16);
            }
            return bytes;
        }

        public static EncodedData ToEncodedData(this string data) =>
            new EncodedData
            {
                Data = data,
                Metadata = null
            };

        public static ExecutionResult ToExecutionResult(this ExecuteStatus status) =>
            new ExecutionResult
            {
                Status = status,
                Output = null
            };

        public static ExecutionResult ToExecutionResult(this object output, ExecuteStatus status = ExecuteStatus.Ok) =>
            new ExecutionResult
            {
                Status = status,
                Output = output
            };

    }
}