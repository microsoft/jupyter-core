// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Linq;
using NetMQ;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Cryptography;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Jupyter.Core
{
    public static partial class Extensions
    {
        /// <summary>
        /// Adds the core kernel servers (heartbeat and shell) to the service collection.
        /// </summary>
        public static IServiceCollection AddKernelServers(this IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddSingleton<IHeartbeatServer, HeartbeatServer>()
                .AddSingleton<IShellServer, ShellServer>();

            return serviceCollection;
        }
    }
}