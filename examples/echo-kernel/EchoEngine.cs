// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Microsoft.Jupyter.Core
{

    public class EchoEngine : BaseEngine
    {
        public EchoEngine(
            IShellServer shell,
            IOptions<KernelContext> context,
            ILogger<EchoEngine> logger
        ) : base(shell, context, logger) { }

        public override ExecutionResult ExecuteMundane(string input, IChannel channel) =>
            (Program.ShoutOption.HasValue() ? input.ToUpper() : input).ToExecutionResult();

        [MagicCommand(
            "%tick",
            "Writes some ticks to demonstrate updatable display data."
        )]
        public ExecutionResult ExecuteTick(string code, IChannel channel)
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
