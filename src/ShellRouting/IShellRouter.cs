// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    public interface IShellHandler
    {
        public string MessageType { get; }
        public void Handle(Message message);
    }

    public interface IShellRouter
    {
        public void RegisterHandler(string messageType, Action<Message> handler);

        public void RegisterHandler(IShellHandler handler) =>
            RegisterHandler(handler.MessageType, handler.Handle);

        public void RegisterFallback(Action<Message> fallback);

        public void Handle(Message message);

        public void RegisterHandlers<TAssembly>();
    }
}
