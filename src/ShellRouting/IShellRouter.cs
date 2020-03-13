// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

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
        ///     Called by the shell server to handle a message coming in from the client.
        /// </summary>
        /// <param name="message">
        ///     The incoming message to be handled.
        /// </param>
        public void Handle(Message message);

        /// <summary>
        ///     Called by the shell server to asynchronously handle a message
        ///     coming in from the client.
        /// </summary>
        /// <param name="message">
        ///     The incoming message to be handled.
        /// </param>
        public async Task HandleAsync(Message message) =>
            await Task.Run(() => Handle(message));
    }

    /// <summary>
    ///     Represents a class that can be used to route incoming shell
    ///     messages to appropriate handlers.
    /// </summary>
    public interface IShellRouter
    {
        /// <summary>
        ///     Registers an action that can be used to handle a
        ///     particular message type.
        /// </summary>
        /// <param name="messageType">
        ///     The type of message to be handled.
        /// </param>
        /// <param name="handler">
        ///     An action that can be used to handle incoming messages of
        ///     type <c>messageType</c>.
        /// </param>
        /// <example>
        ///     To register a handler that logs <c>ping_request</c>
        ///     message IDs:
        ///     <code><![CDATA[
        ///         router.RegisterHandler(
        ///             "ping_request"
        ///             message => logger.LogDebug(
        ///                 "Got ping_request with id {Id}.",
        ///                 message.Header.Id
        ///             )
        ///         );
        ///     ]]></code>
        /// </example>
        public void RegisterHandler(string messageType, Action<Message> handler)
        {
            RegisterHandler(
                messageType,
                async message =>
                    await Task.Run(() => handler(message))
            );
        }

        
        /// <summary>
        ///     Registers an action that can be used to asynchronously handle a
        ///     particular message type.
        /// </summary>
        /// <param name="messageType">
        ///     The type of message to be handled.
        /// </param>
        /// <param name="handler">
        ///     An action that can be used to handle incoming messages of
        ///     type <c>messageType</c>.
        /// </param>
        /// <example>
        ///     To register a handler that logs <c>ping_request</c>
        ///     message IDs:
        ///     <code><![CDATA[
        ///         router.RegisterHandler(
        ///             "ping_request"
        ///             async (message) => logger.LogDebug(
        ///                 "Got ping_request with id {Id}.",
        ///                 message.Header.Id
        ///             )
        ///         );
        ///     ]]></code>
        /// </example>
        public void RegisterHandler(string messageType, Func<Message, Task> handler);

        /// <summary>
        ///     Registers a handler that can be used to handle a
        ///     particular message type.
        /// </summary>
        /// <param name="handler">
        ///     The handler to be registered; the
        ///     <see cref="Microsoft.Jupyter.Core.IShellHandler.MessageType" />
        ///     property of the handler will be used to define the
        ///     message type to be handled.
        /// </param>
        public void RegisterHandler(IShellHandler handler) =>
            RegisterHandler(handler.MessageType, handler.HandleAsync);

        /// <summary>
        ///     Registers an action to be used to handle messages
        ///     whose message types do not have appropriate handlers.
        /// </summary>
        /// <param name="fallback">
        ///     An action that can be used to handle messages
        ///     whose message types do not have appropriate handlers.
        /// </param>
        public void RegisterFallback(Func<Message, Task> fallback);

        /// <summary>
        ///     Calls the appropriate handler for a given message,
        ///     or the fallback handler if no more appropriate handler
        ///     exists.
        /// </summary>
        /// <param name="message">
        ///     The message to be handled.
        /// </param>
        public Task HandleAsync(Message message);

        /// <summary>
        ///     Searches an assembly for types representing shell
        ///     handlers and registers each handler.
        /// </summary>
        /// <typeparam name="TAssembly">
        ///     A type from the assembly to be searched.
        /// </typeparam>
        public void RegisterHandlers<TAssembly>();
    }
}
