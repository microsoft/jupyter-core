// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Dotnet.Script.Core;
using Dotnet.Script.DependencyModel.Runtime;
using Dotnet.Script.DependencyModel.Logging;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;

namespace Microsoft.Jupyter.Core
{


    public class CSharpEngine : BaseEngine
    {

        private readonly InteractiveRunner runner;
        private readonly ChannelConsole console;

        private LogFactory factory =>
            type =>
            (level, msg, ex) =>
            Logger.LogDebug(msg);

        public CSharpEngine(
            IShellServer shell,
            IOptions<KernelContext> context,
            ILogger<CSharpEngine> logger
        ) : base(shell, context, logger)
        {
            // FIXME: this is copied from dotnet-script, and needs removed.
            console = new ChannelConsole();
            var compiler = new ScriptCompiler(factory, false);
            runner = new InteractiveRunner(compiler, factory, console, Array.Empty<string>());

        }

        public override ExecutionResult ExecuteMundane(string input, IChannel channel)
        {
            var oldChannel = console.CurrentChannel;
            console.CurrentChannel = channel;
            try
            {
                var task = runner.Execute(input);
                var result = task.Result;
                if (!console.HadError && result != null)
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
                console.CurrentChannel = oldChannel;
            }
        }
    }
}
