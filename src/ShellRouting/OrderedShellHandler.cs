// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    public abstract class OrderedShellHandler<TResult> : IShellHandler
    where TResult: struct
    {
        private Task<TResult?>? currentTask = null;

        private int taskDepth = 0;

        protected virtual ILogger? Logger { get; set; } = null;

        public abstract string MessageType { get; }
        public abstract Task<TResult> HandleAsync(Message message, TResult? previousResult);

        public Task HandleAsync(Message message)
        {
            Logger?.LogDebug("Handing {MessageType} with ordered shell handler.", message.Header.MessageType);
            lock (this)
            {
                taskDepth++;
                currentTask = new Task<TResult?>((state) =>
                {
                    lock (this)
                    {
                        var previousTask = (Task<TResult?>?)state;
                        var previousResult = previousTask?.Result;
                        var currentResult = HandleAsync(message, previousResult).Result;
                        taskDepth--;
                        if (taskDepth == 0)
                        {
                            currentTask = null;
                        }
                        return currentResult;
                    }
                }, currentTask);
                currentTask.Start();
                return currentTask;
            }
        }
    }
}
