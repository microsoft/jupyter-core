// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Jupyter.Core
{

    internal static class Constants
    {
        internal static KernelProperties PROPERTIES = new KernelProperties
        {
            FriendlyName = "IMoon",
            KernelName = "imoon",
            KernelVersion = typeof(Program).Assembly.GetName().Version.ToString(),
            DisplayName = "Lua (MoonScript)",

            LanguageName = "Lua",
            LanguageVersion = "0.1",
            LanguageMimeType = "text/plain",
            LanguageFileExtension = ".lua",

            Description = "Runs Lua using the MoonScript interpreter."
        };
    }
}
