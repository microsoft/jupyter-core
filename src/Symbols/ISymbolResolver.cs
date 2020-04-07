using System;
using System.Collections.Generic;
#nullable enable

namespace Microsoft.Jupyter.Core
{
    /// <summary>
    ///     Identifies the kind of a symbol (e.g.: whether a symbol is a magic
    ///     symbol).
    /// </summary>
    public enum SymbolKind
    {
        /// <summary>
        ///     Indicates that a symbol is a magic symbol, and not a part of
        ///     the language supported by an execution engine.
        /// </summary>
        Magic,

        /// <summary>
        ///     Indicates that a symbol represents a callable function,
        ///     operation, method, or similar.
        /// </summary>
        Callable,

        /// <summary>
        ///     Indicates that a symbol represents a local declaration,
        ///     variable, or similar.
        /// </summary>
        LocalDeclaration,

        /// <summary>
        ///     Indicates that a symbol is does not belong to any other kinds
        ///     listed in this enum.
        /// </summary>
        Other
    }

    /// <summary>
    ///     Documentation for a symbol as resolved by an execution engine.
    /// </summary>
    public struct Documentation
    {
        /// <summary>
        ///      Summary for the documented symbol. Should be at most a
        ///      sentence or two.
        /// </summary>
        public string Summary;

        [Obsolete("Deprecated, please break into more specific fields as appropriate.")]
        public string? Full;
        
        /// <summary>
        ///     A detailed description of the documented symbol, formatted as
        ///     a Markdown document.
        /// </summary>
        /// <remarks>
        ///     This Markdown document should not contain H1 or H2 headers.
        /// </remarks>
        public string? Description;

        /// <summary>
        ///     Additional remarks about the documented symbol, formatted as
        ///     a Markdown document.
        /// </summary>
        /// <remarks>
        ///     This Markdown document should not contain H1 or H2 headers.
        /// </remarks>
        public string? Remarks;

        /// <summary>
        ///     Examples of how to use the documented symbol, formatted as
        ///     a sequence of Markdown documents.
        /// </summary>
        /// <remarks>
        ///     The Markdown documents in this field should not contain H1 or
        ///     H2 headers.
        /// </remarks>
        public IEnumerable<string>? Examples;

        /// <summary>
        ///     Additional links relevant to the documented symbol, formatted
        ///     as a sequence of links, each with a description and a target.
        /// </summary>
        public IEnumerable<(string, Uri)>? SeeAlso;
    }

    /// <summary>
    ///      Represents a symbol that can be resolved by a symbol resolver,
    ///      such as a magic symbol, a completion result, or so forth.
    /// </summary>
    public interface ISymbol
    {
        /// <summary>
        ///     The name of the resolved symbol.
        /// </summary>
        string Name { get; }

        /// <summary>
        ///      The kind of the resolved symbol (e.g. is it a magic command,
        ///      a local variable, a function, etc.).
        /// </summary>
        SymbolKind Kind { get; }
    }

    /// <summary>
    ///      A service that can be used to resolve symbols from symbol names.
    /// </summary>
    public interface ISymbolResolver
    {
        /// <summary>
        ///      Resolves a global symbol.
        /// </summary>
        /// <returns>
        ///      The resolved symbol, or <c>null</c> if no such symbol can be
        ///      successfully resolved.
        /// </returns>
        ISymbol? Resolve(string symbolName);
    }

}
