namespace Threadlink.User
{
    using UnityEngine;

    internal static class UserWeavingFactory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            //User types here.
            //WeavingFactory<MyCustomSubsystem>.OnCreate += static () => new MyCustomSubsystem();
        }
    }
}
