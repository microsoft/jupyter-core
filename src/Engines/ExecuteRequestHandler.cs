// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Microsoft.Jupyter.Core.Protocol;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Jupyter.Core
{
    internal class ExecuteRequestHandler : OrderedShellHandler<ExecutionResult>
    {

        private BaseEngine engine;
        private IShellServer shellServer;

        private Task<bool>? previousTask = null;
        private ILogger<ExecuteRequestHandler>? logger = null;
        private ICommsRouter router;

        protected override ILogger? Logger => logger;

        /// <summary>
        ///      The number of cells that have been executed since the start of
        ///      this engine. Used by clients to typeset cell numbers, e.g.:
        ///      <c>In[12]:</c>.
        /// </summary>
        public int ExecutionCount { get; protected set; } = 0;

        public ExecuteRequestHandler(IExecutionEngine engine, IShellServer shellServer, ICommsRouter router, ILogger<ExecuteRequestHandler> logger)
        {
            if (engine is BaseEngine baseEngine)
            {
                this.engine = baseEngine;
            }
            else throw new Exception("The ExecuteRequestHandler requires that the IExecutionEngine service inherits from BaseEngine.");
            this.shellServer = shellServer;
            this.logger = logger;
            this.router = router;
        }

        public override string MessageType => "execute_request";
        
        protected async virtual Task<ExecutionResult> ExecutionTaskForMessage(Message message, int executionCount, Action onHandled)
        {
            var engineResponse = ExecutionResult.Aborted;

            var code = (message.Content as ExecuteRequestContent)?.Code ?? string.Empty;

            this.shellServer.SendIoPubMessage(
                new Message
                {
                    ZmqIdentities = message.ZmqIdentities,
                    ParentHeader = message.Header,
                    Metadata = new Dictionary<string, object>(),
                    Content = new ExecuteInputContent
                    {
                        Code = code,
                        ExecutionCount = executionCount
                    },
                    Header = new MessageHeader
                    {
                        MessageType = "execute_input"
                    }
                }
            );

            using (var cancellationTokenSource = new CancellationTokenSource())
            {
                Action<Message> onInterruptRequest = (message) => cancellationTokenSource.Cancel();

                var shellServerSupportsInterrupt = this.shellServer as IShellServerSupportsInterrupt;
                if (shellServerSupportsInterrupt != null)
                {
                    shellServerSupportsInterrupt.InterruptRequest += onInterruptRequest;
                }

                // Make sure that the engine is fully initialized.
                await engine.Initialized;

                try
                {
                    engineResponse = await engine.Execute(
                        code,
                        new BaseEngine.ExecutionChannel(engine, message, router),
                        cancellationTokenSource.Token
                    );
                }
                finally
                {
                    if (shellServerSupportsInterrupt != null)
                    {
                        shellServerSupportsInterrupt.InterruptRequest -= onInterruptRequest;
                    }
                }
            }
            
            // Send the engine's output as an execution result.
            if (engineResponse.Output != null)
            {
                var serialized = engine.EncodeForDisplay(engineResponse.Output);
                this.shellServer.SendIoPubMessage(
                    new Message
                    {
                        ZmqIdentities = message.ZmqIdentities,
                        ParentHeader = message.Header,
                        Metadata = new Dictionary<string, object>(),
                        Content = new ExecuteResultContent
                        {
                            ExecutionCount = executionCount,
                            Data = serialized.Data,
                            Metadata = serialized.Metadata
                        },
                        Header = new MessageHeader
                        {
                            MessageType = "execute_result"
                        }
                    }
                );
            }

            // Invoke the onHandled callback prior to sending the execute_reply message.
            // This guarantees that the OrderedShellHandler correctly processes the completion
            // of this task *before* any processing new task that the client may submit for
            // execution immediately upon receiving the execute_reply message.
            onHandled();

            // Handle the message.
            this.shellServer.SendShellMessage(
                new Message
                {
                    ZmqIdentities = message.ZmqIdentities,
                    ParentHeader = message.Header,
                    Metadata = new Dictionary<string, object>(),
                    Content = new ExecuteReplyContent
                    {
                        ExecuteStatus = engineResponse.Status,
                        ExecutionCount = executionCount,
                        UserExpressions = new Dictionary<string, object>()
                    },
                    Header = new MessageHeader
                    {
                        MessageType = "execute_reply"
                    }
                }
            );

            return engineResponse;
        }

        protected async Task SendAbortMessage(Message message)
        {
            // The previous call failed, so abort here and let the
            // shell server know.
            this.shellServer.SendShellMessage(
                new Message
                {
                    ZmqIdentities = message.ZmqIdentities,
                    ParentHeader = message.Header,
                    Metadata = new Dictionary<string, object>(),
                    Content = new ExecuteReplyContent
                    {
                        ExecuteStatus = ExecuteStatus.Abort,
                        ExecutionCount = null,
                        UserExpressions = new Dictionary<string, object>()
                    },
                    Header = new MessageHeader
                    {
                        MessageType = "execute_reply"
                    }
                }
            );

            // Finish by telling the client that we're free again.
            this.shellServer.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "status"
                    },
                    Content = new KernelStatusContent
                    {
                        ExecutionState = ExecutionState.Idle
                    }
                }.AsReplyTo(message)
            );
        }

        private int IncrementExecutionCount()
        {
            lock (this)
            {
                return ++this.ExecutionCount;
            }
        }

        public override async Task<ExecutionResult> HandleAsync(Message message, ExecutionResult? previousResult) =>
            await HandleAsync(message, previousResult, () => {});

        public override async Task<ExecutionResult> HandleAsync(Message message, ExecutionResult? previousResult, Action onHandled)
        {
            this.logger.LogDebug($"Asked to execute code:\n{((ExecuteRequestContent)message.Content).Code}");

            if (previousResult != null && previousResult.Value.Status != ExecuteStatus.Ok)
            {
                this.logger.LogDebug("Aborting due to previous execution result indicating failure: {PreviousResult}", previousResult.Value);
                onHandled();
                await SendAbortMessage(message);
                return ExecutionResult.Aborted;
            }

            var executionCount = IncrementExecutionCount();
            shellServer.NotifyBusyStatus(message, ExecutionState.Busy);

            try
            {
                var result = await ExecutionTaskForMessage(message, executionCount, onHandled);
                return result;
            }
            catch (TaskCanceledException tce)
            {
                this.logger?.LogDebug(tce, "Task cancelled.");
                return new ExecutionResult
                {
                    Output = null,
                    Status = ExecuteStatus.Abort
                };
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, "Unable to process ExecuteRequest");
                return new ExecutionResult
                {
                    Output = e,
                    Status = ExecuteStatus.Error
                };
            }
            finally
            {
                shellServer.NotifyBusyStatus(message, ExecutionState.Idle);
            }
        }

    }

}
