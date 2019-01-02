// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Jupyter.Core
{
    internal static class Constants
    {
        internal static KernelProperties PROPERTIES = new KernelProperties
        {
            // Name of the kernel as it appears outside of code.
            FriendlyName = "IEcho",
            // A short name for the kernel to be used in code, such as when
            // calling from jupyter_client.
            KernelName = "iecho",
            // The version of the kernel.
            KernelVersion = typeof(Program).Assembly.GetName().Version.ToString(),
            // Name of the kernel as it should appear in lists of available kernels.
            DisplayName = "Echo",

            // Name of the language implemented by the kernel.
            // Note that this property is used to set the syntax highlighting
            // mode in some clients, such as Jupyter Notebook.
            LanguageName = "Echo",
            // Version of the language implemeted by the kernel.
            LanguageVersion = "0.1",
            // The MIME type of the language implemented by the kernel.
            // This property is used mainly for providing "plain" downloads from
            // Jupyter clients.
            LanguageMimeType = MimeTypes.PlainText,
            // The file extension for source files written in the language
            // implemented by the kernel.
            // This property is used mainly for providing "plain" downloads from
            // Jupyter clients.
            LanguageFileExtension = ".txt",

            // An extended description of the kernel.
            Description = "A simple kernel that echos its input."
        };
    }
}