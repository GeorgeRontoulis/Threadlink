namespace Threadlink.Utilities.Editor
{
#if UNITY_EDITOR
	using UnityEngine;
	using UnityEditor;
	using Threadlink.Utilities.Text;

	[CreateAssetMenu(menuName = "Threadlink/Editor/Dynamic Asset Path")]
	public sealed class DynamicEditorAssetPath : ScriptableObject
	{
		public string ProjectRelativePath => AssetDatabase.GetAssetPath(assetReference);
		public string AbsolutePath => TLZString.Construct(Application.dataPath.Replace("Assets", string.Empty), ProjectRelativePath);

		[SerializeField] private Object assetReference = null;
	}
#endif
}