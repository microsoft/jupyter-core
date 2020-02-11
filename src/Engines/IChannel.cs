// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Jupyter.Core
{
    public interface IUpdatableDisplay
    {
        void Update(object displayable);
    }

    /// <summary>
    ///      Specifies a display channel between a Jupyter kernel and its clients
    ///      that can be used for printing to streams and for displaying
    ///      rich data.
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
        IUpdatableDisplay DisplayUpdatable(object displayable);
    }
}