// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.REPL;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{

    public class MoonEngine : BaseEngine
    {
        private ReplInterpreter interp;
        private Action<string> printFn = null;

        public MoonEngine(
            IShellServer shell,
            IOptions<KernelContext> context,
            ILogger<MoonEngine> logger
        ) : base(shell, context, logger)
        {
            RegisterDefaultSerializers();
            RegisterJsonSerializer(
                new DynValueConverter()
            );
            var script = new Script();
            script.Options.DebugPrint = str => printFn?.Invoke(str);
            interp = new ReplInterpreter(script);
        }

        public override ExecutionResult ExecuteMundane(string input, Action<string> stdout, Action<string> stderr)
        {
            var oldAction = printFn;
            printFn = stdout;
            try
            {
                var result = interp.Evaluate(input);
                if (result == null)
                {
                    stderr("Interpreter returned null, this is typically due to incomplete input.");
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
                stderr(ex.ToString());
                return ExecuteStatus.Error.ToExecutionResult();
            }
            finally
            {
                printFn = oldAction;
            }
        }
    }
}
