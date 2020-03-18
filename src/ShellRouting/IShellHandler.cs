// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///     Represents a class that can handle and respond to
    ///     shell messages coming in from a client.
    /// </summary>
    public interface IShellHandler
    {
        /// <summary>
        ///     The message type handled by this handler (e.g.: <c>kernel_info_request</c>).
        /// </summary>
        public string MessageType { get; }

        /// <summary>
        ///     Called by the shell server to asynchronously handle a message
        ///     coming in from the client. Either returns <c>null</c> if no
        ///     further handling is required, or a task that can be awaited on
        ///     for the message to be completely handled.
        /// </summary>
        /// <param name="message">
        ///     The incoming message to be handled.
        /// </param>
        public Task HandleAsync(Message message);
    }
}
