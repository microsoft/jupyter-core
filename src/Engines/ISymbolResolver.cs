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

    public struct Symbol
    {
        public string Name;
        public Lazy<Documentation> Documentation;
        public SymbolKind Kind;
    }

    public interface ISymbolResolver
    {
        Symbol? Resolve(string symbolName);
    }

}
