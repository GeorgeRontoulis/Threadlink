namespace Threadlink.Extensions.Nexus
{
	using Cysharp.Threading.Tasks;
	using Threadlink.Core;

	public enum PlayerLoadingAction { None, Load, Unload }

	public abstract class PlayerLoaderExtension : LinkableAsset
	{
		public bool PlayerIsLoaded { get; protected set; }

		public abstract UniTask LoadPlayerAndDependeciesAsync();
		public abstract void Unload();
	}
}