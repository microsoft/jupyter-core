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
    public abstract class OrderedShellHandler<TResult> : IShellHandler
    {
        private Task<TResult>? currentTask = null;

        public abstract Task<TResult> HandleAsync(Message message, TResult? previousResult);

        public Task HandleAsync(Message message)
        {
            currentTask = new Task((state) =>
            {
                var previousTask = (Task<TResult>?)currentTask;
                var previousResult = previousTask?.Result;
                var currentResult = HandleAsync(message, previousResult).Result;
            }, currentTask);
            currentTask.Start();
            return currentTask;
        }
    }
}
