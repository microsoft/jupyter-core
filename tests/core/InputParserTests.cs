using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Jupyter.Core
{
    internal class MockSymbolResolver : ISymbolResolver
    {
        private string[] magicSymbols = new[] { "%abc", "%def" };

        public ISymbol Resolve(string symbolName)
        {
            if (this.magicSymbols.Contains(symbolName))
            {
                return new MagicSymbol
                {
                    Name = symbolName,
                    Documentation = new Documentation(),
                    Kind = SymbolKind.Magic,
                    Execute = async (input, channel) => { return ExecutionResult.Aborted; }
                };
            }

            return null;
        }
    }

    [TestClass]
    public class InputParserTests
    {
        [TestMethod]
        public void TestEmptyInput()
        {
            var input = string.Empty;

            var inputParser = new InputParser(new MockSymbolResolver());
            var isMagicOrHelp = inputParser.IsMagicOrHelp(input, out ISymbol symbol, out string commandInput, out bool isHelp, out string remainingInput);
            
            Assert.IsFalse(isMagicOrHelp);
            Assert.IsNull(symbol);
            Assert.IsFalse(isHelp);
            Assert.AreEqual(commandInput, string.Empty);
            Assert.AreEqual(remainingInput, string.Empty);
        }

        [TestMethod]
        public void TestInvalidMagic()
        {
            var inputs = new[] { 
                "%notamagic",            // invalid magic
                "% abc",                 // magic with space
                "text %abc",             // magic not at the beginning of a line
                "%abc%def",              // magic without trailing whitespace
            };

            foreach (var input in inputs)
            {
                var inputParser = new InputParser(new MockSymbolResolver());
                var isMagicOrHelp = inputParser.IsMagicOrHelp(input, out ISymbol symbol, out string commandInput, out bool isHelp, out string remainingInput);
                
                Assert.IsFalse(isMagicOrHelp, $"Input:\n{input}");
                Assert.IsNull(symbol, $"Input:\n{input}");
                Assert.IsFalse(isHelp, $"Input:\n{input}");
                Assert.AreEqual(commandInput, input, $"Input:\n{input}");
                Assert.AreEqual(remainingInput, string.Empty, $"Input:\n{input}");
            }
        }

        [TestMethod]
        public void TestSingleMagic()
        {
            var inputs = new[] { 
                "%abc",                            // simple case
                "%abc arg",                        // simple case with argument
                "%abc ?",                          // simple case with argument, not help because of whitespace
                " \t %abc",                        // leading whitespace should have no impact
                " \r\n %def \n arg ",              // leading line breaks should have no impact
                "%def arg \n\t %notamagic arg",    // invalid magic should be treated as plain text
                "%abc arg %def arg",               // magic in middle of line should not be detected
            };

            foreach (var input in inputs)
            {
                var inputParser = new InputParser(new MockSymbolResolver());
                var isMagicOrHelp = inputParser.IsMagicOrHelp(input, out ISymbol symbol, out string commandInput, out bool isHelp, out string remainingInput);
                
                Assert.IsTrue(isMagicOrHelp, $"Input:\n{input}");
                Assert.IsNotNull(symbol, $"Input:\n{input}");
                Assert.IsFalse(isHelp, $"Input:\n{input}");
                Assert.AreEqual(commandInput, input, $"Input:\n{input}");
                Assert.AreEqual(remainingInput, string.Empty, $"Input:\n{input}");
            }
        }

        [TestMethod]
        public void TestSingleHelp()
        {
            var inputs = new[] { 
                "?%abc",                    // leading ?
                "%abc?",                    // trailing ?
                " \r\n  %abc? arg \n",      // leading whitespace and trailing text
            };

            foreach (var input in inputs)
            {
                var inputParser = new InputParser(new MockSymbolResolver());
                var isMagicOrHelp = inputParser.IsMagicOrHelp(input, out ISymbol symbol, out string commandInput, out bool isHelp, out string remainingInput);
                
                Assert.IsTrue(isMagicOrHelp, $"Input:\n{input}");
                Assert.IsNotNull(symbol, $"Input:\n{input}");
                Assert.IsTrue(isHelp, $"Input:\n{input}");
                Assert.AreEqual(commandInput, input, $"Input:\n{input}");
                Assert.AreEqual(remainingInput, string.Empty, $"Input:\n{input}");
            }
        }

        [TestMethod]
        public void TestMultipleMagics()
        {
            var inputParser = new InputParser(new MockSymbolResolver());

            // simple case
            var input = "%abc\n%def";
            var isMagicOrHelp = inputParser.IsMagicOrHelp(input, out ISymbol symbol, out string commandInput, out bool isHelp, out string remainingInput);
                
            Assert.IsTrue(isMagicOrHelp, $"Input:\n{input}");
            Assert.IsNotNull(symbol, $"Input:\n{input}");
            Assert.IsFalse(isHelp, $"Input:\n{input}");
            Assert.AreEqual(commandInput, "%abc", $"Input:\n{input}");
            Assert.AreEqual(remainingInput, "%def", $"Input:\n{input}");

            // simple case with help
            input = "%abc?\n%def";
            isMagicOrHelp = inputParser.IsMagicOrHelp(input, out symbol, out commandInput, out isHelp, out remainingInput);
                
            Assert.IsTrue(isMagicOrHelp, $"Input:\n{input}");
            Assert.IsNotNull(symbol, $"Input:\n{input}");
            Assert.IsTrue(isHelp, $"Input:\n{input}");
            Assert.AreEqual(commandInput, "%abc?", $"Input:\n{input}");
            Assert.AreEqual(remainingInput, "%def", $"Input:\n{input}");

            // multi-line args and extra whitespace
            input = " \n  %abc \r\n arg1 \n\t arg2  \r\n  \t  %def arg3 arg4";
            isMagicOrHelp = inputParser.IsMagicOrHelp(input, out symbol, out commandInput, out isHelp, out remainingInput);
                
            Assert.IsTrue(isMagicOrHelp, $"Input:\n{input}");
            Assert.IsNotNull(symbol, $"Input:\n{input}");
            Assert.IsFalse(isHelp, $"Input:\n{input}");
            Assert.IsTrue(commandInput.TrimStart().StartsWith("%abc"), $"Input:\n{input}");
            Assert.IsTrue(remainingInput.TrimStart().StartsWith("%def"), $"Input:\n{input}");
        }
    }
}
