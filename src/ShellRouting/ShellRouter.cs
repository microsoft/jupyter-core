// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{

    /// <summary>
    ///     Routes shell messages to handlers based on the message type
    ///     of each incoming shell messsge.
    /// </summary>
    public class ShellRouter : IShellRouter
    {
        private readonly IDictionary<string, Action<Message>> shellHandlers = new Dictionary<string, Action<Message>>();
        private Action<Message>? fallback;
        private readonly ILogger<ShellRouter> logger;
        private IServiceProvider services;

        public ShellRouter(
            IServiceProvider services,
            ILogger<ShellRouter> logger
        )
        {
            if (services == null) { throw new ArgumentNullException(nameof(services)); }
            if (logger == null) { throw new ArgumentNullException(nameof(logger)); }
            this.logger = logger;
            this.services = services;

            // Set a default fallback action.
            RegisterFallback(message =>
                logger.LogWarning("Unrecognized custom shell message of type {Type}: {Message}", message.Header.MessageType, message)
            );
        }

        public void Handle(Message message) =>
            (
                shellHandlers.TryGetValue(message.Header.MessageType, out var handler)
                ? handler : fallback
            )?.Invoke(message);

        public void RegisterHandler(string messageType, Action<Message> handler)
        {
            shellHandlers[messageType] = handler;
        }

        public void RegisterFallback(Action<Message> fallback) =>
            this.fallback = fallback;

        public void RegisterHandlers<TAssembly>()
        {
            var handlers = typeof(TAssembly)
                .Assembly
                .GetTypes()
                .Where(t =>
                {
                    if (!t.IsClass && t.IsAbstract) { return false; }
                    var matched = t
                        .GetInterfaces()
                        .Contains(typeof(IShellHandler));
                    this.logger.LogDebug("Class {Class} subclass of CustomShellHandler? {Matched}", t.FullName, matched);
                    return matched;
                })
                .Select(handlerType =>
                    ActivatorUtilities.CreateInstance(services, handlerType)
                )
                .Cast<IShellHandler>();

            foreach (var handler in handlers)
            {
                logger.LogInformation("Registering handler for type: {Type}", handler.MessageType);
                ((IShellRouter) this).RegisterHandler(handler);
            }
        }
    }
}
