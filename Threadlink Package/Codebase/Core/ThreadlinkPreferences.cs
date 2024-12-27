namespace Threadlink.Core
{
	using UnityEngine;

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
