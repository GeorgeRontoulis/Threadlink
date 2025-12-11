namespace Threadlink.Shared
{
    using System;
    using Core.NativeSubsystems.Scribe;

    public static class WeavingFactory<Object> where Object : IDiscardable, IIdentifiable
    {
        private const string FACTORY_NULL_MSG = "The Factory Method for this type is NULL!";

        #region Event Accessors:
        public static event Func<Object> OnCreate
        {
            add => Create += value;
            remove => Create -= value;
        }

        public static event Func<Object, Object> OnCreateFrom
        {
            add => CreateFrom += value;
            remove => CreateFrom -= value;
        }
        #endregion

        #region Delegates:
        private static Func<Object> Create { get; set; }
        private static Func<Object, Object> CreateFrom { get; set; }
        #endregion

        #region Public API:
        public static void Clear()
        {
            Create = null;
            CreateFrom = null;
        }

        public static bool TryCreate(out Object result)
        {
            if (Create != null)
            {
                result = Create();
                return true;
            }
            else Scribe.Send<Object>(FACTORY_NULL_MSG).ToUnityConsole(DebugType.Error);

            result = default;
            return false;
        }

        public static bool TryCreateFrom(Object original, out Object result)
        {
            result = default;

            if (original == null)
            {
                Scribe.Send<Object>(nameof(original), " should never be NULL!").ToUnityConsole(DebugType.Error);
                return false;
            }

            if (CreateFrom != null)
            {
                result = CreateFrom(original);
                return true;
            }
            else Scribe.Send<Object>(FACTORY_NULL_MSG).ToUnityConsole(DebugType.Error);

            return false;
        }
        #endregion
    }
}
