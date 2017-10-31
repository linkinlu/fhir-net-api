﻿/* 
 * Copyright (c) 2017, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://github.com/ewoutkramer/fhir-net-api/blob/master/LICENSE
 */

using Hl7.Fhir.Specification.Source.Summary;
using Hl7.Fhir.Utility;
using System;
using System.IO;
using System.Linq;

namespace Hl7.Fhir.Specification.Source
{
    // cf. SnapshotGeneratorSettings, ValidationSettings

    // Global design choices for API settings:
    // * Configuration setting classes are read/write
    //   This allows consumers to use (default) ctor and object initializer syntax
    //   Note: Read-only classes are not serializable, mutation is clumsy (via custom clone ctor)
    // * public default (parameterless) ctor creates instance with default settings
    // * public static CreateDefault() also returns a new instance with default settings
    // * Preferably, design properties such that default values are equal to false/null/0 etc.
    // * Deprecate old syntax using Obsolete & DebuggerHidden attributes
    // * Support cloning:
    //   - clone ctor T(T other)     = create new instance from existing instance
    //   - public T Clone()          = public method with strongly typed return value
    //   - object ICloneable.Clone() = explicit ICloneable interface implementation (DOTNETFW only)
    // * Christiaan: ASP.NET provides ctors with lambda argument to change internal config settings
    //   This way, caller does not obtain "ownership" of settings instance.


    /// <summary>Configuration settings for the <see cref="DirectorySource"/> class.</summary>
    public sealed class DirectorySourceSettings
#if DOTNETFW
        : ICloneable
#endif
    {
        /// <summary>Default value of the <see cref="FormatPreference"/> configuration setting.</summary>
        public const DirectorySource.DuplicateFilenameResolution DefaultFormatPreference = DirectorySource.DuplicateFilenameResolution.PreferXml;

        /// <summary>Default value of the <see cref="Masks"/> configuration setting.</summary>
        public readonly static string[] DefaultMasks = new[] { "*.*" };

        /// <summary>Creates a new <see cref="DirectorySourceSettings"/> instance with default property values.</summary>
        public static DirectorySourceSettings CreateDefault => new DirectorySourceSettings();

        /// <summary>Default constructor. Creates a new <see cref="DirectorySourceSettings"/> instance with default property values.</summary>
        public DirectorySourceSettings()
        {
            // See property declarations for default initializers
        }

        /// <summary>Clone constructor. Generates a new <see cref="DirectorySourceSettings"/> instance initialized from the state of the specified instance.</summary>
        public DirectorySourceSettings(DirectorySourceSettings settings)
        {
            if (settings == null) { throw Error.ArgumentNull(nameof(settings)); }
            settings.CopyTo(this);
        }

        /// <summary>Copy the current state to the specified instance.</summary>
        /// <param name="other">Another <see cref="DirectorySourceSettings"/> instance.</param>
        public void CopyTo(DirectorySourceSettings other)
        {
            if (other == null) { throw Error.ArgumentNull(nameof(other)); }
            other.IncludeSubDirectories = this.IncludeSubDirectories;
            other.Masks = this.Masks;
            other.Includes = this.Includes;
            other.Excludes = this.Excludes;
            other.FormatPreference = this.FormatPreference;
            other.SummaryDetailsHarvesters = this.SummaryDetailsHarvesters;
        }

        /// <summary>Returns an exact clone of the current configuration settings instance.</summary>
        public DirectorySourceSettings Clone() => new DirectorySourceSettings(this);

#if DOTNETFW
        /// <summary>Returns an exact clone of the current configuration settings instance.</summary>
        object ICloneable.Clone() => Clone();
#endif

        /// <summary>Returns the default content directory of the <see cref="DirectorySource"/>.</summary>
        public static string SpecificationDirectory
        {
            get
            {
#if DOTNETFW
                var codebase = AppDomain.CurrentDomain.BaseDirectory;
#else
                var codebase = AppContext.BaseDirectory;
#endif
                return Directory.Exists(codebase) ? codebase : Directory.GetCurrentDirectory();
            }
        }

        /// <summary>
        /// Gets or sets a value that determines wether the <see cref="DirectorySource"/> should
        /// also include artifacts from (nested) subdirectories of the specified content directory.
        /// <para>
        /// Returns <c>false</c> by default.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Take caution when enabling this setting, as it may potentially cause a
        /// <see cref="DirectorySource"/> instance to scan a (very) large number of files.
        /// Specifically, it is strongly advised NOT to enable this setting for:
        /// <list type="bullet">
        /// <item>
        /// <term>Directories with many deeply nested subdirectories</term>
        /// </item>
        /// <item>
        /// <term>Common folders such as Desktop, My Documents etc.</term>
        /// </item>
        /// <item>
        /// <term>Drive root folders, e.g. C:\</term>
        /// </item>
        /// </list>
        /// </remarks>
        public bool IncludeSubDirectories { get; set; } // = false;

        /// <summary>
        /// Determines if the <see cref="DirectorySource"/> instance should
        /// use only a single thread to harvest the artifact summary information.
        /// </summary>
        /// <remarks>
        /// By default, the <see cref="DirectorySource"/> leverages the thread pool
        /// to try and speed up the artifact summary generation process.
        /// Set this property to <c>true</c> to force single threaded processing.
        /// </remarks>
        public bool SingleThreaded { get; set; } // = false;

        /// <summary>
        /// Gets or sets the search string to match against the names of files in the content directory.
        /// The source will only provide resources from files that match the specified mask.
        /// The source will ignore all files that don't match the specified mask.
        /// Multiple masks can be split by '|'.
        /// <para>
        /// Returns <c>"*.*"</c> by default.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Mask filters are applied first, before any <see cref="Includes"/> and <see cref="Excludes"/> filters.
        /// </remarks>
        /// <value>
        /// Supported wildcards:
        /// <list type="bullet">
        /// <item>
        /// <term>*</term>
        /// <description>Matches zero or more characters within a file or directory name.</description>
        /// </item>
        /// <item>
        /// <term>?</term>
        /// <description>Matches any single character</description>
        /// </item>
        /// </list>
        /// </value>
        /// <example>
        /// <code>Mask = "v2*.*|*.StructureDefinition.*";</code>
        /// </example>
        public string Mask
        {
            get => String.Join("|", Masks);
            set { Masks = SplitMask(value); }
        }

        internal static string[] SplitMask(string mask) => mask?.Split('|').Select(s => s.Trim()).Where(s => !String.IsNullOrEmpty(s)).ToArray();

        /// <summary>
        /// Gets or sets an array of search strings to match against the names of files in the content directory.
        /// The source will only provide resources from files that match the specified mask.
        /// The source will ignore all files that don't match the specified mask.
        /// <para>
        /// Returns <c>{ "*.*" }</c> by default.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Mask filters are applied first, before <see cref="Includes"/> and <see cref="Excludes"/> filters.
        /// </remarks>
        /// <value>
        /// Supported wildcards:
        /// <list type="bullet">
        /// <item>
        /// <term>*</term>
        /// <description>Matches zero or more characters within a file or directory name.</description>
        /// </item>
        /// <item>
        /// <term>?</term>
        /// <description>Matches any single character</description>
        /// </item>
        /// </list>
        /// </value>
        /// <example>
        /// <code>Masks = new string[] { "v2*.*", "*.StructureDefinition.*" };</code>
        /// </example>
        public string[] Masks { get; set; } = DefaultMasks;

        /// <summary>
        /// Gets or sets an array of search strings to match against the names of subdirectories of the content directory.
        /// The source will only provide resources from subdirectories that match the specified include mask(s).
        /// The source will ignore all subdirectories that don't match the specified include mask(s).
        /// </summary>
        /// <remarks>
        /// Include filters are applied after <see cref="Mask"/> filters and before <see cref="Excludes"/> filters.
        /// </remarks>
        /// <value>
        /// Supported wildcards:
        /// <list type="bullet">
        /// <item>
        /// <term>*</term>
        /// <description>Matches zero or more characters within a directory name.</description>
        /// </item>
        /// <item>
        /// <term>**</term>
        /// <description>
        /// Recursive wildcard.
        /// For example, <c>/hello/**/*</c> matches all descendants of <c>/hello</c>.
        /// </description>
        /// </item>
        /// </list>
        /// </value>
        /// <example>
        /// <code>Includes = new string[] { "profiles/**/*", "**/valuesets" };</code>
        /// </example>
        public string[] Includes { get; set; }

        /// <summary>
        /// Gets or sets an array of search strings to match against the names of subdirectories of the content directory.
        /// The source will ignore all subdirectories that match the specified exclude mask(s).
        /// The source will only provide resources from subdirectories that don't match the specified exclude mask(s).
        /// </summary>
        /// <remarks>
        /// Exclude filters are applied last, after any <see cref="Mask"/> and <see cref="Includes"/> filters.
        /// </remarks>
        /// <value>
        /// Supported wildcards:
        /// <list type="bullet">
        /// <item>
        /// <term>*</term>
        /// <description>Matches zero or more characters within a directory name.</description>
        /// </item>
        /// <item>
        /// <term>**</term>
        /// <description>
        /// Recursive wildcard.
        /// For example, <c>/hello/**/*</c> matches all descendants of <c>/hello</c>.
        /// </description>
        /// </item>
        /// </list>
        /// </value>
        /// <example>
        /// <code>Excludes = new string[] { "profiles/**/old", "temp/**/*" };</code>
        /// </example>
        public string[] Excludes { get; set; }

        /// <summary>Gets or sets a value that determines how to process duplicate files with multiple serialization formats.<para>
        /// <remarks>The default value is <see cref="DirectorySource.DuplicateFilenameResolution.PreferXml"/>.</remarks>
        public DirectorySource.DuplicateFilenameResolution FormatPreference { get; set; } = DefaultFormatPreference;

        /// <summary>
        /// An array of <see cref="ArtifactSummaryHarvester"/> delegates for
        /// harvesting summary details from an artifact.
        /// </summary>
        /// <remarks>
        /// By default, the <see cref="ArtifactSummaryGenerator"/> calls all the default harvesters
        /// as defined by <see cref="ArtifactSummaryGenerator.DefaultArtifactSummaryHarvesters"/>.
        /// However if the caller specifies one or more summary harvester delegates, then the generator
        /// will call only the provided delegates in the specified order. The caller can also explicitly
        /// include one or more default harvester delegates in the specified list.
        /// </remarks>
        public ArtifactSummaryHarvester[] SummaryDetailsHarvesters { get; set; }
    }

}
