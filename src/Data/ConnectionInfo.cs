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
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///     Represents the information stored in a Jupyter connection file.
    ///     See https://jupyter-client.readthedocs.io/en/stable/kernels.html#connection-files
    ///     for details.
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ConnectionInfo
    {
        // As per documentation at https://jupyter-client.readthedocs.io/en/stable/kernels.html#connection-files,
        // an example connection file looks like the following:
        //
        // {
        //     "control_port": 50160,
        //     "shell_port": 57503,
        //     "transport": "tcp",
        //     "signature_scheme": "hmac-sha256",
        //     "stdin_port": 52597,
        //     "hb_port": 42540,
        //     "ip": "127.0.0.1",
        //     "iopub_port": 40885,
        //     "key": "a0436f6c-1916-498b-8eb9-e81ab9368e84"
        // }

        #region Port Information

        [JsonProperty("control_port")]
        public int ControlPort { get; set; }

        [JsonProperty("shell_port")]
        public int ShellPort { get; set; }

        [JsonProperty("hb_port")]
        public int HeartbeatPort { get; set; }

        [JsonProperty("iopub_port")]
        public int IoPubPort { get; set; }

        [JsonProperty("stdin_port")]
        public int StdInPort { get; set; }

        #endregion

        #region Transport Information

        [JsonProperty("transport")]
        public Transport Transport { get; set; }

        [JsonProperty("ip")]
        [JsonConverter(typeof(IpAddressConverter))]
        public IPAddress IpAddress { get; set; }

        #endregion

        #region Authentication Information

        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("signature_scheme")]
        public SignatureScheme SignatureScheme { get; set; }

        #endregion

        #region Metadata

        [JsonProperty("kernel_name")]
        public string KernelName { get; set; }

        #endregion

        #region ZeroMQ Configuration
        // ZeroMQ uses a notion of "address" that combines both IP addresses
        // and ports, similar to a URL. Thus, we put some logic here to
        // construct ZeroMQ addresses from the rest of the connection info.

        // FIXME: consolidate address logic here.
        public string HeartbeatZmqAddress {
            get {
                var protocol = Enum.GetName(typeof(Transport), Transport).ToLower();
                return $"{protocol}://{IpAddress}:{HeartbeatPort}";
            }
        }

        public string ShellZmqAddress {
            get {
                var protocol = Enum.GetName(typeof(Transport), Transport).ToLower();
                return $"{protocol}://{IpAddress}:{ShellPort}";
            }
        }

        public string ControlZmqAddress {
            get {
                var protocol = Enum.GetName(typeof(Transport), Transport).ToLower();
                return $"{protocol}://{IpAddress}:{ControlPort}";
            }
        }

        public string IoPubZmqAddress {
            get {
                var protocol = Enum.GetName(typeof(Transport), Transport).ToLower();
                return $"{protocol}://{IpAddress}:{IoPubPort}";
            }
        }
        #endregion

        #region Diagnostic Support

        public override string ToString() =>
            JsonConvert.SerializeObject(this, Formatting.Indented);

        #endregion

    }
}
