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
using System.Diagnostics;

namespace Microsoft.Jupyter.Core
{

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
                engine.WriteDisplayData(parent, displayable);
            }

            public void Stderr(string message)
            {
                engine.WriteToStream(parent, StreamName.StandardError, message);
            }

            public void Stdout(string message)
            {
                engine.WriteToStream(parent, StreamName.StandardOut, message);
            }
        }

        /// <summary>
        /// The list of arguments passed down to the <c>MundaneExecuted</c>, 
        /// <c>MagicExecuted</c> and <c>HelpExecuted</c> events.
        /// </summary>
        public class ExecutedEventArgs : EventArgs
        {
            /// <summary>
            /// Default constructor, populates the corresponding event fields.
            /// </summary>
            public ExecutedEventArgs(ISymbol symbol, ExecutionResult result, TimeSpan duration)
            {
                this.Symbol = symbol;
                this.Result = result;
                this.Duration = duration;
            }

            /// <summary>
            /// The symbol, for example the Magic symbol, that was executed.
            /// For the <c>MundaneExecuted</c> this is null.
            /// </summary>
            public ISymbol Symbol { get; }

            /// <summary>
            /// The actual result from the execution.
            /// </summary>
            public ExecutionResult Result { get; }

            /// <summary>
            /// How long the execution took.
            /// </summary>
            public TimeSpan Duration { get; }
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

        /// <summary>
        /// This event is triggered when a non-magic cell is executed.
        /// </summary>
        public event EventHandler<ExecutedEventArgs> MundaneExecuted;

        /// <summary>
        /// This event is triggered when a magic command is executed. Magic commands are typically
        /// identified by symbol    s that is pre-fixed with '%' (like <c>%history</c>).
        /// </summary>
        public event EventHandler<ExecutedEventArgs> MagicExecuted;

        /// <summary>
        /// This event is triggered when a the help command is executed. Help commands are typically
        /// identified when a symbols starts or finishes with '?' (like <c>history?</c>).
        /// </summary>
        public event EventHandler<ExecutedEventArgs> HelpExecuted;

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
                ILogger logger
        )
        {
            ExecutionCount = 0;
            this.ShellServer = shell;
            this.Context = context.Value;
            this.Logger = logger;

            History = new List<string>();
            var magicResolver = new MagicCommandResolver(this);
            RegisterSymbolResolver(magicResolver);

            RegisterDefaultEncoders();
        }

        #region Symbol Resolution

        /// <summary>
        ///      Adds a new symbol resolver to the list of resolvers used by the
        ///      <see cref="Resolve" /> method.
        /// </summary>
        /// <param name="resolver">A symbol resolver to be registered.</param>
        public void RegisterSymbolResolver(ISymbolResolver resolver)
        {
            resolvers.Add(resolver);
        }

        /// <summary>
        ///      Given the name of a symbol, returns an object representing that
        ///      symbol using each registered symbol resolver in turn.
        /// </summary>
        /// <param name="symbolName">Name of the symbol to be resolved.</param>
        /// <returns>
        ///     An <c>ISymbol</c> representing the resolution of the given
        ///     symbol name, or <c>null</c> if the symbol could not be resolved.
        /// </returns>
        /// <remarks>
        ///     The most recently added symbol resolvers are used first, falling
        ///     back through to the oldest resolver until a resolution is found.
        /// </remarks>
        public ISymbol Resolve(string symbolName)
        {
            if (symbolName == null) { throw new ArgumentNullException(nameof(symbolName)); }
            foreach (var resolver in resolvers.EnumerateInReverse())
            {
                var resolution = resolver.Resolve(symbolName);
                if (resolution != null) return resolution;
            }
            return null;
        }

        #endregion

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
            RegisterDisplayEncoder(new MagicSymbolToTextResultEncoder());
            RegisterDisplayEncoder(new MagicSymbolToHtmlResultEncoder());
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
                    try
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
                    catch (Exception e)
                    {
                        this.Logger.LogWarning(e, $"Encoder {encoder.GetType().FullName} threw an exception encoding '{displayable}' ({e.Message}).");
                    }
                }
            }

            return displayData;
        }

        #endregion

        #region Display Messaging

        private void WriteToStream(Message parent, StreamName stream, string text)
        {
            try
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
            catch(Exception e)
            {
                this.Logger?.LogError(e, "Unexpected error when trying to write to stream.");
            }
        }

        private void WriteDisplayData(Message parent, object displayable)
        {
            try
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
            catch (Exception e)
            {
                this.Logger?.LogError(e, "Unexpected error when trying to write display data.");
            }
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
            try
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
            catch (Exception e)
            {
                this.Logger?.LogError(e, "Unable to process KernelInfoRequest");
            }
        }

        public virtual void OnExecuteRequest(Message message)
        {
            this.Logger.LogDebug($"Asked to execute code:\n{((ExecuteRequestContent)message.Content).Code}");

            try
            {
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
            catch (Exception e)
            {
                this.Logger?.LogError(e, "Unable to process ExecuteRequest");
            }
        }

        public virtual void OnShutdownRequest(Message message)
        {
            System.Environment.Exit(0);
        }

        #endregion

        #region Command Parsing

        public virtual bool IsMagic(string input, out ISymbol symbol)
        {
            if (input == null)
            {
                symbol = null;
            }
            else
            {
                var parts = input.Trim().Split(new[] { ' ' }, 2);
                symbol = Resolve(parts[0]) as MagicSymbol;
            }

            return symbol != null;
        }

        /// <summmary>
        ///      Returns <c>true</c> if a given input is a request for help
        ///      on a symbol. If this method returns true, then <c>symbol</c>
        ///      will be populated with the resolution of the symbol targeted
        ///      for help.
        /// </summary>
        /// <remarks>
        ///      If an input is a request for help on an invalid symbol, then
        ///      this method will return true, but <c>symbol</c> will be null.
        /// </remarks>
        public virtual bool IsHelp(string input, out ISymbol symbol)
        {
            if (input == null)
            {
                symbol = null;
            }
            else
            {
                var stripped = input.Trim();
                string symbolName = null;
                if (stripped.StartsWith("?"))
                {
                    symbolName = stripped.Substring(1, stripped.Length - 1);
                }
                else if (stripped.EndsWith("?"))
                {
                    symbolName = stripped.Substring(0, stripped.Length - 1);
                }

                symbol = symbolName != null ? Resolve(symbolName) : null;
            }

            return symbol != null;
        }

        #endregion

        #region Command Execution

        /// <summary>
        /// Main entry point to execute a Jupyter cell.
        /// 
        /// It identifies if the cell contains a help or magic command and triggers ExecuteHelp
        /// or ExecuteMagic accordingly. If no special symbols are found, it triggers ExecuteMundane.
        /// </summary>
        /// <param name="input">the cell's content.</param>
        /// <param name="channel">the channel to generate messages or errors.</param>
        /// <returns>An <c>ExecutionResult</c> instance with the results of </returns>
        public virtual ExecutionResult Execute(string input, IChannel channel)
        {
            this.ExecutionCount++;

            try
            {
                this.History.Add(input);

                // We first check to see if the first token is a
                // magic command for this kernel.

                if (IsHelp(input, out var helpSymbol))
                {
                    return ExecuteAndNotify(input, helpSymbol, channel, ExecuteHelp, HelpExecuted);
                }
                else if (IsMagic(input, out var magicSymbol))
                {
                    return ExecuteAndNotify(input, magicSymbol, channel, ExecuteMagic, MagicExecuted);
                }
                else
                {
                    return ExecuteAndNotify(input, channel, ExecuteMundane, MundaneExecuted);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, $"Exception encountered when executing input: ${input}");
                channel.Stderr(e.Message);
                return ExecuteStatus.Error.ToExecutionResult();
            }
        }

        public virtual ExecutionResult ExecuteHelp(string input, ISymbol symbol, IChannel channel)
        {
            if (symbol == null)
            {
                channel.Stderr($"Symbol not found.");
                return ExecuteStatus.Error.ToExecutionResult();
            }
            else
            {
                return symbol.ToExecutionResult();
            }
        }

        public virtual ExecutionResult ExecuteMagic(string input, ISymbol symbol, IChannel channel)
        {
            // We should never be called with an ISymbol that isn't a MagicSymbol,
            // since this method should only be called by using magicResolver.
            Debug.Assert(symbol as MagicSymbol != null);

            // Which magic command do we have? Split up until the first space.
            if (symbol is MagicSymbol magic)
            {
                var parts = input.Trim().Split(new[] { ' ' }, 2);
                var remainingInput = parts.Length > 1 ? parts[1] : "";
                return magic.Execute(remainingInput, channel);
            }
            else
            {
                channel.Stderr($"Magic command {symbol?.Name} not recognized.");
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

        /// <summary>
        ///     Executes the given action with the corresponding parameters, and then triggers the given event.
        /// </summary>
        public ExecutionResult ExecuteAndNotify(string input, IChannel channel, Func<string, IChannel, ExecutionResult> action, EventHandler<ExecutedEventArgs> evt)
        {
            var duration = Stopwatch.StartNew();
            var result = action(input, channel);
            duration.Stop();

            evt?.Invoke(this, new ExecutedEventArgs(null, result, duration.Elapsed));
            return result;
        }

        /// <summary>
        ///     Executes the given action with the corresponding parameters, and then triggers the given event.
        /// </summary>
        public ExecutionResult ExecuteAndNotify(string input, ISymbol symbol, IChannel channel, Func<string, ISymbol, IChannel, ExecutionResult> action, EventHandler<ExecutedEventArgs> evt)
        {
            var duration = Stopwatch.StartNew();
            var result = action(input, symbol, channel);
            duration.Stop();

            evt?.Invoke(this, new ExecutedEventArgs(symbol, result, duration.Elapsed));
            return result;
        }
        #endregion

        #region Example Magic Commands

        [MagicCommand("%history",
            summary: "Displays a list of commands run so far this session."
        )]
        public ExecutionResult ExecuteHistory(string input, IChannel channel)
        {
            return History.ToExecutionResult();
        }

        [MagicCommand("%version",
            summary: "Displays the version numbers for various components of this kernel."
        )]
        public ExecutionResult ExecuteVersion(string input, IChannel channel)
        {
            var versions = Context.Properties.VersionTable;
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
