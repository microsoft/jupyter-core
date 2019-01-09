using System;

namespace Microsoft.Jupyter.Core
{
    public enum SymbolKind
    {
        Magic,
        Other
    }

    public struct Documentation
    {
        public string Full;
        public string Summary;
    }

    public interface ISymbol
    {
        string Name { get; }
        SymbolKind Kind { get; }
    }

    public interface ISymbolResolver
    {
        ISymbol Resolve(string symbolName);
    }

}
