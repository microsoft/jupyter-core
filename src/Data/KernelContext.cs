// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      Describes all information needed to identify a kernel at runtime,
    ///      including how to connect to clients, and a description of the
    ///      kernel's supported language.
    /// </summary>
    public class KernelContext
    {
        public ConnectionInfo ConnectionInfo { get; private set; }
        public KernelProperties Properties { get; set; }

        /// <summary>
        ///      Populates the context using a connection file, typically
        ///      provided by Jupyter when instantiating a kernel.
        /// </summary>
        /// <param name="connectionFile">
        ///     A path to the connection file to be loaded.
        /// </param>
        /// <param name="logger">
        ///     A logger object used to report debugging information from the
        ///     connection file.
        /// </param>
        public void LoadConnectionFile(string connectionFile, ILogger logger = null)
        {
            logger?.LogDebug("Loading kernel context from connection file: {connectionFile}.", connectionFile);
            this.ConnectionInfo = JsonConvert.DeserializeObject<ConnectionInfo>(File.ReadAllText(connectionFile));
            logger?.LogDebug("Loaded connection information:\n{connectionInfo}", this.ConnectionInfo);
        }

        internal HMAC NewHmac() {
            // TODO: ensure that this HMAC algorithm agrees with that specified
            //       in ConnectionInfo.
            return new HMACSHA256(Encoding.ASCII.GetBytes(ConnectionInfo.Key));
        }
    }

}