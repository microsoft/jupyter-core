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
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      Marks that a given method implements the given magic command.
    /// </summary>
    /// <remarks>
    ///      Each magic command method must have the signature
    ///      <c>ExecuteResult (string, IChannel)</c>, similar to
    ///      <ref>BaseEngine.ExecuteMundane</ref>.
    /// </remarks>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MagicCommandAttribute : System.Attribute
    {
        public readonly string Name;
        public readonly Documentation Documentation;

        public MagicCommandAttribute(
            string name,
            string summary = null,
            string fullDocumentation = null
        )
        {
            Name = name;
            Documentation = new Documentation
            {
                Full = fullDocumentation,
                Summary = summary
            };
        }
    }

    public class MagicCommandResolver : ISymbolResolver
    {
        private IDictionary<string, MagicCommandAttribute> attributes;
        public MagicCommandResolver(IDictionary<string, MagicCommandAttribute> attributes)
        {
            this.attributes = attributes;
        }

        public Symbol? Resolve(string symbolName)
        {
            if (this.attributes.ContainsKey(symbolName))
            {
                var attr = this.attributes[symbolName];
                return new Symbol
                {
                    Name = attr.Name,
                    Documentation = attr.Documentation,
                    Kind = SymbolKind.Magic
                };
            }
            else return null;
        }
    }

    /// <summary>
    ///      Abstract class used to provide the basic functionality needed by
    ///      the evaluation engine for each kernel.
    ///      At a minimum, each subclass must provide an implementation of the
    ///      method used for executing mundane (non-magic) cells.
    ///      Subclasses can additionally provide new magic commands, display
    ///      encoders, and can override communication with the various socket
    ///      servers that make up the kernel.
    /// </summary>
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

            public void Display(object displayable)
            {
                if (displayable == null) throw new ArgumentNullException(nameof(displayable));
                engine.WriteDisplayData(parent, displayable);
            }

            public void Stderr(string message)
            {
                if (message == null) throw new ArgumentNullException(nameof(message));
                engine.WriteToStream(parent, StreamName.StandardError, message);
            }

            public void Stdout(string message)
            {
                if (message == null) throw new ArgumentNullException(nameof(message));
                engine.WriteToStream(parent, StreamName.StandardOut, message);
            }
        }

        /// <summary>
        ///      The number of cells that have been executed since the start of
        ///      this engine. Used by clients to typeset cell numbers, e.g.:
        ///      <c>In[12]:</c>.
        /// </summary>
        public int ExecutionCount { get; protected set; }
        protected List<string> History;
        private Dictionary<string, Stack<IResultEncoder>> serializers = new Dictionary<string, Stack<IResultEncoder>>();
        private List<ISymbolResolver> resolvers = new List<ISymbolResolver>();
        private readonly ImmutableDictionary<string, (MagicCommandAttribute, MethodInfo)> magicMethods;

        /// <summary>
        ///     The shell server used to communicate with the clients over the
        ///     shell IOPub socket.
        /// </summary>
        public IShellServer ShellServer { get; }

        /// <summary>
        ///      The context object for this engine, recording how the kernel
        ///      was invoked, and metadata about the language supported by the
        ///      kernel.
        /// </summary>
        public KernelContext Context { get; }

        /// <summary>
        ///     A logger used to report errors, warnings, and debugging
        ///     information internal to the operation of the engine.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        ///      Constructs an engine that communicates with a given server,
        ///      and uses a given kernel context.
        /// </summary>
        /// <remarks>
        ///      This constructor should only be called by a dependency
        ///      injection framework.
        /// </remarks>
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
            var magicMethods = this
                .GetType()
                .GetMethods()
                .Where(
                    method => method.GetCustomAttributes(typeof(MagicCommandAttribute), inherit: true).Length > 0
                )
                .Select(
                    method => {
                        var attr = (
                            (MagicCommandAttribute)
                            method
                            .GetCustomAttributes(typeof(MagicCommandAttribute), inherit: true)
                            .Single()
                        );
                        return (attr, method);
                    }
                )
                .ToImmutableDictionary(
                    pair => pair.attr.Name,
                    pair => (pair.attr, pair.method)
                );

            RegisterSymbolResolver(new MagicCommandResolver(
                magicMethods
                    .ToDictionary(
                        item => item.Key,
                        item => item.Value.attr
                    )
            ));

            RegisterDefaultEncoders();
        }

        public void RegisterSymbolResolver(ISymbolResolver resolver)
        {
            resolvers.Add(resolver);
        }

        #region Display and Result Encoding

        /// <summary>
        ///      Adds a new result encoder to the list of encoders used by this
        ///      engine to format data for output as execution results and rich
        ///      displays.
        ///      The most recently added result encoders take precedence over
        ///      any result encoders that have already been registered.
        /// </summary>
        public void RegisterDisplayEncoder(IResultEncoder encoder)
        {
            if (encoder == null) throw new ArgumentNullException(nameof(encoder));
            if (!serializers.ContainsKey(encoder.MimeType))
            {
                this.serializers[encoder.MimeType] = new Stack<IResultEncoder>();
            }
            this.serializers[encoder.MimeType].Push(encoder);
        }

        public void RegisterDisplayEncoder(string mimeType, Func<object, EncodedData?> encoder) =>
            RegisterDisplayEncoder(new FuncResultEncoder(mimeType, encoder));

        public void RegisterDisplayEncoder(string mimeType, Func<object, string> encoder) =>
            RegisterDisplayEncoder(new FuncResultEncoder(mimeType, encoder));

        /// <summary>
        ///      Adds a new result encoder that serializes its output to JSON.
        ///      Serialization failures are logged, but are not written out
        ///      as results or displayed.
        /// </summary>
        /// <param name="converters">
        ///      Additional JSON converters to be used when serializing results
        ///      into JSON.
        /// </param>
        public void RegisterJsonEncoder(params JsonConverter[] converters) =>
            RegisterDisplayEncoder(new JsonResultEncoder(this.Logger, converters));

        /// <summary>
        ///      Registers a default set of result encoders that is sufficient
        ///      for most basic kernel operations.
        /// </summary>
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
            if (displayable == null) throw new ArgumentNullException(nameof(displayable));
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

        #endregion

        #region Display Messaging

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
            if (displayable == null) throw new ArgumentNullException(nameof(displayable));
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


        #endregion

        #region Lifecycle

        public virtual void Start()
        {
            this.ShellServer.KernelInfoRequest += OnKernelInfoRequest;
            this.ShellServer.ExecuteRequest += OnExecuteRequest;
            this.ShellServer.ShutdownRequest += OnShutdownRequest;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        ///       Called by shell servers to report kernel information to the
        ///       client. By default, this method responds by converting
        ///       the kernel properties stored in this engine's context to a
        ///       <c>kernel_info</c> Jupyter message.
        /// </summary>
        /// <param name="message">The original request from the client.</param>
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

        #endregion

        #region Command Parsing

        public virtual bool ContainsMagic(string input)
        {
            var parts = input.Trim().Split(new[] { ' ' }, 2);
            return magicMethods.ContainsKey(parts[0]);
        }

        public virtual bool IsMagic(string input) => ContainsMagic(input);

        #endregion

        #region Command Execution

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
                (var attr, var method) = magicMethods[parts[0]];
                var remainingInput = parts.Length > 1 ? parts[1] : "";
                return (ExecutionResult)method.Invoke(this, new object[] { remainingInput, channel });
            }
            else
            {
                channel.Stderr($"Magic command {parts[0]} not recognized.");
                return ExecuteStatus.Error.ToExecutionResult();
            }
        }

        /// <summary>
        ///      Executes a given input cell, returning any result from the
        ///      execution.
        /// </summary>
        /// <param name="input">The input sent by the client for execution.</param>
        /// <param name="channel">The display channel used to present information back to the client.</param>
        /// <returns>
        ///     A value indicating whether the input executed
        ///     correctly, and what value should be reported back to the client
        ///     as the result of executing the input (e.g.: as the result typeset
        ///     as <c>Out[12]:</c> outputs).
        /// </returns>
        public abstract ExecutionResult ExecuteMundane(string input, IChannel channel);

        #endregion

        #region Example Magic Commands

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

        #endregion

    }
}
