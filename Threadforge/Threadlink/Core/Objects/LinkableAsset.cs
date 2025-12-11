namespace Threadlink.Core
{
    using NativeSubsystems.Scribe;
    using Shared;
    using System;
    using UnityEngine;

    /// <summary>
    /// Base class for all Threadlink-Compatible assets.
    /// </summary>
    public abstract class LinkableAsset : ScriptableObject, IDiscardable, IIdentifiable, INamable
    {
        public virtual int ID => GetInstanceID();
        public virtual string Name => name;

        public bool IsInstance { get; internal set; }

        public event Action OnDiscard = null;

        public virtual void Discard()
        {
            if (OnDiscard != null)
            {
                OnDiscard.Invoke();
                OnDiscard = null;
            }

            if (IsInstance) Destroy(this);
        }

        public static bool TryCreate<T>(string assetName, out T result) where T : LinkableAsset
        {
            if (string.IsNullOrEmpty(assetName))
            {
                Scribe.Send<T>("A ", nameof(LinkableAsset), "'s name cannot be NULL or empty!").ToUnityConsole(DebugType.Error);
                result = null;
                return false;
            }

            var output = CreateInstance<T>();

            output.name = assetName;
            output.IsInstance = true;

            result = output;
            return true;
        }
    }
}
