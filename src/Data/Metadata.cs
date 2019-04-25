// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using Microsoft.Jupyter.Core.Protocol;
using System.IO;
using System.Linq;
using System.Reflection;

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

    /// <summary>
    ///     Specifies the metadata for a particular language kernel, including
    ///     details about the kernel, the language supported by the kernel,
    ///     and relevant version information.
    /// </summary>
    /// <example>
    ///     <code>
    ///         var properties = new KernelProperties
    ///         {
    ///             FriendlyName = "IEcho",
    ///             KernelName = "iecho",
    ///             KernelVersion = typeof(Program).Assembly.GetName().Version.ToString(),
    ///             DisplayName = "Echo",
    ///
    ///             LanguageName = "Echo",
    ///             LanguageVersion = "0.1",
    ///             LanguageMimeType = MimeTypes.PlainText,
    ///             LanguageFileExtension = ".txt",
    ///
    ///             Description = "A simple kernel that echos its input."
    ///         };
    ///     </code>
    /// </example>
    public class KernelProperties
    {
        /// <summary>
        ///     A user-friendly name for the kernel, typically used in menus.
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        ///     The name for the kernel, this name will be used to register the kernel with Jupyter
        ///     and to identify the kernel programmatically.
        /// </summary>
        public string KernelName { get; set; }

        /// <summary>
        ///     A string describing the version of the kernel.
        /// </summary>
        /// <remarks>
        ///     Note that this property does not refer to the version of the
        ///     language supported by the kernel.
        /// </remarks>
        public string KernelVersion { get; set; }

        public string DisplayName { get; set; }


        /// <summary>
        ///     The name of the language supported by the kernel.
        /// </summary>
        public string LanguageName { get; set; }
        /// <summary>
        ///      A string describing the version of the language supported by
        ///      the kernel.
        /// </summary>
        public string LanguageVersion { get; set; }

        /// <summary>
        ///      The MIME type used for source files in the supported language.
        /// </summary>
        /// <remarks>
        ///      This property is typically used by clients to offer exports of
        ///      notebooks as plain script or source files.
        /// </remarks>
        public string LanguageMimeType { get; set; }

        /// <summary>
        ///      The file extension used for source files in the supported
        ///      language.
        /// </summary>
        /// <remarks>
        ///      This property is typically used by clients to offer exports of
        ///      notebooks as plain script or source files.
        /// </remarks>
        public string LanguageFileExtension { get; set; }

        /// <summary>
        ///      An extended description of the kernel.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///      A collection of links to more information on this kernel and
        ///      its supported language.
        /// </summary>
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

        [JsonProperty("version")]
        public string LanguageVersion {get; set;}

        [JsonProperty("mimetype")]
        public string MimeType {get; set;}

        [JsonProperty("file_extension")]
        public string FileExtension {get; set;}
    }

}