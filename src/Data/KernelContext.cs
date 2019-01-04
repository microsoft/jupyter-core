// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
        public void LoadConnectionFile(string connectionFile)
        {
            this.ConnectionInfo = JsonConvert.DeserializeObject<ConnectionInfo>(File.ReadAllText(connectionFile));
        }

        internal HMAC NewHmac() {
            // TODO: ensure that this HMAC algorithm agrees with that specified
            //       in ConnectionInfo.
            return new HMACSHA256(Encoding.ASCII.GetBytes(ConnectionInfo.Key));
        }
    }

}