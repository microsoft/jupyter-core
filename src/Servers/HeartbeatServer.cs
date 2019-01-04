// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;

namespace Microsoft.Jupyter.Core
{
    public class HeartbeatServer : IDisposable, IHeartbeatServer
    {
        private bool alive = false;
        private Thread thread;
        private ResponseSocket socket;

        private ILogger<HeartbeatServer> logger;
        private KernelContext context;

        public HeartbeatServer(
            ILogger<HeartbeatServer> logger,
            IOptions<KernelContext> context
        )
        {
            this.logger = logger;
            this.context = context.Value;
        }

        public void Start()
        {
            alive = true;
            thread = new Thread(EventLoop);
            thread.Start();
        }

        public void Join() => thread.Join();

        public void Stop()
        {
            alive = false;
            thread.Interrupt();
            socket?.Close();
            Join();
            thread = null;
        }

        private void EventLoop()
        {
            var addr = context.ConnectionInfo.HeartbeatZmqAddress;
            this.logger.LogDebug("Starting heartbeat server at {Address}.", addr);
            socket = new ResponseSocket();
            socket.Bind(addr);

            while (alive)
            {
                // We use the Bytes receiver so that we can ping back data
                // unmodified, without worrying about encodings.
                try
                {
                    var data = socket.ReceiveFrameBytes();
                    logger.LogDebug($"Got heartbeat message of length {data.Length}.");
                    if (!socket.TrySendFrame(data))
                    {
                        logger.LogError("Error sending heartbeat message back to client.");
                    }
                }
                catch (ThreadInterruptedException)
                {
                    continue;
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Unhandled exception in heartbeat loop.");
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    socket?.Dispose();
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
