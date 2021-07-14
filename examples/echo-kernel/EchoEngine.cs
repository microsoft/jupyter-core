// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Jupyter.Core
{

    public class EchoEngine : BaseEngine
    {
        public EchoEngine(
            IShellServer shell,
            IShellRouter router,
            IOptions<KernelContext> context,
            ILogger<EchoEngine> logger,
            IServiceProvider serviceProvider
        ) : base(shell, router, context, logger, serviceProvider) { }

        public override async Task<ExecutionResult> ExecuteMundane(string input, IChannel channel) =>
            (Program.ShoutOption.HasValue() ? input.ToUpper() : input).ToExecutionResult();

        public override async Task<IEnumerable<BaseEngine.Completion>> CompleteMundane(string code, int cursorPos)
        {
            Logger.LogInformation("Got completion request inside the echo kernel!");
            return new List<BaseEngine.Completion>
            {
                new Completion
                {
                    CursorStart = cursorPos,
                    CursorEnd = cursorPos,
                    Text = "foo"
                },
                new Completion
                {
                    CursorStart = cursorPos,
                    CursorEnd = cursorPos,
                    Text = "bar"
                }
            };
        }

        [MagicCommand(
            "%tick",
            "Writes some ticks to demonstrate updatable display data."
        )]
        public async Task<ExecutionResult> ExecuteTick(string code, IChannel channel)
        {
            var tickMessage = "Tick.";
            var updatable = channel.DisplayUpdatable(tickMessage);

            foreach (var idx in Enumerable.Range(0, 10))
            {
                tickMessage += ".";
                Thread.Sleep(1000);
                updatable.Update(tickMessage);
            }

            return ExecuteStatus.Ok.ToExecutionResult();
        }
    }
}
