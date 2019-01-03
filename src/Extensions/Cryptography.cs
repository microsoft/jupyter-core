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

namespace Microsoft.Jupyter.Core
{
    public static partial class Extensions
    {
        public static byte[] ComputeHash(this HMAC hmac, params byte[][] blobs)
        {
            hmac.Initialize();
            // TODO: generalize to allow encodings other than UTF-8.
            foreach (var blob in blobs.Take(blobs.Length - 1))
            {
                hmac.TransformBlock(blob, 0, blob.Length, null, 0);
            }
            var lastBlob = blobs[blobs.Length - 1];
            hmac.TransformFinalBlock(lastBlob, 0, lastBlob.Length);
            return hmac.Hash;
        }

        public static byte[] ComputeHash(this HMAC hmac, params object[] blobs)
        {
            return hmac.ComputeHash(
                blobs
                    .Select(blob => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(blob)))
                    .ToArray()
            );
        }

    }
}