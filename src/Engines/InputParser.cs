// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Jupyter.Core
{
    internal class InputParser
    {       
        internal enum CommandType
        {
            Mundane,
            Magic,
            Help,
            MagicHelp
        }

        public ISymbolResolver Resolver;
        
        public InputParser(ISymbolResolver resolver)
        {
            this.Resolver = resolver;
        }

        public CommandType GetNextCommand(string input, out ISymbol? symbol, out string? commandInput, out string? remainingInput)
        {
            if (input == null) { throw new ArgumentNullException("input"); }

            symbol = null;
            commandInput = input;
            remainingInput = string.Empty;

            var inputLines = input?.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            if (inputLines == null || inputLines.Count == 0)
            {
                return CommandType.Mundane;
            }

            // Find the first non-whitespace line and see if it starts with a magic symbol.
            bool isHelp = false;
            int firstLineIndex = inputLines.FindIndex(s => !string.IsNullOrWhiteSpace(s));
            if (firstLineIndex < 0 || !StartsWithMagic(inputLines[firstLineIndex], out symbol, out isHelp))
            {
                // No magic symbol found.
                return isHelp ? CommandType.Help : CommandType.Mundane;
            }

            // Look through the remaining lines until we find one that
            // starts with a magic symbol.
            commandInput = null;
            for (int lineIndex = firstLineIndex + 1; lineIndex < inputLines.Count; lineIndex++)
            {
                if (StartsWithMagic(inputLines[lineIndex], out _, out _))
                {
                    commandInput = string.Join(Environment.NewLine, inputLines.SkipLast(inputLines.Count - lineIndex));
                    remainingInput = string.Join(Environment.NewLine, inputLines.Skip(lineIndex));
                    break;
                }
            }

            // If we didn't find another magic symbol, use the full input
            // as the command input.
            if (commandInput == null)
            {
                commandInput = input;
            }
            
            return isHelp ? CommandType.MagicHelp : CommandType.Magic;
        }

        private bool StartsWithMagic(string input, out ISymbol? symbol, out bool isHelp)
        {
            symbol = null;
            isHelp = false;

            var inputParts = input.Trim().Split(null, 2);
            var symbolName = inputParts[0].Trim();
            if (symbolName.StartsWith("?"))
            {
                symbolName = symbolName.Substring(1, symbolName.Length - 1);
                isHelp = true;
            }
            else if (symbolName.EndsWith("?"))
            {
                symbolName = symbolName.Substring(0, symbolName.Length - 1);
                isHelp = true;
            }

            if (!string.IsNullOrEmpty(symbolName))
            {
                symbol = this.Resolver.Resolve(symbolName);
            }

            return (symbol as MagicSymbol) != null;
        }            
    }
}