using System;

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///      Represents the kind of a symbol that results from resolution.
    ///      Mainly useful for providing icons and other UI tools such as
    ///      inline documentation and autocompletion.
    /// </summary>
    public enum SymbolKind
    {
        /// <summary>
        ///      Used for symbols that represent magic commands available to
        ///      the user.
        /// </summary>
        Magic,

        /// <summary>
        ///      Used for symbols that don't match any of the other kinds in
        ///      this enum.
        /// </summary>
        Other
    }

    /// <summary>
    ///       Documentation for a symbol.
    /// </summary>
    public struct Documentation
    {
        /// <summary>
        ///     Complete documentation for the symbol.
        /// </summary>
        public string? Full;

        /// <summary>
        ///     A brief summary of the documentation for the symbol, suitable
        ///     for showing as on-hover help or for use with <c>?</c> commands.
        /// </summary>
        public string Summary;
    }

    public interface ISymbol
    {
        string Name { get; }
        SymbolKind Kind { get; }
    }

    public interface ISymbolResolver
    {
        /// <summary>
        ///      Attempts to find a symbol by a given name.
        /// </summary>
        /// <returns>
        ///      The symbol if resolution completed successfully, and <c>null</c>
        ///      otherwise.
        /// </returns>
        ISymbol? Resolve(string symbolName);
    }

}
