// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      Provides extension methods used throughout the Jupyter Core library.
    /// </summary>
    public static partial class Extensions
    {
        /// <summary>
        ///     Creates a wrapper of an IChannel that adds new lines to every message
        ///     sent to stdout and stderr.
        /// </summary>
        /// <remarks>
        ///     If <c>original</c> is already a <c>ChannelWithNewLines</c>, this method
        ///     simply returns <c>original</c> unmodified.
        /// </remarks>
        public static ChannelWithNewLines WithNewLines(this IChannel original) =>
            (original is ChannelWithNewLines ch) ? ch : new ChannelWithNewLines(original);
    }

    /// <summary>
    ///     This is a Jupyter Core IChannel that wraps an existing IChannel and
    ///     adds NewLine symbols (Environment.NewLine)
    ///     to every message that gets logged to Stdout and Stderror.
    /// </summary>
    public class ChannelWithNewLines : IChannel
    {
        /// <summary>
        ///     The existing channel that this channel wraps with new lines.
        /// </summary>
        public IChannel BaseChannel { get; }

        /// <summary>
        ///     Constructs a new channel, given a base channel to be wrapped
        ///     with newlines.
        /// </summary>
        public ChannelWithNewLines(IChannel original) => BaseChannel = original;

        /// <summary>
        ///     Formats a given message for display to stdout or stderr.
        /// </summary>
        /// <param name="msg">The message to be formatted.</param>
        /// <returns>
        ///     <paramref name="msg" />, formatted with a trailing newline
        ///     (<c>Environment.NewLine</c>).
        /// </returns>
        public static string Format(string msg) => $"{msg}{Environment.NewLine}";

        /// <summary>
        ///     Writes a given message to the base channel's standard output,
        ///     but with a trailing newline appended.
        /// </summary>
        /// <param name="message">The message to be written.</param>
        public void Stdout(string message) => BaseChannel?.Stdout(Format(message));

        /// <summary>
        ///     Writes a given message to the base channel's standard error,
        ///     but with a trailing newline appended.
        /// </summary>
        /// <param name="message">The message to be written.</param>
        public void Stderr(string message) => BaseChannel?.Stderr(Format(message));

        /// <summary>
        ///     Displays a given object using the base channel.
        /// </summary>
        /// <param name="displayable">The object to be displayed.</param>
        /// <remarks>
        ///     Note that no newline is appended by this method, as the
        ///     displayable object need not be a string.
        /// </remarks>
        public void Display(object displayable) => BaseChannel?.Display(displayable);

        /// <summary>
        ///     Displays a given object using the base channel, allowing for
        ///     future updates.
        /// </summary>
        /// <param name="displayable">The object to be displayed.</param>
        /// <remarks>
        ///     Note that no newline is appended by this method, as the
        ///     displayable object need not be a string.
        /// </remarks>
        /// <returns>
        ///     An object that can be used to update the display in the future.
        /// </returns>
        public IUpdatableDisplay DisplayUpdatable(object displayable) => BaseChannel?.DisplayUpdatable(displayable);

        /// <inheritdoc/>
        public void SendIoPubMessage(Message message) => BaseChannel?.SendIoPubMessage(message);
    }
}
