// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

// Ideally, end users should not need to directly know about the classes we
// use to serialize and de-serialize messages, so we guard those in a new
// namespace.
namespace Microsoft.Jupyter.Core.Protocol
{

    [JsonObject(MemberSerialization.OptIn)]
    public class MessageHeader
    {
        public MessageHeader()
        {
            ProtocolVersion = "5.2.0";
            Id = Guid.NewGuid().ToString();
            Username = Environment.UserName;
        }

        [JsonProperty("msg_id")]
        public string Id { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("session")]
        public string Session { get; set; }

        // NB: Not an enum here, since we MUST handle unknown message types
        //     gracefully as per the wire protocol.
        [JsonProperty("msg_type")]
        public string MessageType { get; set; }

        [JsonProperty("version")]
        public string ProtocolVersion { get; set; }

        // FIXME: Need to add ISO 8601 format date here.
    }

    public class Message
    {
        public List<byte[]> ZmqIdentities { get; set; }

        public byte[] Signature { get; set; }

        public MessageHeader Header { get; set; }

        // As per Jupyter's wire protocol, if messages occur in sequence,
        // each message will have the previous message's header in this field.
        public MessageHeader ParentHeader { get; set; }

        // FIXME: make not just an object.
        public object Metadata { get; set; }

        // FIXME: make not just an object.
        public MessageContent Content { get; set; }

        public Message WithSessionFrom(Message parent)
        {
            var reply = this.MemberwiseClone() as Message;
            reply.ZmqIdentities = parent.ZmqIdentities;
            reply.Header.Session = parent.Header.Session;
            return reply;
        }

        public Message AsReplyTo(Message parent)
        {
            // No parent, just return
            if (parent == null) return this;

            var reply = this.WithSessionFrom(parent);
            reply.ParentHeader = parent.Header;
            return reply;
        }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class MessageContent
    {
        public readonly static ImmutableDictionary<string, Func<string, MessageContent>> Deserializers =
            new Dictionary<string, Func<string, MessageContent>>
            {
                ["kernel_info_request"] = data => new EmptyContent(),
                ["execute_request"] = data => JsonConvert.DeserializeObject<ExecuteRequestContent>(data),
                ["interrupt_request"] = data => new EmptyContent(),
                ["shutdown_request"] = data => JsonConvert.DeserializeObject<ShutdownRequestContent>(data),
                ["comm_open"] = data => JsonConvert.DeserializeObject<CommOpenContent>(data),
                ["comm_msg"] = data => JsonConvert.DeserializeObject<CommMessageContent>(data),
                ["comm_close"] = data => JsonConvert.DeserializeObject<CommCloseContent>(data)
            }.ToImmutableDictionary();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class EmptyContent : MessageContent
    {
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class UnknownContent : MessageContent
    {
        public Dictionary<string, object> Data
        {
            get
            {
                return RawData.ToObject<Dictionary<string, object>>();
            }

            set
            {
                RawData = JToken.FromObject(value);
            }
        }

        public JToken RawData { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class KernelInfoReplyContent : MessageContent
    {
        [JsonProperty("protocol_version")]
        public string ProtocolVersion { get => "5.2.0"; }

        [JsonProperty("implementation")]
        public string Implementation { get; set; }

        [JsonProperty("implementation_version")]
        public string ImplementationVersion { get; set; }

        [JsonProperty("language_info")]
        public LanguageInfo LanguageInfo { get; set; }

        [JsonProperty("banner")]
        public string Banner { get; set; }

        [JsonProperty("help_links")]
        public HelpLinks[] HelpLinks { get; set; }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ShutdownRequestContent : MessageContent
    {
        [JsonProperty("restart")]
        public bool Restart { get; set; }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class ExecuteRequestContent : MessageContent
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("silent")]
        public bool Silent { get; set; }

        [JsonProperty("store_history")]
        public bool StoreHistory { get; set; }

        [JsonProperty("user_expressions")]
        public object UserExpressions { get; set; }

        [JsonProperty("allow_stdin")]
        public bool AllowStandardIn { get; set; }

        [JsonProperty("stop_on_error")]
        public bool StopOnError { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ExecuteResultContent : MessageContent
    {
        [JsonProperty("execution_count")]
        public int ExecutionCount { get; set; }

        [JsonProperty("data")]
        public Dictionary<string, string> Data { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ExecuteInputContent : MessageContent
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("execution_count")]
        public int ExecutionCount { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ExecuteReplyContent : MessageContent
    {
        [JsonProperty("status")]
        public ExecuteStatus ExecuteStatus { get; set; }

        [JsonProperty("execution_count")]
        public int? ExecutionCount { get; set; }

        // Don't use this! It's deprecated at the protocol level.
        [JsonProperty("payload")]
        public List<Dictionary<string, object>> Payloads { get; set; } = new List<Dictionary<string, object>>();

        [JsonProperty("user_expressions")]
        public Dictionary<string, object> UserExpressions { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class KernelStatusContent : MessageContent
    {
        [JsonProperty("execution_state")]
        public ExecutionState ExecutionState { get; set; }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class StreamContent : MessageContent
    {
        [JsonProperty("name")]
        public StreamName StreamName { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class TransientDisplayData
    {
        [JsonProperty("display_id")]
        public string DisplayId { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class DisplayDataContent : MessageContent
    {
        [JsonProperty("data")]
        public Dictionary<string, string> Data { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

        [JsonProperty("transient", NullValueHandling=NullValueHandling.Ignore)]
        public TransientDisplayData Transient { get; set; } = null;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CommOpenContent : MessageContent
    {
        [JsonProperty("comm_id")]
        public string Id { get; set; }

        [JsonProperty("target_name")]
        public string TargetName { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public JToken RawData { get; set; } = null;
    }

    [JsonObject]
    public class CommMessageContent : MessageContent
    {
        [JsonProperty("comm_id")]
        public string Id { get; set; }

        [JsonProperty("data")]
        public JToken RawData { get; set; } = null;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class CommCloseContent : MessageContent
    {
        [JsonProperty("comm_id")]
        public string Id { get; set; }

        [JsonProperty("data")]
        public JToken RawData { get; set; } = null;
    }

}
