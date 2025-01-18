namespace Threadlink.Editor
{
	using System.Collections.Generic;
	using UnityEditor;
	using UnityEngine;

	internal sealed class HiddenObjectExplorer : EditorWindow
	{
		readonly List<GameObject> m_Objects = new();
		Vector2 scrollPos = Vector2.zero;

		[MenuItem("Threadlink/Hidden Scene Objects Explorer")]
		static void Init() => GetWindow<HiddenObjectExplorer>();

		void OnEnable() => FindObjects();

		void FindObjects()
		{
			var objs = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
			int length = objs.Length;

			m_Objects.Clear();

			for (int i = 0; i < length; i++)
			{
				var go = objs[i].transform.root.gameObject;

				if (m_Objects.Contains(go) == false) m_Objects.Add(go);
			}
		}

		HideFlags HideFlagsButton(string aTitle, HideFlags aFlags, HideFlags aValue)
		{
			if (GUILayout.Toggle((aFlags & aValue) > 0, aTitle, "Button"))
				aFlags |= aValue;
			else
				aFlags &= ~aValue;

			return aFlags;
		}

		void OnGUI()
		{
			GUILayout.BeginHorizontal();

			if (GUILayout.Button("Find top-level")) FindObjects();

			if (GUILayout.Button("Find ALL objects"))
			{
				var objs = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];

				m_Objects.Clear();
				m_Objects.AddRange(objs);
			}

			GUILayout.EndHorizontal();

			scrollPos = GUILayout.BeginScrollView(scrollPos);

			for (int i = 0; i < m_Objects.Count; i++)
			{
				var go = m_Objects[i];

				if (go == null) continue;

				GUILayout.BeginHorizontal();

				EditorGUILayout.ObjectField(go.name, go, typeof(GameObject), true);

				var flags = go.hideFlags;

				flags = HideFlagsButton("HideInHierarchy", flags, HideFlags.HideInHierarchy);
				flags = HideFlagsButton("HideInInspector", flags, HideFlags.HideInInspector);
				flags = HideFlagsButton("DontSave", flags, HideFlags.DontSave);
				flags = HideFlagsButton("NotEditable", flags, HideFlags.NotEditable);

				go.hideFlags = flags;

				GUILayout.Label(((int)flags).ToString(), GUILayout.Width(20));
				GUILayout.Space(20);

				if (GUILayout.Button("DELETE"))
				{
					DestroyImmediate(go);
					FindObjects();
					GUIUtility.ExitGUI();
				}

				GUILayout.Space(20);
				GUILayout.EndHorizontal();
			}

			GUILayout.EndScrollView();
		}
	}
}