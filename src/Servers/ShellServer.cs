// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using Microsoft.Jupyter.Core.Protocol;
using System.Net;

namespace Microsoft.Jupyter.Core
{
    public class ShellServer : IDisposable, IShellServer
    {

        // A shell server works by using a request-reply pattern, as documented
        // at:
        //
        //     https://jupyter-client.readthedocs.io/en/stable/messaging.html#wire-protocol
        // In particular:
        //
        //     • The client sends a <action>_request message on its shell socket.
        //     • The kernel responds with a "status": "busy" message on its IOPub.
        //     • When the message has been fully acted upon, a <action>_reply
        //       message is sent out across the shell socket.
        //     • A final "status": "idle" is sent out on the IOPub socket.
        //
        // This sequence ensures that the shell socket can handle requests from
        // multiple frontends at once, while maintaining proper sequences.
        // Meanwhile, the IOPub socket makes sure that all frontends know about
        // the current status, stdout printing, etc.
        //
        // Note that for "quick" reply/request patterns, we need not advertise
        // that we are busy over the IOPub socket. For example, we skip that
        // step when responding to kernel_info_requests.
        private bool alive = false;
        private RouterSocket shellSocket;
        private Thread shellThread;

        private RouterSocket controlSocket;
        private Thread controlThread;

        private PublisherSocket ioPubSocket;

        private ILogger<ShellServer> logger;
        private KernelContext context;
        private IServiceProvider provider;
        private IShellRouter router;

        private string session;

        public ShellServer(
            ILogger<ShellServer> logger,
            IOptions<KernelContext> context,
            IServiceProvider provider,
            IShellRouter router
        )
        {
            this.logger = logger;
            this.context = context.Value;
            this.provider = provider;
            this.router = router;

            router.RegisterHandler("kernel_info_request", message => KernelInfoRequest?.Invoke(message));
            router.RegisterHandler("execute_request", message => ExecuteRequest?.Invoke(message));
            router.RegisterHandler("shutdown_request", message => ShutdownRequest?.Invoke(message));
        }

        public void Start()
        {
            shellSocket = new RouterSocket();
            shellSocket.Bind(context.ConnectionInfo.ShellZmqAddress);
            controlSocket = new RouterSocket();
            controlSocket.Bind(context.ConnectionInfo.ControlZmqAddress);
            ioPubSocket = new PublisherSocket();
            ioPubSocket.Bind(context.ConnectionInfo.IoPubZmqAddress);

            alive = true;
            controlThread = new Thread(() => EventLoop(controlSocket))
            {
                Name = "Control server"
            };
            controlThread.Start();
            shellThread = new Thread(() => EventLoop(shellSocket))
            {
                Name = "Shell server"
            };
            shellThread.Start();
        }

        public void SendShellMessage(Message message) =>
            SendMessage(shellSocket, "shell", message);

        public void SendControlMessage(Message message) =>
            SendMessage(controlSocket, "control", message);

        public void SendIoPubMessage(Message message) =>
            SendMessage(ioPubSocket, "iopub", message);

        private void SendMessage(NetMQSocket socket, string socketKind, Message message)
        {
            // Add metadata for the current session if needed.
            if (message.Header.Session == null)
            {
                message.Header.Session = session;
            }
            logger.LogDebug($"Sending {socketKind} message:\n\t{JsonConvert.SerializeObject(message)}");
            socket.SendMessage(context, message);
        }

        private void EventLoop(NetMQSocket socket)
        {
            this.logger.LogDebug("Starting shell server event loop at {Address}.", socket);
            while (alive)
            {
                try
                {
                    // Start by pulling off the next <action>_request message
                    // from the client.
                    var nextMessage = socket.ReceiveMessage(context);
                    logger.LogDebug(
                        $"Received new message:\n" +
                        $"\t{JsonConvert.SerializeObject(nextMessage.Header)}\n" +
                        $"\t{JsonConvert.SerializeObject(nextMessage.ParentHeader)}\n" +
                        $"\t{JsonConvert.SerializeObject(nextMessage.Metadata)}\n" +
                        $"\t{JsonConvert.SerializeObject(nextMessage.Content)}"
                    );

                    // If this is our first message, we need to set the session
                    // id.
                    if (session == null)
                    {
                        session = nextMessage.Header.Session;
                    }

                    // Get a service that can handle the message type and
                    // dispatch.
                    router.Handle(nextMessage);
                }
                catch (ProtocolViolationException ex)
                {
                    logger.LogCritical(ex, $"Protocol violation when trying to receive next ZeroMQ message.");
                }
                catch (ThreadInterruptedException)
                {
                    if (alive) continue; else return;
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public event Action<Message> KernelInfoRequest;
        public event Action<Message> ExecuteRequest;
        public event Action<Message> ShutdownRequest;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    shellSocket?.Dispose();
                    controlSocket?.Dispose();
                    ioPubSocket?.Dispose();
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
