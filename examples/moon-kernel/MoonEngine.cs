// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.REPL;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Jupyter.Core
{

    public class MoonEngine : BaseEngine
    {
        private ReplInterpreter interp;
        private Action<string> printFn = null;

        public MoonEngine(
            IShellServer shell,
            IShellRouter router,
            IOptions<KernelContext> context,
            ILogger<MoonEngine> logger,
            IServiceProvider serviceProvider
        ) : base(shell, router, context, logger, serviceProvider)
        {
            RegisterJsonEncoder(
                new DynValueConverter()
            );
            RegisterDisplayEncoder(
                MimeTypes.Markdown,
                displayable => {
                    if (displayable is IEnumerable<string> list)
                    {
                        return String.Join(
                            "\n",
                            list.Select(item => $"- {item}")
                        );
                    }
                    else return $"`{displayable}`";
                }
            );
            var script = new Script();
            script.Options.DebugPrint = str => printFn?.Invoke(str);
            interp = new ReplInterpreter(script);
        }

        public override async Task<ExecutionResult> ExecuteMundane(string input, IChannel channel, CancellationToken cancellationToken)
        {
            var oldAction = printFn;
            printFn = channel.Stdout;
            try
            {
                var result = interp.Evaluate(input);
                if (result == null)
                {
                    channel.Stderr("Interpreter returned null, this is typically due to incomplete input.");
                    return ExecuteStatus.Error.ToExecutionResult();
                }
                else if (result.ToObject() != null)
                {
                    return result.ToExecutionResult();
                }
                else
                {
                    return ExecuteStatus.Ok.ToExecutionResult();
                }
            }
            catch (Exception ex)
            {
                channel.Stderr(ex.ToString());
                return ExecuteStatus.Error.ToExecutionResult();
            }
            finally
            {
                printFn = oldAction;
            }
        }
    }
}
