// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Threading.Tasks;
using Microsoft.Jupyter.Core.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{

    public enum CommSessionClosedBy
    {
        Client,
        Kernel,
    }

    public interface ICommSession
    {
        /// <summary>
        ///     Raised when a new message is available from the client.
        /// </summary>
        event Func<CommMessageContent, Task>? OnMessage;

        /// <summary>
        ///     Raised when this session is closed, whether by the kernel or
        ///     by the client.
        /// </summary>
        event Func<CommSessionClosedBy, Task>? OnClose;

        /// <summary>
        ///     If <c>true</c>, then this session is still open and can be used
        ///     to send and receive comms messages.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        ///     A unique ID identifying this session as part of a larger Jupyter
        ///     messaging session.
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     Sends a comm message to the client.
        /// </summary>
        Task SendMessage(object contents);

        /// <summary>
        ///     Closes this comm session, notifying the client if the session
        ///     was not already closed.
        /// </summary>
        Task Close();
    }

    public interface ICommSessionOpen
    {
        event Func<ICommSession, JToken, Task>? On;
    }

    public interface ICommsRouter
    {
        ICommSessionOpen SessionOpenEvent(string targetName);

        Task<ICommSession> OpenSession(string targetName, object? data);

    }
}
