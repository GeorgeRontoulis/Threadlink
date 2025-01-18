namespace Threadlink.Core
{
	using UnityEngine;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	[CreateAssetMenu(fileName = "Threadlink User Data", menuName = "Threadlink/Preferences Asset", order = 999)]
	public class ThreadlinkPreferences : ScriptableObject
	{
		public enum CoreDeploymentMethod { Automatic, Manual }

		public CoreDeploymentMethod coreDeployment;

		[Space(10)]

		public string[] nativeSubSystems;

		[Space(10)]

		public string[] additionalNativeAssets;
	}
}
