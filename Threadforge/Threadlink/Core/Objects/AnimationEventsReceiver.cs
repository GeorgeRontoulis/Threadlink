namespace Threadlink.Animation
{
    using System;
    using Threadlink.Core;
    using Threadlink.Utilities.Objects;
    using Threadlink.Vault;
    using UnityEngine;

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class AnimationEventsReceiver : LinkableBehaviour
    {
        public event Action<Animator> OnVoidReceived = null;
        public event Action<Animator, Vault> OnVaultReceived = null;
        public event Action<Animator, UnityEngine.Object> OnUnityObjectReceived = null;
        public event Action<Animator, float> OnFloatReceived = null;
        public event Action<Animator, int> OnIntegerReceived = null;
        public event Action<Animator, string> OnStringReceived = null;
        public event Action<Animator, AnimationEvent> OnEventReceived = null;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ReadOnly]
#endif
        [SerializeField] private Animator animator = null;

        public override void Discard()
        {
            OnVoidReceived = null;
            OnVaultReceived = null;
            OnUnityObjectReceived = null;
            OnFloatReceived = null;
            OnIntegerReceived = null;
            OnStringReceived = null;
            OnEventReceived = null;
            animator = null;
            base.Discard();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            this.Set(ref animator);
        }
#endif

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveVoid() => OnVoidReceived?.Invoke(animator);

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveVault(UnityEngine.Object data)
        {
            OnUnityObjectReceived?.Invoke(animator, data);

            if (data is not Vault vault)
                return;

            OnVaultReceived?.Invoke(animator, vault);
        }

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveUnityObject(UnityEngine.Object data) => OnUnityObjectReceived?.Invoke(animator, data);

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveFloat(float data) => OnFloatReceived?.Invoke(animator, data);

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveInteger(int data) => OnIntegerReceived?.Invoke(animator, data);

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveString(string data) => OnStringReceived?.Invoke(animator, data);

        /// <summary>
        /// Called by animation events. Do not call manually. 
        /// </summary>
        /// <param name="data">The received event data.</param>
        public virtual void ReceiveEvent(AnimationEvent data) => OnEventReceived?.Invoke(animator, data);
    }
}
