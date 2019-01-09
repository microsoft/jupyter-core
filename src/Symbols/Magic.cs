using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{

    /// <summary>
    ///      Marks that a given method implements the given magic command.
    /// </summary>
    /// <remarks>
    ///      Each magic command method must have the signature
    ///      <c>ExecuteResult (string, IChannel)</c>, similar to
    ///      <ref>BaseEngine.ExecuteMundane</ref>.
    /// </remarks>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MagicCommandAttribute : System.Attribute
    {
        public readonly string Name;
        public readonly Documentation Documentation;

        public MagicCommandAttribute(
            string name,
            string summary,
            string fullDocumentation = null
        )
        {
            Name = name;
            Documentation = new Documentation
            {
                Full = fullDocumentation,
                Summary = summary
            };
        }
    }

    /// <summary>
    ///      A symbol representing a magic command.
    /// </summary>
    public class MagicSymbol : ISymbol
    {
        public string Name { get; set; }
        public SymbolKind Kind { get; set; }

        /// <summary>
        ///     Documentation about this magic command to be displayed to the user.
        /// </summary>
        public Documentation Documentation { get; set; }

        /// <summary>
        ///      A function to be run when the magic command is executed by the
        ///      user.
        /// </summary>
        [JsonIgnore]
        public Func<string, IChannel, ExecutionResult> Execute { get; set; }
    }

    /// <summary>
    ///      A symbol resolver that uses <see cref="MagicCommandAttribute" />
    ///      attributes to find magic commands in a given engine class.
    /// </summary>
    /// <param name="engine">
    ///      The execution engine to be searched for magic command methods.
    /// </param>
    public class MagicCommandResolver : ISymbolResolver
    {
        private IExecutionEngine engine;
        private IDictionary<string, (MagicCommandAttribute, MethodInfo)> methods;
        public MagicCommandResolver(IExecutionEngine engine)
        {
            this.engine = engine;
            methods = engine
                .GetType()
                .GetMethods()
                .Where(
                    method => method.GetCustomAttributes(typeof(MagicCommandAttribute), inherit: true).Length > 0
                )
                .Select(
                    method => {
                        var attr = (
                            (MagicCommandAttribute)
                            method
                            .GetCustomAttributes(typeof(MagicCommandAttribute), inherit: true)
                            .Single()
                        );
                        return (attr, method);
                    }
                )
                .ToImmutableDictionary(
                    pair => pair.attr.Name,
                    pair => (pair.attr, pair.method)
                );

        }

        public ISymbol Resolve(string symbolName)
        {
            if (this.methods.ContainsKey(symbolName))
            {
                (var attr, var method) = this.methods[symbolName];
                return new MagicSymbol
                {
                    Name = attr.Name,
                    Documentation = attr.Documentation,
                    Kind = SymbolKind.Magic,
                    Execute = (input, channel) =>
                        (ExecutionResult)(method.Invoke(engine, new object[] { input, channel }))
                };
            }
            else return null;
        }
    }
}
