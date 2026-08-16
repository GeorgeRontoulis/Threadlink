namespace Threadlink.Editor
{
    using System;
    using System.Runtime.CompilerServices;
    using UnityEditor;
    using UnityEngine;

    [Serializable]
    internal sealed class ThreadlinkDomainAssets
    {
        internal string DomainName
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => domainName;
        }

        internal string OutputFileName
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => outputFileName;
        }

        internal TextAsset Shell
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => shell;
        }

        internal TextAsset NativeEntries
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => nativeEntries;
        }

        internal bool IsOrdinal
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ordinalValues;
        }

        internal bool ReserveZero
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => reserveZero;
        }

        internal bool SourcedFromAddressables
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => sourcedFromAddressables;
        }

        [Tooltip("Injector files must be named after this, as {DomainName}.{Injector}.txt")]
        [SerializeField] private string domainName = string.Empty;

        [Tooltip("Name of the emitted script, without extension. Falls back to the domain name when empty.")]
        [SerializeField] private string outputFileName = string.Empty;

        [Space(5)]

        [SerializeField] private TextAsset shell = null;

        [Tooltip("Entries owned by the framework itself. Injectors are appended after these.")]
        [SerializeField] private TextAsset nativeEntries = null;

        [Space(5)]

        [Tooltip("Dense positional values instead of hashes. Only correct when the value is used as an array index.")]
        [SerializeField] private bool ordinalValues = false;

        [Tooltip("Whether the shell declares a sentinel at zero that the allocator must avoid.")]
        [SerializeField] private bool reserveZero = true;

        [Tooltip("Whether this domain also draws injectors from the Addressables Injectors Folder.")]
        [SerializeField] private bool sourcedFromAddressables = false;
    }

    [CreateAssetMenu(fileName = "ThreadlinkConfig.Editor.asset", menuName = "Threadlink/Editor Config")]
    internal sealed class ThreadlinkEditorConfig : ScriptableObject
    {
        internal ThreadlinkDomainAssets[] NativeDomains => nativeDomains;

        internal TextAsset DomainDefinitionShell => domainDefinitionShell;

        internal DefaultAsset GeneratedScriptsFolder => generatedScriptsFolder;
        internal DefaultAsset InjectorsFolder => injectorsFolder;
        internal DefaultAsset AddressablesInjectorsFolder => addressablesInjectorsFolder;
        internal DefaultAsset DomainDefinitionsFolder => domainDefinitionsFolder;
        internal DefaultAsset DomainDefinitionScriptsFolder => domainDefinitionScriptsFolder;

        [Header("Native Domains:")]
        [Space(10)]

        [Tooltip("Every generated framework enum is written here. Nothing outside this folder is ever touched.")]
        [SerializeField] private DefaultAsset generatedScriptsFolder = null;

        [Space(10)]

        [SerializeField] private ThreadlinkDomainAssets[] nativeDomains = Array.Empty<ThreadlinkDomainAssets>();

        [Header("Injectors:")]
        [Space(10)]

        [Tooltip("Files that append entries to an existing domain, named {DomainName}.{Injector}.txt. "
        + "The injector name becomes the scope of every entry in the file. Both your own entries and "
        + "any third-party module's entries live here.")]
        [SerializeField] private DefaultAsset injectorsFolder = null;

        [Tooltip("Injectors written by the Addressables Mapping Window, one per Addressable group. "
        + "Generated output: Apply overwrites this folder, so hand edits will be lost.")]
        [SerializeField] private DefaultAsset addressablesInjectorsFolder = null;

        [Header("Domain Definitions:")]
        [Space(10)]

        [Tooltip("Files that declare an entirely new enum, one file per domain. "
        + "Injectors may extend these too, exactly as they extend native domains.")]
        [SerializeField] private DefaultAsset domainDefinitionsFolder = null;

        [SerializeField] private DefaultAsset domainDefinitionScriptsFolder = null;

        [Space(5)]

        [SerializeField] private TextAsset domainDefinitionShell = null;
    }
}