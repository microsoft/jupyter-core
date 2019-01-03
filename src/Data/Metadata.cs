// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using Microsoft.Jupyter.Core.Protocol;

namespace Microsoft.Jupyter.Core
{

    [JsonObject(MemberSerialization.OptIn)]
    public class HelpLinks
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("url")]
        public Uri Url { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class KernelSpec
    {
        [JsonProperty("argv")]
        public List<string> Arguments;

        [JsonProperty("display_name")]
        public string DisplayName;

        [JsonProperty("language")]
        public string LanguageName;
    }

    public class KernelProperties
    {
        public string FriendlyName { get; set; }
        public string KernelName { get; set; }
        public string KernelVersion { get; set; }
        public string DisplayName { get; set; }

        public string LanguageName { get; set; }
        public string LanguageVersion { get; set; }
        public string LanguageMimeType { get; set; }
        public string LanguageFileExtension { get; set; }

        public string Description { get; set; }

        public HelpLinks[] HelpLinks { get; set; }

        public LanguageInfo AsLanguageInfo()
        {
            return new LanguageInfo
            {
                Name = LanguageName,
                LanguageVersion = LanguageVersion,
                MimeType = LanguageMimeType,
                FileExtension = LanguageFileExtension
            };
        }

        internal KernelInfoReplyContent AsKernelInfoReply()
        {
            return new KernelInfoReplyContent
            {
                Implementation = KernelName,
                ImplementationVersion = KernelVersion,
                LanguageInfo = AsLanguageInfo(),
                HelpLinks = HelpLinks
            };
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class LanguageInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        // FIXME: This also needs to be populated.
        // NB: This property refers to the version of the Q# language supported,
        //     and ★not★ to the version of the IQ# kernel supporting that
        //     language. In most cases, we expect that the two will agree,
        //     however.
        [JsonProperty("version")]
        public string LanguageVersion {get; set;}

        [JsonProperty("mimetype")]
        public string MimeType {get; set;}

        [JsonProperty("file_extension")]
        public string FileExtension {get; set;}
    }

}