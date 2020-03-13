using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{
    public interface IShellServer
    {
        event Action<Message> KernelInfoRequest;

        event Action<Message> ShutdownRequest;

        void SendShellMessage(Message message);

        void SendIoPubMessage(Message message);

        void Start();
    }
}
