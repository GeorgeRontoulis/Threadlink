namespace Threadlink.Extensions.Nexus
{
	using System.Collections;
	using Threadlink.Core;

	public enum PlayerLoadingAction { None, Load, Unload }

	public abstract class BasePlayerLoaderExtension : LinkableAsset
	{
		public bool PlayerIsLoaded { get; protected set; }

		public abstract IEnumerator LoadingCoroutine();
		public abstract void Unload();
	}
}