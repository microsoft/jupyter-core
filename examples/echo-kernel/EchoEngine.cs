// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Microsoft.Jupyter.Core
{

    public class EchoEngine : BaseEngine
    {
        public EchoEngine(
            IShellServer shell,
            IOptions<KernelContext> context,
            ILogger<EchoEngine> logger
        ) : base(shell, context, logger) {
            RegisterDefaultSerializers();
        }

        public override ExecutionResult ExecuteMundane(string input, Action<string> stdout, Action<string> stderr)
        {
            return input.ToExecutionResult();
        }
    }
}
