namespace Microsoft.Jupyter.Core
{

    internal static class Constants
    {
        internal static KernelProperties PROPERTIES = new KernelProperties
        {
            FriendlyName = "IKusto",
            KernelName = "ikusto",
            KernelVersion = typeof(Program).Assembly.GetName().Version.ToString(),
            DisplayName = "Kusto",

            LanguageName = "Kusto",
            LanguageVersion = "0.1",
            LanguageMimeType = "text/plain",
            LanguageFileExtension = ".txt",

            Description = "Query language for the Azure Data Explorer service."
        };
    }
}