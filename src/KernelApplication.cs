// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Newtonsoft.Json;
using McMaster.Extensions.CommandLineUtils;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Jupyter.Core
{
    public class KernelApplication : CommandLineApplication
    {
        private readonly KernelProperties properties;

        private readonly Action<ServiceCollection> configure;

        public KernelApplication(KernelProperties properties, Action<ServiceCollection> configure)
        {
            this.properties = properties;
            this.configure = configure;

            Name = $"dotnet {properties.KernelName}";
            Description = properties.Description;
            this.HelpOption();
            this.VersionOption(
                "--version",
                () => properties.KernelVersion,
                () =>
                    $"Language kernel: {properties.KernelVersion}\n" +
                    $"Jupyter core: {typeof(KernelApplication).Assembly.GetName().Version}"
            );
        }

        public KernelApplication WithDefaultCommands() => this
            .AddInstallCommand()
            .AddKernelCommand();

        public KernelApplication AddInstallCommand()
        {
            this.Command(
                "install",
                cmd =>
                {
                    cmd.HelpOption();
                    cmd.Description = $"Installs the {properties.KernelName} kernel into Jupyter.";
                    var developOpt = cmd.Option(
                        "--develop",
                        "Installs a kernel spec that runs against this working directory. Useful for development only.",
                        CommandOptionType.NoValue
                    );
                    var logLevelOpt = cmd.Option<LogLevel>(
                        "-l|--log-level <LEVEL>",
                        "Level of logging messages to emit to the console. On development mode, defaults to Information.",
                        CommandOptionType.SingleValue
                    );
                    cmd.OnExecute(() =>
                    {
                        var develop = developOpt.HasValue();
                        var logLevel =
                            logLevelOpt.HasValue()
                            ? logLevelOpt.ParsedValue
                            : (develop ? LogLevel.Information : LogLevel.Error);
                        return ReturnExitCode(() => InstallKernelSpec(develop, logLevel));
                    });
                }
            );

            return this;
        }

        public KernelApplication AddKernelCommand()
        {
            this.Command(
                "kernel",
                cmd =>
                {
                    cmd.HelpOption();
                    cmd.Description = $"Runs the {properties.KernelName} kernel. Typically only run by a Jupyter client.";
                    var connectionFileArg = cmd.Argument(
                        "connection-file", "Connection file used to connect to a Jupyter client."
                    );
                    var logLevelOpt = cmd.Option<LogLevel>(
                        "-l|--log-level <LEVEL>",
                        "Level of logging messages to emit to the console. Defaults to Error.", 
                        CommandOptionType.SingleValue
                    );
                    cmd.OnExecute(() =>
                    {
                        var connectionFile = connectionFileArg.Value;
                        var logLevel =
                            logLevelOpt.HasValue()
                            ? logLevelOpt.ParsedValue
                            : LogLevel.Error;

                        return ReturnExitCode(() => StartKernel(connectionFile, logLevel));
                    });
                }
            );

            return this;
        }

        public int ReturnExitCode(Action func)
        {
            try {
                func();
                return 0;
            } catch (Exception ex) {
                System.Console.Error.WriteLine(ex.Message);
                return -1;
            }
        }

        public int InstallKernelSpec(bool develop, LogLevel logLevel)
        {
            var kernelSpecDir = "";
            KernelSpec kernelSpec;
            if (develop)
            {
                System.Console.WriteLine(
                    "NOTE: Installing a kernel spec which references this directory.\n" +
                    $"      Any changes made in this directory will affect the operation of the {properties.FriendlyName} kernel.\n" +
                    "      If this was not what you intended, run 'dotnet " +
                              $"{properties.KernelName} install' without the '--develop' option."
                );

                // Serialize a new kernel spec that points to this directory.
                kernelSpec = new KernelSpec
                {
                    DisplayName = properties.KernelName,
                    LanguageName = properties.LanguageName,
                    Arguments = new List<string> {
                        "dotnet", "run",
                        "--project", Directory.GetCurrentDirectory(),
                        "--", "kernel",
                        "--log-level", logLevel.ToString(),
                        "{connection_file}"
                    }
                };
            }
            else
            {
                kernelSpec = new KernelSpec
                {
                    DisplayName = properties.DisplayName,
                    LanguageName = properties.LanguageName,
                    Arguments = new List<string>
                    {
                        "dotnet", properties.KernelName,
                        "kernel",
                        "--log-level", logLevel.ToString(),
                        "{connection_file}"
                    }
                };
            }

            // Make a temporary directory to hold the kernel spec.
            var tempKernelSpecDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var jsonPath = Path.Combine(tempKernelSpecDir, "kernel.json");
            Directory.CreateDirectory(tempKernelSpecDir);
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(kernelSpec));
            kernelSpecDir = tempKernelSpecDir;

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "jupyter",
                Arguments = $"kernelspec install {kernelSpecDir} --name=\"{properties.KernelName}\""
            });
            process.WaitForExit();
            File.Delete(jsonPath);
            Directory.Delete(tempKernelSpecDir);
            return process.ExitCode;
        }


        public int StartKernel(string connectionFile, LogLevel minLevel = LogLevel.Debug)
        {
            // Begin by setting up the dependency injection that we will need
            // in order to configure logging in a fashion that is idiomatic to
            // .NET Core.
            var serviceCollection = new ServiceCollection();
            serviceCollection
                // For now, we add a logger that reports to the console.
                // TODO: add a logger that reports back to the client.
                .AddLogging(configure => configure.AddConsole())
                .Configure<LoggerFilterOptions>(
                    options => options.MinLevel = minLevel
                )

                // We need to pass along the context to each server, including
                // information gleaned from the connection file and from user
                // preferences.
                .Configure<KernelContext>(
                    ctx =>
                    {
                        ctx.LoadConnectionFile(connectionFile);
                        ctx.Properties = properties;
                    }
                )

                // We want to make sure that we only ever start a single
                // copy of each listener.
                .AddSingleton<IHeartbeatServer, HeartbeatServer>()
                .AddSingleton<IShellServer, ShellServer>();

            // After setting up the service collection, we give the specific
            // kernel a chance to configure it. At a minimum, the specific kernel
            // must provide an IReplEngine.
            configure(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Minimally, we need to start a server for each of the heartbeat,
            // control and shell sockets.
            var logger = serviceProvider.GetService<ILogger<HeartbeatServer>>();
            var context = serviceProvider.GetService<KernelContext>();

            var heartbeatServer = serviceProvider.GetService<IHeartbeatServer>();
            var shellServer = serviceProvider.GetService<IShellServer>();
            var engine = serviceProvider.GetService<IExecutionEngine>();

            // We start by launching a heartbeat server, which echoes whatever
            // input it gets from the client. Clients can use this to ensure
            // that the kernel is still alive and responsive.
            engine.Start();
            heartbeatServer.Start();
            shellServer.Start();

            return 0;
        }
    }
}