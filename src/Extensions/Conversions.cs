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

        /// <summary>
        ///      Converts a string containing hexadecimal digits to an array of
        ///      bytes representing the same data.
        /// </summary>
        /// <param name="hex">
        ///     A string containing an even number of hexadecimal characters
        ///     (0-f).
        /// </param>
        /// <returns>An array of bytes representing the same data.</returns>
        public static byte[] HexToBytes(this string hex)
        {
            var bytes = new byte[hex.Length / 2];
            foreach (var idxHexPair in Enumerable.Range(0, hex.Length / 2))
            {
                bytes[idxHexPair] = Convert.ToByte(hex.Substring(2 * idxHexPair, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        ///     Encapsulates encoded data into an <see cref="EncodedData" />
        ///     value without metadata.
        /// </summary>
        public static EncodedData ToEncodedData(this string data) =>
            new EncodedData
            {
                Data = data,
                Metadata = ""
            };

        /// <summary>
        ///     Encapsulates an execution status as a result object without
        ///     output. This is useful when an input being executed has
        ///     completed, but has not produced output; e.g. after a print
        ///     statement or function.
        /// </summary>
        /// <param name="status">
        ///     The status to be encapsulated as the result of an execution.
        /// </param>
        public static ExecutionResult ToExecutionResult(this ExecuteStatus status) =>
            new ExecutionResult
            {
                Status = status,
                Output = null
            };

        /// <summary>
        ///      Encapsulates a given output as the result of an execution.
        ///      By default, this method denotes that an execution completed
        ///      successfully.
        /// </summary>
        /// <param name="output">
        ///      The output from an execution.
        /// </param>
        /// <param name="status">
        ///     The status to be encapsulated as the result of an execution.
        /// </param>
        public static ExecutionResult ToExecutionResult(this object output, ExecuteStatus status = ExecuteStatus.Ok) =>
            new ExecutionResult
            {
                Status = status,
                Output = output
            };

    }
}