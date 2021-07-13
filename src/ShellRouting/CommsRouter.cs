// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Jupyter.Core.Protocol;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Microsoft.Jupyter.Core
{
    /// <inheritdoc />
    public class CommsRouter : ICommsRouter
    {

        #region Inner Classes
        
        /// <inheritdoc />
        public class CommSession : ICommSession, IDisposable
        {
            /// <inheritdoc />
            public event Func<CommMessageContent, Task>? OnMessage;

            /// <inheritdoc />
            public event Func<CommSessionClosedBy, Task>? OnClose;


            /// <inheritdoc />
            public bool IsValid { get; private set; }

            /// <inheritdoc />
            public string Id { get; private set; }

            private readonly CommsRouter commsRouter;


            internal CommSession(CommsRouter commsRouter, string id)
            {
                this.commsRouter = commsRouter;
                this.Id = id;
            }

            /// <inheritdoc />
            public async Task SendMessage(object contents)
            {
                if (!IsValid)
                {
                    throw new ProtocolViolationException(
                        "Attempted to send a message on a comms session that has been closed or disposed. " +
                        "If you did not close this comms session, it may have been closed from the client. " +
                        "You can subscribe to the OnClose event of this session to check for closures coming from the client."
                    );
                }

                var messsage = new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "comm_msg"
                    },
                    Content = new CommMessageContent
                    {
                        Id = this.Id,
                        RawData = JToken.FromObject(contents)
                    }
                };
                await this.commsRouter.SendMessage(messsage);
            }

            internal async Task HandleClose(CommSessionClosedBy closedBy)
            {
                Debug.Assert(IsValid);
                IsValid = false;
                await (this.OnClose?.Invoke(closedBy) ?? Task.CompletedTask);
            }

            internal async Task HandleMessage(CommMessageContent content)
            {
                Debug.Assert(IsValid);
                await (this.OnMessage?.Invoke(content) ?? Task.CompletedTask);
            }

            /// <inheritdoc />
            public async Task Close()
            {
                // Make Close idempotent; that is, closing a closed session should
                // do nothing.
                if (!IsValid)
                {
                    return;
                }

                var message = new Message
                {
                    Header = new MessageHeader
                    {
                        MessageType = "comm_close"
                    },
                    Content = new CommCloseContent
                    {
                        Id = this.Id
                    }
                };

                await this.commsRouter.SendMessage(message);
                await HandleClose(closedBy: CommSessionClosedBy.Kernel);
            }

            void IDisposable.Dispose() => Close().Wait();
        }

        private class CommSessionOpen : ICommSessionOpen
        {
            public event Action<ICommSession>? On = null;

            internal void Handle(ICommSession session) =>
                On?.Invoke(session);
        }

        #endregion

        private readonly Dictionary<string, CommSession> openSessions
            = new Dictionary<string, CommSession>();

        private readonly Dictionary<string, CommSessionOpen> sessionHandlers
            = new Dictionary<string, CommSessionOpen>();

        private IShellServer Server { get; set; }

        private IShellRouter Router { get; set; }

        private ILogger<CommsRouter>? logger;

        /// <summary>
        ///      Constructs a new comms router, given services required by the
        ///      new router.
        /// </summary>
        public CommsRouter(IShellServer server, IShellRouter router, ILogger<CommsRouter>? logger = null)
        {
            this.Server = server;
            this.Router = router;
            this.logger = logger;

            router.RegisterHandler("comm_open", async (message) =>
            {
                if (message.Content is CommOpenContent openContent)
                {
                    if (sessionHandlers.TryGetValue(openContent.TargetName, out var handler))
                    {
                        var session = new CommSession(this, openContent.Id);
                        openSessions.Add(session.Id, session);
                        handler.Handle(session);
                    }
                    else
                    {
                        // According to the Jupyter messaging protocol, we are
                        // supposed to ignore comm_open messages entirely if
                        // we don't recognizer the target_name property.
                        logger.LogWarning(
                            "Got a comm_open message for target name {TargetName}, but no handler for that target name has been registered.",
                            openContent.TargetName
                        );
                    }
                }
                else
                {
                    logger.LogError(
                        "Expected message content for a comm_open message, but got content of type {Type} instead.",
                        message.Content.GetType()
                    );
                }
            });

            router.RegisterHandler("comm_msg", async (message) =>
            {
                if (message.Content is CommMessageContent msgContent)
                {
                    if (!openSessions.TryGetValue(msgContent.Id, out var session))
                    {
                        logger.LogError(
                            "Got comms message for session {Id}, but no such session is currently open.",
                            session.Id
                        );
                    }
                    await session.HandleMessage(msgContent);
                }
                else
                {
                    logger.LogError(
                        "Expected message content for a comm_msg message, but got content of type {Type} instead.",
                        message.Content.GetType()
                    );
                }
            });

            router.RegisterHandler("comm_close", async (message) =>
            {
                if (message.Content is CommCloseContent closeContent)
                {
                    if (!openSessions.TryGetValue(closeContent.Id, out var session))
                    {
                        logger.LogError(
                            "Asked by client to close comms session with {Id}, but no such session is currently open.",
                            session.Id
                        );
                    }
                    openSessions.Remove(session.Id);
                    await session.HandleClose(CommSessionClosedBy.Client);
                }
                else
                {
                    logger.LogError(
                        "Expected message content for a comm_close message, but got content of type {Type} instead.",
                        message.Content.GetType()
                    );
                }
            });
        }

        /// <inheritdoc />
        public async Task<ICommSession> OpenSession(string targetName, object? data = null)
        {
            var id = Guid.NewGuid().ToString();
            var commSession = new CommSession(this, id);
            openSessions.Add(id, commSession);

            await SendMessage(new Message
            {
                Content = new CommOpenContent
                {
                    RawData = data == null ? null : JToken.FromObject(data),
                    Id = id,
                    TargetName = targetName
                },
                Header = new MessageHeader
                {
                    MessageType = "comm_open"
                }
            });

            return commSession;
        }

        internal void RemoveSession(CommSession session)
        {
            Debug.Assert(
                openSessions.ContainsKey(session.Id),
                "Attempted to remove a session that was not still open. " +
                "This is an internal error that should never happen."
            );
            openSessions.Remove(session.Id);
        }

        internal Task SendMessage(Message messsage)
        {
            this.Server.SendShellMessage(messsage);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public ICommSessionOpen SessionOpenEvent(string targetName)
        {
            if (sessionHandlers.TryGetValue(targetName, out var handler))
            {
                return handler;
            }
            else
            {
                var newHandler = new CommSessionOpen();
                sessionHandlers.Add(targetName, newHandler);
                return newHandler;
            }
        }
    }
}
