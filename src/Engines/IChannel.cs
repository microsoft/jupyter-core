// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Jupyter.Core
{
    public interface IChannel
    {
        void Stdout(string message);
        void Stderr(string message);
        void Display(DisplayDataContent displayData);
    }
}