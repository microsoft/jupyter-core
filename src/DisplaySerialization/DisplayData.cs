using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.Jupyter.Core
{

    public struct DisplayData
    {
        public Dictionary<string, string> Data;
        public Dictionary<string, string> Metadata;

        public static DisplayData Empty() => new DisplayData
        {
            Data = new Dictionary<string, string>(),
            Metadata = new Dictionary<string, string>()
        };
    }

    public interface IDisplaySerializer
    {
        DisplayData? Serialize(object displayable);
    }

}
