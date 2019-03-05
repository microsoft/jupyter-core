// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using static Microsoft.Jupyter.Core.Constants;
using Microsoft.Extensions.DependencyInjection;
using McMaster.Extensions.CommandLineUtils;

namespace Microsoft.Jupyter.Core
{
    class Program
    {
        public static void Init(ServiceCollection serviceCollection) =>
            serviceCollection
                // Start a new service for the ReplEngine.
                .AddSingleton<IExecutionEngine, EchoEngine>();

        internal static CommandOption ShoutOption;

        public static int Main(string[] args) {
            var app = new KernelApplication(
                PROPERTIES,
                Init
            );
            CommandOption shoutInstallOption = null;

            return app
                .AddInstallCommand(
                    installCmd => {
                        shoutInstallOption = installCmd.Option(
                            "--shout",
                            "Shout back when echoing input.",
                            CommandOptionType.NoValue
                        );
                    }
                )
                .AddKernelCommand(
                    kernelCmd => {
                        ShoutOption = kernelCmd.Option(
                            "--shout",
                            "Shout back when echoing input.",
                            CommandOptionType.NoValue
                        );
                    }
                )
                .WithKernelArguments(
                    () => shoutInstallOption.HasValue() ? new[] {"--shout"} : new string[] {}
                )
                .WithKernelSpecResources<Program>(
                    new Dictionary<string, string>
                    {
                        ["logo-64x64.png"] = "echo-kernel.res.logo-64x64.png"
                    }
                )
                .Execute(args);
        }
    }
}
