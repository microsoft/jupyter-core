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

    public class Runner : InteractiveRunner
    {
        public Runner(
            ScriptCompiler scriptCompiler,
            LogFactory logFactory,
            ScriptConsole console,
            string[] packageSources
        ) : base(scriptCompiler, logFactory, console, packageSources) {}
        public new Task Execute(string input)
        {
            return base.Execute(input);
        }
    }

    public class ChannelConsole : ScriptConsole
    {
        private IChannel channel = null;
        private StringWriter successWriter;
        public bool HadError { get; private set; } = false;
        public IChannel CurrentChannel {
            get
            { return channel; }
            
            set
            {
                channel = value;
                if (channel != null)
                {
                    successWriter = new StringWriter();
                    HadError = false;
                }
            }
        }
        public string Success => successWriter?.ToString();

        public ChannelConsole() : base(Console.Out, null, Console.Error) { }

        public override void WriteError(string value)
        {
            if (CurrentChannel != null)
            {
                HadError = true;
                CurrentChannel?.Stderr(value);
            }
            else
            {
                base.WriteError(value);
            }
        }
        public override void WriteNormal(string value) => CurrentChannel?.Stdout(value);
        public override void WriteSuccess(string value)
        {
            System.Console.WriteLine($"success: {value}");
            if (successWriter != null)
            {
                successWriter.Write(value);
            }
            else
            {
                base.WriteSuccess(value);
            }
        }

    }

    public class CSharpEngine : BaseEngine
    {

        private readonly Runner runner;
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
            // TODO: replace with something that can pipe to IChannels.
            // FIXME: this is copied from dotnet-script, and needs removed.
            console = new ChannelConsole();

            var runtimeDependencyResolver = new RuntimeDependencyResolver(factory, useRestoreCache: false);

            var compiler = new ScriptCompiler(factory, runtimeDependencyResolver);
            runner = new Runner(compiler, factory, console, Array.Empty<string>());
            
        }

        public override ExecutionResult ExecuteMundane(string input, IChannel channel)
        {
            var oldChannel = console.CurrentChannel;
            console.CurrentChannel = channel;
            // Get the return value by accessing the private script
            // state internal to dotnet-script.
            var field = typeof(InteractiveRunner).GetField("_scriptState", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
            var state = field.GetValue(runner) as ScriptState;
            try
            {
                runner.Execute(input).Wait();
                var result = state?.ReturnValue;
                System.Console.WriteLine($"??? {result} {console.HadError}");
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
