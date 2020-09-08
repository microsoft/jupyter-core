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
        private readonly object taskDepthLock = new object();

        protected virtual ILogger? Logger { get; set; } = null;

        public abstract string MessageType { get; }
        public abstract Task<TResult> HandleAsync(Message message, TResult? previousResult);

        public Task HandleAsync(Message message)
        {
            Logger?.LogDebug("Handing {MessageType} with ordered shell handler.", message.Header.MessageType);
            currentTask = new Task<TResult?>((state) =>
            {
                lock (taskDepthLock)
                {
                    taskDepth++;
                }
                var previousTask = (Task<TResult?>?)state;
                var previousResult = previousTask?.Result;
                return HandleAsync(message, previousResult)
                    .ContinueWith<TResult>((task) =>
                    {
                        lock (taskDepthLock)
                        {
                            taskDepth--;
                            if (taskDepth == 0)
                            {
                                currentTask = null;
                            }
                        }
                        return task.Result;
                    })
                    .Result;
            }, currentTask);
            currentTask.Start();
            return currentTask;
        }
    }
}
