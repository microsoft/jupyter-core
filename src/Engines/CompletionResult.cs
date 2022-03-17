// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Jupyter.Core
{

    // From the Jupyter messaging protocol documentation
    // at https://jupyter-client.readthedocs.io/en/stable/messaging.html#completion:
    //
    // content = {
    // # status should be 'ok' unless an exception was raised during the request,
    // # in which case it should be 'error', along with the usual error message content
    // # in other messages.
    // 'status' : 'ok'
    // 
    // # The list of all matches to the completion request, such as
    // # ['a.isalnum', 'a.isalpha'] for the above example.
    // 'matches' : list,
    // 
    // # The range of text that should be replaced by the above matches when a completion is accepted.
    // # typically cursor_end is the same as cursor_pos in the request.
    // 'cursor_start' : int,
    // 'cursor_end' : int,
    // 
    // # Information that frontend plugins might use for extra display information about completions.
    // 'metadata' : dict,
    // }

    /// <summary>
    ///     Represents a list of completion results returned by an execution
    ///     engine.
    /// </summary>
    public struct CompletionResult
    {
        public CompleteStatus Status;
        public IList<string>? Matches;
        public int? CursorStart;
        public int? CursorEnd;
        public IDictionary<string, object>? Metadata;

        public static CompletionResult Failed => new CompletionResult
        {
            Status = CompleteStatus.Ok,
            Matches = null,
            CursorStart = null,
            CursorEnd = null,
            Metadata = null
        };
    }

}
