namespace Threadlink.Shared
{
    using Core.NativeSubsystems.Scribe;
    using System;

    public static class WeavingFactory<Object> where Object : IDiscardable, IIdentifiable
    {
        private const string FACTORY_NULL_MSG = "The Factory Method for this type is NULL!";

        #region Event Accessors:
        public static event Func<Object> OnCreate
        {
            add => Create = value;
            remove => Create = null;
        }

        public static event Func<Object, Object> OnCreateFrom
        {
            add => CreateFrom = value;
            remove => CreateFrom = null;
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

        /// <summary>
        /// Attempt to create a new object of type <typeparamref name="Object"/>.
        /// </summary>
        /// <param name="result">The newly created object.</param>
        /// <returns><see langword="true"/> if the object was successfully created. <see langword="false"/> otherwise.</returns>
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

        /// <summary>
        /// Attempt to create a clone of the original <typeparamref name="Object"/>.
        /// </summary>
        /// <param name="original">The original object to clone.</param>
        /// <param name="result">The newly created clone.</param>
        /// <returns><see langword="true"/> if the clone was successfully created. <see langword="false"/> otherwise.</returns>
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
