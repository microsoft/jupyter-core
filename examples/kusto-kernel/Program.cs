using System;
using System.IO;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using static Microsoft.Jupyter.Core.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Jupyter.Core
{
    class Program
    {
        public static void Init(ServiceCollection serviceCollection) =>
            serviceCollection
                // We begin by adding a configuration class that will let
                // us do things like add authentication credentials.
                .AddSingleton<AppSettings>()

                // Start a new service for the ReplEngine.
                .AddSingleton<IExecutionEngine, KustoEngine>();

        public static int Main(string[] args) {
            var app = new KernelApplication(
                PROPERTIES,
                Init
            );

            return app.WithDefaultCommands().Execute(args);
        }
    }
}
