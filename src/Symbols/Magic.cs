using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{

    /// <summary>
    ///      Marks that a given method implements the given magic command.
    /// </summary>
    /// <remarks>
    ///      Each magic command method must have the signature
    ///      <c>Task&lt;ExecutionResult&gt; (string, IChannel)</c>, similar to
    ///      <ref>BaseEngine.ExecuteMundane</ref>.
    /// </remarks>
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class MagicCommandAttribute : System.Attribute
    {
        /// <summary>
        ///      Name of the magic command represented by this method.
        /// </summary>
        public readonly string Name;
        
        /// <summary>
        ///     Documentation to be presented to the user in repsonse to a
        ///     help command.
        /// </summary>
        public readonly Documentation Documentation;

        /// <summary>
        ///      Constructs a new attribute that marks a given method as
        ///      implementing a given magic command.
        /// </summary>
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
        /// <inheritdoc />
        public string Name { get; set; }
        
        /// <inheritdoc />
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
        public Func<string, IChannel, Task<ExecutionResult>> Execute { get; set; }
    }

    /// <summary>
    ///      A symbol representing a magic command that is cancellable.
    /// </summary>
    public class CancellableMagicSymbol : MagicSymbol
    {
        /// <summary>
        ///     Creates a cancellable magic symbol object.
        /// </summary>
        public CancellableMagicSymbol() =>
            this.Execute = (input, channel) => this.ExecuteCancellable(input, channel, CancellationToken.None);

        /// <summary>
        ///      A function to be run when the magic command is executed by the
        ///      user which supports cancellation.
        /// </summary>
        [JsonIgnore]
        public Func<string, IChannel, CancellationToken, Task<ExecutionResult>> ExecuteCancellable { get; set; }
    }

    /// <summary>
    ///      A symbol resolver that uses <see cref="MagicCommandAttribute" />
    ///      attributes to find magic commands in a given engine class.
    /// </summary>
    public class MagicCommandResolver : ISymbolResolver
    {
        private IExecutionEngine engine;
        private IDictionary<string, (MagicCommandAttribute, MethodInfo)> methods;

        /// <summary>
        ///      Constructs a new resolver by searching a given engine for
        ///      methods annotated with the <see cref="MagicCommandAttribute" />
        ///      attribute.
        /// </summary>
        /// <param name="engine">
        ///      The execution engine to be searched for magic command methods.
        /// </param>
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

        /// <inheritdoc />
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
                        {
                            try
                            {
                                return (Task<ExecutionResult>)(method.Invoke(engine, new object[] { input, channel }));
                            } 
                            catch (TargetInvocationException e)
                            {
                                throw e.InnerException;
                            }
                            catch (Exception)
                            {
                                throw new InvalidOperationException($"Invalid magic method for {symbolName}. Expecting a public async method that takes a String and and IChannel as parameters.");
                            }
                        }
                };
            }
            else return null;
        }
    }
}
