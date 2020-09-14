// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    public abstract class OrderedShellHandler<TResult> : IShellHandler
    where TResult: struct
    {
        private ConcurrentQueue<Task<TResult?>> taskQueue = new ConcurrentQueue<Task<TResult?>>();

        protected virtual ILogger? Logger { get; set; } = null;

        public abstract string MessageType { get; }
        public abstract Task<TResult> HandleAsync(Message message, TResult? previousResult);
        public virtual Task<TResult> HandleAsync(Message message, TResult? previousResult, Action onHandled) =>
            HandleAsync(message, previousResult);

        public Task HandleAsync(Message message)
        {
            Logger?.LogDebug("Handing {MessageType} with ordered shell handler.", message.Header.MessageType);

            var previousTask = taskQueue.LastOrDefault();
            var task = new Task<TResult?>(() =>
            {
                // lock to serialize task execution
                lock (this)
                {
                    var handled = false;
                    Action onHandled = () =>
                    {
                        handled = true;
                        taskQueue.TryDequeue(out var task);
                    };
                    var currentResult = HandleAsync(message, previousTask?.Result, onHandled).Result;
                    if (!handled)
                    {
                        onHandled();
                    }
                    return currentResult;
                }
            });

            taskQueue.Enqueue(task);
            task.Start();
            return task;
        }
    }
}
