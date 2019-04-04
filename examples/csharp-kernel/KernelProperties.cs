// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Jupyter.Core
{

    internal static class Constants
    {
        internal static KernelProperties PROPERTIES = new KernelProperties
        {
            FriendlyName = "ICSharp",
            KernelName = "icsharp",
            KernelVersion = typeof(Program).Assembly.GetName().Version.ToString(),
            DisplayName = "C#",

            LanguageName = "C#",
            LanguageVersion = "7.1",
            LanguageMimeType = "text/csharp",
            LanguageFileExtension = ".cs",

            Description = "C#"
        };
    }
}
