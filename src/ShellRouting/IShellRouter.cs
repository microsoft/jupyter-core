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
    ///     Represents a class that can be used to route incoming shell
    ///     messages to appropriate handlers.
    /// </summary>
    public interface IShellRouter
    {

        /// <summary>
        ///     Registers an action that can be used to handle a
        ///     particular message type. This action either returns a task
        ///     that can be awaited if further processing is required, or
        ///     <c>null</c> if the handler has completed handling the message.
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
        public void RegisterHandler(string messageType, Func<Message, Task?> handler);

        public void RegisterHandler<THandler>(IServiceProvider serviceProvider) =>
            RegisterHandler(
                (IShellHandler)ActivatorUtilities.CreateInstance(serviceProvider, typeof(THandler))
            );

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
        public void RegisterFallback(Func<Message, Task?> fallback);

        /// <summary>
        ///     Calls the appropriate handler for a given message,
        ///     or the fallback handler if no more appropriate handler
        ///     exists.
        /// </summary>
        /// <param name="message">
        ///     The message to be handled.
        /// </param>
        public Task? Handle(Message message);

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
