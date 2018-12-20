// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{
    public class KernelContext
    {
        public ConnectionInfo ConnectionInfo { get; private set; }
        public KernelProperties Properties { get; set; }

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