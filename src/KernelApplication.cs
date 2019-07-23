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
using System.Reflection;
using System.Linq;
using Microsoft.Jupyter.Core.Protocol;
using System.ComponentModel;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      The main application for Jupyter kernels, used both to install
    ///      kernelspecs into Jupyter and to start new kernel instances.
    /// </summary>
    public class KernelApplication : CommandLineApplication
    {
        private readonly KernelProperties properties;
        private readonly IDictionary<string, Func<Stream>> additionalFiles = new Dictionary<string, Func<Stream>>();
        private IList<Func<IEnumerable<string>>> additionalKernelArgumentSources
            = new List<Func<IEnumerable<string>>>();

        private readonly Action<ServiceCollection> configure;

        /// <summary>
        /// This event is called when the Kernel starts. It passes down the SerivceProvider collection
        /// with all the current services used in dependency injection.
        /// </summary>
        public event Action<ServiceProvider> KernelStarted;

        /// <summary>
        /// This event is called when the Kernel stops.
        /// </summary>
        public event Action KernelStopped;

        /// <summary>
        ///     Constructs a new application given properties describing a
        ///     particular kernel, and an action to configure services.
        /// </summary>
        /// <param name="properties">
        ///     Properties describing this kernel to clients.
        /// </param>
        /// <param name="configure">
        ///     An action to configure services for the new kernel application.
        ///     This action is called after all other kernel services have been
        ///     configured, and is typically used to provide an implementation
        ///     of <see cref="IExecutionEngine" /> along with any services
        ///     required by that engine.
        /// </param>
        /// <example>
        ///     To instantiate and run a kernel application using the
        ///     <c>EchoEngine</c> class:
        ///     <code>
        ///         public static int Main(string[] args) =>
        ///             new KernelApplication(
        ///                 properties,
        ///                 serviceCollection =>
        ///                      serviceCollection
        ///                     .AddSingleton&lt;IExecutionEngine, EchoEngine&gt;();
        ///             )
        ///             .WithDefaultCommands()
        ///             .Execute(args);
        ///         }
        ///     </code>
        /// </example>
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
                () => String.Join("\n",
                    properties
                    .VersionTable
                    .Select(row => $"{row.Item1}: {row.Item2}")
                )
            );
        }

        /// <summary>
        ///      <para>
        ///          Adds all default commands to this kernel application
        ///          (installation and kernel instantiation).
        ///      </para>
        ///      <seealso cref="AddInstallCommand" />
        ///      <seealso cref="AddKernelCommand" />
        /// </summary>
        public KernelApplication WithDefaultCommands() => this
            .AddInstallCommand()
            .AddKernelCommand();

        /// <summary>
        ///     Adds the given resources files as additional kernelspec files.
        /// </summary>
        /// <param name="resources">
        ///      A dictionary from kernelspec file names to the embedded resource
        ///      paths which should be copied to each kernelspec file.
        /// </param>
        /// <typeparam>
        ///      A type in the assembly that should be used to look up resource
        ///      files. Typically, this will be the main static program class
        ///      used to run each kernel.
        /// </typeparam>
        public KernelApplication WithKernelSpecResources<TProgram>(IDictionary<string, string> resources)
        {
            var assembly = typeof(TProgram).Assembly;
            foreach (var (name, resourcePath) in resources)
            {
                if (assembly.GetManifestResourceInfo(resourcePath) == null)
                {
                    throw new IOException($"Kernelspec resource {name} not found at {resourcePath}.");
                }

                additionalFiles[name] = () => assembly.GetManifestResourceStream(resourcePath);
            }

            return this;
        }

        /// <summary>
        ///      Adds arguments that should be passed to the kernel when invoked
        ///      by jupyter. These arguments will be written to` the kernelspec
        ///      for the kernel.
        /// </summary>
        public KernelApplication WithKernelArguments(Func<IEnumerable<string>> arguments)
        {
            this.additionalKernelArgumentSources.Add(arguments);
            return this;
        }

        /// <summary>
        ///     Adds a command to allow users to install this kernel into
        ///     Jupyter's list of available kernels.
        /// </summary>
        /// <remarks>
        ///     This command assumes that the command <c>jupyter</c> is on the
        ///     user's <c>PATH</c>.
        /// </remarks>
        public KernelApplication AddInstallCommand(Action<CommandLineApplication> configure = null)
        {
            var installCmd = this.Command(
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
                    var userOpt = cmd.Option(
                        "--user",
                        "Installs the kernel for the current user only.",
                        CommandOptionType.NoValue
                    );
                    var logLevelOpt = cmd.Option<LogLevel>(
                        "-l|--log-level <LEVEL>",
                        "Level of logging messages to emit to the console. On development mode, defaults to Information.",
                        CommandOptionType.SingleValue
                    );
                    var prefixOpt = cmd.Option<string>(
                        "--prefix <PREFIX>",
                        "Prefix to use when installing the kernel into Jupyter. See `jupyter kernelspec install --help` for details.",
                        CommandOptionType.SingleValue
                    );
                    var toolPathOpt = cmd.Option<string>(
                        "--path-to-tool <PATH>",
                        "Specified an explicit path to the kernel tool being installed, rather than using the .NET command. " +
                        "This option is incompatible with --develop, and isn't typically needed except in CI builds or other automated environments.",
                        CommandOptionType.SingleValue
                    );
                    cmd.OnExecute(() =>
                    {
                        var develop = developOpt.HasValue();
                        var logLevel =
                            logLevelOpt.HasValue()
                            ? logLevelOpt.ParsedValue
                            : (develop ? LogLevel.Information : LogLevel.Error);
                        var prefix = prefixOpt.HasValue() ? prefixOpt.Value() : null;
                        return ReturnExitCode(() => InstallKernelSpec(
                            develop, logLevel,
                            prefix: prefix,
                            user: userOpt.HasValue(),
                            additionalFiles: additionalFiles,
                            additionalKernelArguments:
                                additionalKernelArgumentSources
                                .SelectMany(source => source()),
                            pathToTool:
                                toolPathOpt.HasValue()
                                ? toolPathOpt.ParsedValue
                                : null
                        ));
                    });
                }
            );
            if (configure != null) { configure(installCmd); }

            return this;
        }

        /// <summary>
        ///     Adds a command to allow Jupyter to start instances of this
        ///     kernel.
        /// </summary>
        /// <remarks>
        ///     This command is typically not run by end users directly, but
        ///     by Jupyter on the user's behalf.
        /// </remarks>
        public KernelApplication AddKernelCommand(Action<CommandLineApplication> configure = null)
        {
            var kernelCmd = this.Command(
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

                        return ReturnExitCode(() => Run(connectionFile, logLevel));
                    });
                }
            );
            if (configure != null) { configure(kernelCmd); }

            return this;
        }

        /// <summary>
        ///      Given an action, runs the action and then returns with either
        ///      0 or a negative error code, depending on whether the action
        ///      completed successfully or threw an exception.
        /// </summary>
        /// <param name="func">An action to be run.</param>
        /// <returns>
        ///     Either <c>0</c> if <c>func</c> completed successfully
        ///     or <c>-1</c> if <c>func</c> threw an exception.
        /// </returns>
        public int ReturnExitCode(Action func)
        {
            try
            {
                func();
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine(ex.Message);
                return -1;
            }
        }

        /// <summary>
        ///      Installs this kernel into Jupyter's list of available kernels.
        /// </summary>
        /// <param name="develop">
        ///      If <c>true</c>, this kernel will be installed in develop mode,
        ///      such that the kernel is rebuilt whenever a new instance is
        ///      started.
        /// </param>
        /// <param name="logLevel">
        ///      The default logging level to be used when starting new kernel
        ///      instances.
        /// </param>
        /// <param name="prefix">
        ///      A path to be provided to <c>jupyter kernelspec install</c>
        ///      as the prefix into which the kernel should be installed.
        ///      Typically, this parameter is used when installing into an environment.
        ///      If <c>null</c>, no prefix is passed to Jupyter.
        /// </param>
        /// <param name="user">
        ///      If <c>true</c>, the kernel will be installed for the current
        ///      user only.
        /// </param>
        /// <param name="additionalFiles">
        ///      Specifies additional files which should be included in the kernelspec
        ///      directory. Files are specified as a dictionary from file names
        ///      to functions yielding streams that read the contents of each
        ///      file.
        /// </param>
        /// <param name="pathToTool">
        ///      If present, the value of this parameter will be used in the
        ///      kernelspec as an explicit path to the kernel being invoked,
        ///      as opposed to using the dotnet command-line program to find
        ///      the appropriate kernel.
        ///      This is not needed in most circumstances, but can be helpful
        ///      when working with CI environments that do not add .NET Global
        ///      Tools to the PATH environment variable.
        /// </param>
        /// <param name="additionalKernelArguments">
        ///     Specifies additional parameters that should be included to
        ///     the command that starts the kernel.
        /// </param>
        /// <remarks>
        ///      This method dynamically generates a new <c>kernelspec.json</c>
        ///      file representing the kernel properties provided when the
        ///      application was constructed, along with options such as the
        ///      development mode.
        /// </remarks>
        public int InstallKernelSpec(bool develop,
                                     LogLevel logLevel,
                                     string prefix = null, bool user = false,
                                     IDictionary<string, Func<Stream>> additionalFiles = null,
                                     IEnumerable<string> additionalKernelArguments = null,
                                     string pathToTool = null)
        {
            var kernelSpecDir = "";
            KernelSpec kernelSpec;
            if (develop)
            {
                if (pathToTool != null)
                {
                    throw new InvalidDataException("Cannot use development mode together with custom tool paths.");
                }
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
                var kernelArgs = new List<string>();
                if (pathToTool != null)
                {
                    kernelArgs.Add(pathToTool);
                }
                else
                {
                    kernelArgs.AddRange(new[] { "dotnet", properties.KernelName });
                }

                kernelArgs.AddRange(
                    new[]
                    {
                        "kernel",
                        "--log-level", logLevel.ToString(),
                        "{connection_file}"
                    }
                );

                kernelSpec = new KernelSpec
                {
                    DisplayName = properties.DisplayName,
                    LanguageName = properties.LanguageName,
                    Arguments = kernelArgs
                };
            }

            // Add any additional arguments to the kernel spec as needed.
            if (additionalKernelArguments != null)
            {
                kernelSpec.Arguments.AddRange(additionalKernelArguments);
            }

            // Make a temporary directory to hold the kernel spec.
            var tempKernelSpecDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var filesToDelete = new List<string>();
            var jsonPath = Path.Combine(tempKernelSpecDir, "kernel.json");
            Directory.CreateDirectory(tempKernelSpecDir);
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(kernelSpec));
            filesToDelete.Add(jsonPath);
            kernelSpecDir = tempKernelSpecDir;

            // Add any additional files we may need.
            if (additionalFiles != null)
            {
                foreach (var (fileName, streamAction) in additionalFiles)
                {
                    var dest = Path.Combine(tempKernelSpecDir, fileName);
                    var sourceStream = streamAction();
                    using (var destStream = File.OpenWrite(dest))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                    filesToDelete.Add(dest);
                }
            }

            // Find out if we need any extra arguments.
            var extraArgs = new List<string>();
            if (!String.IsNullOrWhiteSpace(prefix)) { extraArgs.Add($"--prefix=\"{prefix}\""); }
            if (user) { extraArgs.Add("--user"); }

            Process process = null;
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = "jupyter",
                    Arguments = $"kernelspec install {kernelSpecDir} --name=\"{properties.KernelName}\" {String.Join(" ", extraArgs)}"
                });
            }
            catch (Win32Exception ex)
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                if (ex.NativeErrorCode == 2)
                {
                    System.Console.Error.WriteLine(
                        "[ERROR] " +
                        $"Could not install {properties.KernelName} into your Jupyter configuration, " +
                        "as `jupyter` was not found on your PATH. " +
                        "Please make sure that Jupyter is installed and is on your PATH. " +
                        "If you are using conda or venv, please " +
                        "make sure that you have the correct environment activated.\n"
                    );
                }
                else
                {
                    System.Console.Error.WriteLine(
                        "[ERROR] " +
                        $"An exception occured while trying to call `jupyter` to install {properties.KernelName} " +
                        "into your Jupyter configuration.\n"
                    );
                }
                System.Console.ResetColor();
                System.Console.Error.WriteLine(
                    "Full exception details:\n" + ex.ToString()
                );
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(ex);
                return -2;
            }

            process?.WaitForExit();

            foreach (var fileName in filesToDelete)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch { }
            }
            Directory.Delete(tempKernelSpecDir);
            return process?.ExitCode ?? -1;
        }


        /// <summary>
        ///     Main execution entry point. Creates the service collection and
        ///     configures the kernel for execution based on the given connection file as provided by
        ///     Jupyter. 
        ///     Once services are created and configured, it calls StartKernel to start execution.
        /// </summary>
        public virtual int Run(string connectionFile, LogLevel minLevel = LogLevel.Debug)
        {
            var serviceCollection = InitServiceCollection(connectionFile, minLevel);
            var serviceProvider = InitServiceProvider(serviceCollection);

            return StartKernel(serviceProvider);
        }

        /// <summary>
        /// Creates and sets up the default configuration of the ServiceCollection.
        /// Creates a ServiceCollection instance and then calls calls ConfigureServiceCollection 
        /// to provide logging and others configuration and to add the internal servers needed for execution
        /// like Heartbeat and Shell. Finally it calls the configuration method provided
        /// during constructor to give opportunity to third parties to provide their own services and configuration.
        /// </summary>
        public virtual IServiceCollection InitServiceCollection(string connectionFile, LogLevel minLevel = LogLevel.Debug)
        {
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

                // Add the Shell and Heartbeat servers:
                .AddKernelServers();

            configure(serviceCollection);

            return serviceCollection;
        }

        /// <summary>
        /// Calls serviceCollection.BuildServiceProvider(). Provides KernelApplications the ability
        /// to perform their own actions during initialization.
        /// </summary>
        public virtual ServiceProvider InitServiceProvider(IServiceCollection serviceCollection) =>
            serviceCollection.BuildServiceProvider();

        /// <summary>
        /// This method is called to start the servers and the execution engine
        /// and the mail execution loop.
        /// </summary>
        public virtual int StartKernel(ServiceProvider serviceProvider)
        {
            // Minimally, we need to start a server for each of the heartbeat,
            // control and shell sockets.
            var heartbeatServer = serviceProvider.GetService<IHeartbeatServer>();
            var shellServer = serviceProvider.GetService<IShellServer>();
            var engine = serviceProvider.GetService<IExecutionEngine>();

            shellServer.ShutdownRequest += OnShutdownRequest;

            // We start by launching a heartbeat server, which echoes whatever
            // input it gets from the client. Clients can use this to ensure
            // that the kernel is still alive and responsive.
            engine.Start();
            heartbeatServer.Start();
            shellServer.Start();

            KernelStarted?.Invoke(serviceProvider);

            return 0;
        }

        private void OnShutdownRequest(Message obj)
        {
            KernelStopped?.Invoke();
        }
    }
}
