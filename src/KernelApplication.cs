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

            Name = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
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
                if (assembly.GetManifestResourceInfo(resourcePath) == null) {
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
                    cmd.Description = $"Installs the {properties.FriendlyName} ({properties.KernelName}) kernel into Jupyter.";
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
                    cmd.Description = $"Runs the {properties.FriendlyName} kernel. Typically only run by a Jupyter client.";
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
            try {
                func();
                return 0;
            } catch (Exception ex) {
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
                    $"NOTE: Installing a kernel spec which references this directory.\n" +
                    $"      Any changes made in this directory will affect the operation of the {properties.FriendlyName} kernel.\n" +
                    $"      If this was not what you intended, run 'install' without the '--develop' option."
                );

                // Serialize a new kernel spec that points to this directory.
                kernelSpec = new KernelSpec
                {
                    DisplayName = properties.FriendlyName,
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
                    if (System.Diagnostics.Process.GetCurrentProcess().ProcessName == "dotnet")
                    {
                        kernelArgs.AddRange(new[] { "dotnet", properties.KernelName });
                    }
                    else
                    {
                        kernelArgs.Add(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
                    }
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
                    DisplayName = properties.FriendlyName,
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
            if (additionalFiles != null) {
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

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "jupyter",
                Arguments = $"kernelspec install {kernelSpecDir} --name=\"{properties.KernelName}\" {String.Join(" ", extraArgs)}"
            });
            process.WaitForExit();
            foreach (var fileName in filesToDelete)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch {}
            }
            Directory.Delete(tempKernelSpecDir);
            return process.ExitCode;
        }


        /// <summary>
        ///     Launches a new kernel instance, loading all relevant connection
        ///     parameters from the given connection file as provided by
        ///     Jupyter.
        /// </summary>
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
