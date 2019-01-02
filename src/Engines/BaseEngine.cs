// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MagicCommandAttribute : System.Attribute
    {
        public readonly string Name;

        public MagicCommandAttribute(string name)
        {
            Name = name;
        }
    }

    public abstract class BaseEngine : IExecutionEngine
    {

        private class ExecutionChannel : IChannel
        {
            private readonly Message parent;
            private readonly BaseEngine engine;
            public ExecutionChannel(BaseEngine engine, Message parent)
            {
                this.parent = parent;
                this.engine = engine;
            }

            public void Display(object displayable) =>
                engine.WriteDisplayData(parent, displayable);

            public void Stderr(string message) =>
                engine.WriteToStream(parent, StreamName.StandardError, message);

            public void Stdout(string message) =>
                engine.WriteToStream(parent, StreamName.StandardOut, message);
        }

        public int ExecutionCount { get; protected set; }
        protected List<string> History;
        private Dictionary<string, List<IResultEncoder>> serializers = new Dictionary<string, List<IResultEncoder>>();
        private readonly ImmutableDictionary<string, MethodInfo> magicMethods;

        public IShellServer ShellServer { get; }

        public KernelContext Context { get; }

        public ILogger Logger { get; }

        public BaseEngine(
                IShellServer shell,
                IOptions<KernelContext> context,
                ILogger logger)
        {
            ExecutionCount = 0;
            this.ShellServer = shell;
            this.Context = context.Value;
            this.Logger = logger;

            History = new List<string>();
            magicMethods = this
                .GetType()
                .GetMethods()
                .Where(
                    method => method.GetCustomAttributes(typeof(MagicCommandAttribute), inherit: true).Length > 0
                )
                .Select(
                    method => (
                        ((MagicCommandAttribute)method.GetCustomAttributes(typeof(MagicCommandAttribute), inherit: true).Single()).Name,
                        method
                    )
                )
                .ToImmutableDictionary(
                    pair => pair.Name,
                    pair => pair.method
                );

            RegisterDefaultEncoders();
        }

        public void RegisterDisplayEncoder(IResultEncoder serializer)
        {
            if (serializers.ContainsKey(serializer.MimeType))
            {
                this.serializers[serializer.MimeType].Add(serializer);
            }
            else
            {
                this.serializers[serializer.MimeType] = new List<IResultEncoder>
                {
                    serializer
                };
            }
        }

        public void RegisterDisplayEncoder(string mimeType, Func<object, EncodedData?> encoder) =>
            RegisterDisplayEncoder(new FuncResultEncoder(mimeType, encoder));

        public void RegisterDisplayEncoder(string mimeType, Func<object, string> encoder) =>
            RegisterDisplayEncoder(new FuncResultEncoder(mimeType, encoder));

        public void RegisterJsonEncoder(params JsonConverter[] converters) =>
            RegisterDisplayEncoder(new JsonResultEncoder(this.Logger, converters));

        public void RegisterDefaultEncoders()
        {
            RegisterDisplayEncoder(new PlainTextResultEncoder());
            RegisterDisplayEncoder(new ListToTextResultEncoder());
            RegisterDisplayEncoder(new ListToHtmlResultEncoder());
            RegisterDisplayEncoder(new TableToTextDisplayEncoder());
            RegisterDisplayEncoder(new TableToHtmlDisplayEncoder());
        }

        internal MimeBundle EncodeForDisplay(object displayable)
        {
            // Each serializer contributes what it can for a given object,
            // and we take the union of their contributions, with preference
            // given to the last serializers registered.
            var displayData = MimeBundle.Empty();
            foreach ((var mimeType, var encoders) in serializers)
            {
                foreach (var encoder in encoders)
                {
                    var encoded = encoder.Encode(displayable);
                    if (encoded == null)
                    {
                        continue;
                    }
                    else
                    {
                        displayData.Data[mimeType] = encoded.Value.Data;
                        if (encoded.Value.Metadata != null)
                        {
                            displayData.Metadata[mimeType] = encoded.Value.Metadata;
                        }
                        break;
                    }
                }
            }
            return displayData;
        }

        public virtual void Start()
        {
            this.ShellServer.KernelInfoRequest += OnKernelInfoRequest;
            this.ShellServer.ExecuteRequest += OnExecuteRequest;
            this.ShellServer.ShutdownRequest += OnShutdownRequest;
        }

        public virtual void OnKernelInfoRequest(Message message)
        {
            this.ShellServer.SendShellMessage(
                new Message
                {
                    ZmqIdentities = message.ZmqIdentities,
                    ParentHeader = message.Header,
                    Metadata = null,
                    Content = this.Context.Properties.AsKernelInfoReply(),
                    Header = new MessageHeader
                    {
                        MessageType = "kernel_info_reply",
                        Id = Guid.NewGuid().ToString(),
                        ProtocolVersion = "5.2.0"
                    }
                }
            );
        }

        public virtual void OnExecuteRequest(Message message)
        {
            this.Logger.LogDebug($"Asked to execute code:\n{((ExecuteRequestContent)message.Content).Code}");

            // Begin by sending that we're busy.
            this.ShellServer.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "status"
                    },
                    Content = new KernelStatusContent
                    {
                        ExecutionState = ExecutionState.Busy
                    }
                }.AsReplyTo(message)
            );

            // Run in the engine.
            var engineResponse = Execute(
                ((ExecuteRequestContent)message.Content).Code,
                new ExecutionChannel(this, message)
            );

            // Send the engine's output as an execution result.
            if (engineResponse.Output != null)
            {
                var serialized = EncodeForDisplay(engineResponse.Output);
                this.ShellServer.SendIoPubMessage(
                    new Message
                    {
                        ZmqIdentities = message.ZmqIdentities,
                        ParentHeader = message.Header,
                        Metadata = null,
                        Content = new ExecuteResultContent
                        {
                            ExecutionCount = this.ExecutionCount,
                            Data = serialized.Data,
                            Metadata = serialized.Metadata
                        },
                        Header = new MessageHeader
                        {
                            MessageType = "execute_result"
                        }
                    }
                );
            }

            // Handle the message.
            this.ShellServer.SendShellMessage(
                new Message
                {
                    ZmqIdentities = message.ZmqIdentities,
                    ParentHeader = message.Header,
                    Metadata = null,
                    Content = new ExecuteReplyContent
                    {
                        ExecuteStatus = engineResponse.Status,
                        ExecutionCount = this.ExecutionCount
                    },
                    Header = new MessageHeader
                    {
                        MessageType = "execute_reply"
                    }
                }
            );

            // Finish by telling the client that we're free again.
            this.ShellServer.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "status"
                    },
                    Content = new KernelStatusContent
                    {
                        ExecutionState = ExecutionState.Idle
                    }
                }.AsReplyTo(message)
            );
        }

        public virtual void OnShutdownRequest(Message message)
        {
            System.Environment.Exit(0);
        }

        private void WriteToStream(Message parent, StreamName stream, string text)
        {
            // Send the engine's output to stdout.
            this.ShellServer.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "stream"
                    },
                    Content = new StreamContent
                    {
                        StreamName = stream,
                        Text = text
                    }
                }.AsReplyTo(parent)
            );
        }

        private void WriteDisplayData(Message parent, object displayable)
        {
            var serialized = EncodeForDisplay(displayable);
            // Send the engine's output to stdout.
            this.ShellServer.SendIoPubMessage(
                new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "display_data"
                    },
                    Content = new DisplayDataContent
                    {
                        Data = serialized.Data,
                        Metadata = serialized.Metadata,
                        Transient = null
                    }
                }.AsReplyTo(parent)
            );
        }

        public virtual bool ContainsMagic(string input)
        {
            var parts = input.Trim().Split(new[] { ' ' }, 2);
            return magicMethods.ContainsKey(parts[0]);
        }

        public virtual bool IsMagic(string input) => ContainsMagic(input);

        public virtual ExecutionResult Execute(string input, IChannel channel)
        {
            this.ExecutionCount++;
            this.History.Add(input);

            // We first check to see if the first token is a
            // magic command for this kernel.

            if (IsMagic(input))
            {
                return ExecuteMagic(input, channel);
            }
            else
            {
                return ExecuteMundane(input, channel);
            }

        }

        public virtual ExecutionResult ExecuteMagic(string input, IChannel channel)
        {
            // Which magic command do we have? Split up until the first space.
            var parts = input.Split(new[] { ' ' }, 2);
            if (magicMethods.ContainsKey(parts[0]))
            {
                var method = magicMethods[parts[0]];
                var remainingInput = parts.Length > 1 ? parts[1] : "";
                return (ExecutionResult)method.Invoke(this, new object[] { remainingInput, channel });
            }
            else
            {
                channel.Stderr($"Magic command {parts[0]} not recognized.");
                return ExecuteStatus.Error.ToExecutionResult();
            }
        }

        public abstract ExecutionResult ExecuteMundane(string input, IChannel channel);

        [MagicCommand("%history")]
        public ExecutionResult ExecuteHistory(string input, IChannel channel)
        {
            return History.ToExecutionResult();
        }

        [MagicCommand("%version")]
        public ExecutionResult ExecuteVersion(string input, IChannel channel)
        {
            var versions = new [] {
                (Context.Properties.KernelName, Context.Properties.KernelVersion),
                ("Jupyter Core", typeof(BaseEngine).Assembly.GetName().Version.ToString())
            };
            channel.Display(
                new Table<(string, string)>
                {
                    Columns = new List<(string, Func<(string, string), string>)>
                    {
                        ("Component", item => item.Item1),
                        ("Version", item => item.Item2)
                    },
                    Rows = versions.ToList()
                }
            );
            return ExecuteStatus.Ok.ToExecutionResult();
        }
    }
}
