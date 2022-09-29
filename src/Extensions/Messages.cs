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
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{
    public static partial class Extensions
    {

        /// <summary>
        ///     Receives an entire Jupyter wire protocol message from a given
        ///     ZeroMQ socket and deserializes it to a Message object for
        ///     further processing.
        /// </summary>
        public static Message ReceiveMessage(
            this NetMQSocket socket,
            KernelContext context,
            Encoding encoding = null
        )
        {
            encoding = encoding ?? Encoding.UTF8;

            // Get all the relevant message frames.
            var rawFrames = socket
                .ReceiveMultipartBytes();
            var frames = rawFrames
                .Select(frame => encoding.GetString(frame))
                .ToList();

            // We know that one of the frames should be the special delimiter
            // <IDS|MSG>. If we don't find it, time to throw an exception.
            var idxDelimiter = frames.IndexOf("<IDS|MSG>");
            if (idxDelimiter < 0)
            {
                throw new ProtocolViolationException("Expected <IDS|MSG> delimiter, but none was present.");
            }

            // At this point, we know that everything before idxDelimter is
            // a ZMQ identity, and that everything after follows the Jupyter
            // wire protocol. In particular, the next five blobs after <IDS|MSG>
            // are as follows:
            //     • An HMAC signature for the entire message.
            //     • A serialized header for this message.
            //     • A serialized header for the previous message in sequence.
            //     • A serialized metadata dictionary.
            //     • A serialized content dictionary.
            // Any remaining blobs are extra raw data buffers.

            // We start by computing the digest, since that is much, much easier
            // to do given the raw frames than trying to unambiguously
            // reserialize everything.
            // To compute the digest and verify the message, we start by pulling
            // out the claimed signature. This is by default a string of
            // hexadecimal characters, so we convert to a byte[] for comparing
            // with the HMAC output.
            var signature = frames[idxDelimiter + 1].HexToBytes();
            // Next, we take the four frames after the <IDS|MSG> delimeter, since
            // those are the subject of the digest.
            var toDigest = rawFrames.Skip(idxDelimiter + 2).Take(4).ToArray();
            var digest = context.NewHmac().ComputeHash(toDigest);

            if (!signature.IsEqual(digest))
            {
                var digestStr = Convert.ToBase64String(digest);
                var signatureStr = Convert.ToBase64String(signature);
                throw new ProtocolViolationException(
                    $"HMAC {digestStr} did not agree with {signatureStr}.");
            }

            // If we made it this far, we can unpack the content of the message
            // into the right subclass of MessageContent.
            var header = JsonConvert.DeserializeObject<MessageHeader>(frames[idxDelimiter + 2]);
            var content = MessageContent.Deserializers.GetValueOrDefault(
                header.MessageType,
                data =>
                    new UnknownContent
                    {
                        RawData = JToken.Parse(data)
                    }
            )(frames[idxDelimiter + 5]);

            var message = new Message
            {
                ZmqIdentities = rawFrames.Take(idxDelimiter).ToList(),
                Signature = signature,
                Header = header,
                ParentHeader = JsonConvert.DeserializeObject<MessageHeader>(frames[idxDelimiter + 3]),
                Metadata = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(frames[idxDelimiter + 4]),
                Content = content
            };

            return message;
        }

        public static void SendMessage(this IOutgoingSocket socket, KernelContext context, Message message)
        {
            // FIXME: need to handle parents for messages which are handled
            //        sequentially.

            // Conceptually, sending a message consists of three steps:
            //     • Convert the message to four frames.
            //     • Digest the four frames.
            //     • Send the identities, the delimeter, the digest, and the
            //       message frames.

            var zmqMessage = new NetMQMessage();
            var frames = new object[]
                {
                    message.Header,
                    message.ParentHeader,
                    message.Metadata,
                    message.Content
                }
                .Select(frame => JsonConvert.SerializeObject(frame))
                .Select(str => Encoding.UTF8.GetBytes(str))
                .ToList();
            var digest = context.NewHmac().ComputeHash(frames.ToArray());

            message.ZmqIdentities?.ForEach(ident => zmqMessage.Append(ident));
            zmqMessage.Append("<IDS|MSG>");
            zmqMessage.Append(BitConverter.ToString(digest).Replace("-", "").ToLowerInvariant());
            frames.ForEach(ident => zmqMessage.Append(ident));

            socket.SendMultipartMessage(zmqMessage);
        }
    }

}