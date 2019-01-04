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

namespace Microsoft.Jupyter.Core
{
    class Program
    {
        public static void Init(ServiceCollection serviceCollection) =>
            serviceCollection
                // Start a new service for the ReplEngine.
                .AddSingleton<IExecutionEngine, EchoEngine>();

        public static int Main(string[] args) {
            var app = new KernelApplication(
                PROPERTIES,
                Init
            );

            return app.WithDefaultCommands().Execute(args);
        }
    }
}
