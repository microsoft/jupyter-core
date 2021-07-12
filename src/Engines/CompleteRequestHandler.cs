// Copyright (c) Microsoft Corporation.
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
    internal class CompleteRequestHandler : IShellHandler
    {
        private BaseEngine engine;
        private IShellServer shellServer;
        private ILogger<CompleteRequestHandler> logger;

        public CompleteRequestHandler(IExecutionEngine engine, IShellServer shellServer, ILogger<CompleteRequestHandler> logger)
        {
            if (engine is BaseEngine baseEngine)
            {
                this.engine = baseEngine;
            }
            else throw new Exception("The CompleteRequestHandler requires that the IExecutionEngine service inherits from BaseEngine.");
            this.shellServer = shellServer;
            this.logger = logger;
        }

        public string MessageType => "complete_request";

        public async Task HandleAsync(Message message)
        {
            await engine.Initialized;

            var request = message.Content as CompleteRequestContent;
            if (request == null)
            {
                logger.LogError("Expected completion result content, but got {Type} instead.", message.Content.GetType());
                return;
            }
            this.logger.LogDebug("Ask to complete code at cursor position {CursorPos}:\n{Code}", request.CursorPos, request.Code);

            shellServer.NotifyBusyStatus(message, ExecutionState.Busy);
            try
            {
                var completion = await engine.Complete(request.Code, request.CursorPos);
                // If we got a completion back from the engine that was anything
                // other than null, respond with it here. Note that unlike execute_request,
                // it's ok to just ignore a complete request that we don't know
                // how to handle.
                if (completion != null)
                {
                    this.shellServer.SendShellMessage(
                        new Message
                        {
                            Content = new CompleteReplyContent
                            {
                                CompleteStatus = completion.Value.Status,
                                Matches = completion.Value.Matches.ToList(),
                                CursorStart = completion.Value.CursorStart ?? request.CursorPos,
                                CursorEnd = completion.Value.CursorEnd ?? request.CursorPos
                            },
                            Header = new MessageHeader
                            {
                                MessageType = "complete_reply"
                            }
                        }
                        .AsReplyTo(message)
                    );
                }
                return;
            }
            catch (TaskCanceledException tce)
            {
                this.logger?.LogDebug(tce, "Task cancelled.");
                return;
            }
            catch (Exception e)
            {
                this.logger?.LogError(e, "Unable to process CompleteRequest.");
                return;
            }
            finally
            {
                shellServer.NotifyBusyStatus(message, ExecutionState.Idle);
            }
        }
    }
}