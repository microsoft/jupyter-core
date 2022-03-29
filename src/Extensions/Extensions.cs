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
using Microsoft.Jupyter.Core.Protocol;

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

        internal static void NotifyBusyStatus(this IShellServer shellServer, Message message, ExecutionState state)
        {
            // Begin by sending that we're busy.
            shellServer.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "status"
                    },
                    Content = new KernelStatusContent
                    {
                        ExecutionState = state
                    },
                    Metadata = ""
                }.AsReplyTo(message)
            );
        }

        internal static CompletionResult AsCompletionResult(this IEnumerable<BaseEngine.Completion> completions, string code, int cursorPos)
        {
            // Since Jupyter's messaging protocol assumes a single cursor start and end for all completions, we need
            // to make a common cursor start from the minimum cursor start, and similarly for the cursor end.
            var minCursor = cursorPos;
            var maxCursor = cursorPos;
            if (completions.Any())
            {
                minCursor = completions.Min(completion => completion.CursorStart);
                maxCursor = completions.Max(completion => completion.CursorEnd);
            }

            return new CompletionResult
            {
                Status = CompleteStatus.Ok,
                CursorStart = minCursor,
                CursorEnd = maxCursor,
                Matches = completions
                    .Select(completion =>
                    {
                        var prefix = code.Substring(minCursor, completion.CursorStart - minCursor);
                        var suffix = code.Substring(maxCursor, completion.CursorEnd - maxCursor);
                        return prefix + completion.Text + suffix;
                    })
                    .ToList()
            };
        }
    }
}
