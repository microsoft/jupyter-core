// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;
using Dotnet.Script.Core;

namespace Microsoft.Jupyter.Core
{
    public class ActionWriter : TextWriter
    {
        protected StringWriter buffer = new StringWriter();
        public override Encoding Encoding => Encoding.Default;
        public Action<string> Action { get; set; }
        public override void Write(char value)
        {
            buffer.Write(value);
            System.Console.Out.Write(value);
            if (value == '\n')
            {
                Flush();
            }
        }


        public override void Flush()
        {
            if (Action != null)
            {
                Action(buffer.ToString());
                var sb = buffer.GetStringBuilder();
                sb.Remove(0, sb.Length);
            }
        }
    }

    public class ChannelConsole : ScriptConsole
    {
        private IChannel channel = null;
        private StringWriter successWriter;
        public bool HadError { get; private set; } = false;
        public IChannel CurrentChannel {
            get => channel;

            set
            {
                channel = value;
                if (channel != null)
                {
                    successWriter = new StringWriter();
                    HadError = false;
                }
            }
        }
        public string Success => successWriter?.ToString();

        public ChannelConsole() : base(new ActionWriter(), null, new ActionWriter())
        {
            (Out as ActionWriter).Action = (value => channel?.Stdout(value));
            (Error as ActionWriter).Action = (value => channel?.Stderr(value));
        }
        // public ChannelConsole() : base(Console.Out, null, Console.Error) { }

        public override void WriteError(string value)
        {
            if (CurrentChannel != null)
            {
                HadError = true;
                CurrentChannel?.Stderr(value);
            }
            else
            {
                base.WriteError(value);
            }
        }
        public override void WriteNormal(string value)
        {
            CurrentChannel?.Stdout($"NORMAL {value}");
        }

        public override void WriteSuccess(string value)
        {
            System.Console.WriteLine($"success: {value}");
            if (successWriter != null)
            {
                successWriter.Write(value);
            }
            else
            {
                base.WriteSuccess(value);
            }
        }

    }
}
