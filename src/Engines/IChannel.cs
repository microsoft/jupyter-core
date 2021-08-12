// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///     Represents a display output that can be updated after it is first
    ///     rendered (e.g.: for displaying progress of long-running tasks,
    ///     or for providing interactivity with the user).
    /// </summary>
    public interface IUpdatableDisplay
    {
        /// <summary>
        ///     Replaces any previous content rendered to this display with a
        ///     new displayable object.
        /// </summary>
        /// <param name="displayable">
        ///     The object to be displayed. Cannot be null.
        /// </param>
        void Update(object displayable);
    }

    /// <summary>
    ///      Provided as a backwards compatability shim for implementations of
    ///      IChannel that do not support updatable display.
    /// </summary>
    internal class UpdatableDisplayFallback : IUpdatableDisplay
    {
        private readonly IChannel channel;

        public UpdatableDisplayFallback(IChannel channel)
        {
            this.channel = channel;
        }

        public void Update(object displayable)
        {
            channel.Display(displayable);
        }
    }

    /// <summary>
    ///      Specifies a display channel between a Jupyter kernel and its clients
    ///      that can be used for printing to streams, displaying rich data, and
    ///      sending messages.
    /// </summary>
    public interface IChannel
    {
        /// <summary>
        ///      Writes a message to this channel's standard output stream.
        /// </summary>
        /// <param name="message">The message to be written. Cannot be null.</param>
        void Stdout(string message);

        /// <summary>
        ///      Writes a message to this channel's standard error stream.
        /// </summary>
        /// <param name="message">The message to be written. Cannot be null.</param>
        void Stderr(string message);

        /// <summary>
        ///     Displays an object using this display channel.
        /// </summary>
        /// <param name="displayable">The object to be displayed. Cannot be null.</param>
        void Display(object displayable);

        /// <summary>
        ///     Displays an object using this display channel and allows for the
        ///     object to be updated with future calls.
        /// </summary>
        /// <param name="displayable">The object to be displayed. Cannot be null.</param>
        /// <returns>An object that can be used to update the display.</returns>
        public IUpdatableDisplay DisplayUpdatable(object displayable)
        {
            this.Display(displayable);
            return new UpdatableDisplayFallback(this);
        }

        /// <summary>
        ///     Sends an iopub message to the client associated with this channel.
        /// </summary>
        /// <param name="message">The message to send. Cannot be null.</param>
        void SendIoPubMessage(Message message) => throw new NotImplementedException();

        /// <summary>
        ///     Gets the comms router associated with this channel, if any,
        ///     or <c>null</c> if no comms router is available.
        /// </summary>
        /// <remarks>
        ///     Engines should ensure that this value is not <c>null</c>. In
        ///     a future version, this property will no longer be nullable.
        /// </remarks>
        public ICommsRouter? CommsRouter => null;

    }
}
