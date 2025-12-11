namespace Threadlink.User
{
    using UnityEngine;

    internal sealed class UserWeavingFactory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            //User types here.
            //WeavingFactory<MySubsystem>.OnCreate += static () => new MySubsystem();
        }
    }
}
