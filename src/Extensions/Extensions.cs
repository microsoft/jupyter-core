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
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      Provides extension methods used throughout the Jupyter Core library.
    /// </summary>
    public static partial class Extensions
    {
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
                    }
                }.AsReplyTo(message)
            );
        }
    }
}