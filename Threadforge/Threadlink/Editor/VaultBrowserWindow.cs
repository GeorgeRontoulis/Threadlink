namespace Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using Theme = ThreadlinkEditorTheme;
    using VaultAsset = global::Threadlink.Vault.Vault;
    using UnityInspector = UnityEditor.Editor;

    /// <summary>
    /// Lists every <see cref="Vault"/> in the project, and inspects and edits the selected one.
    /// </summary>
    internal sealed class VaultBrowserWindow : EditorWindow
    {
        private sealed class Entry
        {
            public string Guid;
            public string Path;
            public string Name;
            public string Folder;
            public int FieldCount;
        }

        private const float RowHeight = 42f;
        private const float HeaderHeight = 34f;
        private const float FooterHeight = 24f;
        private const float MinListWidth = 180f;
        private const float MinInspectorWidth = 260f;
        private const float SplitterWidth = 5f;

        [SerializeField] private string selectedGuid = string.Empty;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private float listWidth = 260f;
        [SerializeField] private Vector2 listScroll;
        [SerializeField] private Vector2 inspectorScroll;

        private readonly List<Entry> entries = new(32);
        private readonly List<Entry> visible = new(32);

        private SearchField searchField;
        private UnityInspector inspector;
        private VaultAsset inspected;
        private bool listDirty = true;
        private bool resizing;
        private string pendingGuid;

        private GUIStyle nameStyle, folderStyle, countStyle, titleStyle, pathStyle, emptyStyle, footerStyle;

        [MenuItem("Threadlink/Vault Browser", priority = 20)]
        internal static void Open() => Show(null);

        [MenuItem("Assets/Threadlink/Open in Vault Browser", priority = 2000)]
        private static void OpenSelected() => Show(Selection.activeObject as VaultAsset);

        [MenuItem("Assets/Threadlink/Open in Vault Browser", true)]
        private static bool CanOpenSelected() => Selection.activeObject is VaultAsset;

        [MenuItem("CONTEXT/Vault/Open in Vault Browser")]
        private static void OpenFromInspector(MenuCommand command) => Show(command.context as VaultAsset);

        private static void Show(VaultAsset vault)
        {
            var window = GetWindow<VaultBrowserWindow>();
            window.titleContent = new GUIContent("Vault Browser", EditorGUIUtility.IconContent("ScriptableObject Icon").image);
            window.minSize = new Vector2(MinListWidth + MinInspectorWidth, 280f);

            if (vault != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(vault, out string guid, out long _))
                window.Select(guid);

            window.Show();
        }

        #region Lifecycle:
        private void OnEnable()
        {
            searchField = new SearchField();
            EditorApplication.projectChanged += MarkDirty;
            Undo.undoRedoPerformed += Repaint;
            wantsMouseMove = true;
            listDirty = true;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= MarkDirty;
            Undo.undoRedoPerformed -= Repaint;
            DestroyInspector();
        }

        private void MarkDirty()
        {
            listDirty = true;
            Repaint();
        }

        private void DestroyInspector()
        {
            if (inspector != null)
                DestroyImmediate(inspector);

            inspector = null;
            inspected = null;
        }
        #endregion

        #region Data:
        private void RefreshEntries()
        {
            listDirty = false;
            entries.Clear();

            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(VaultAsset).Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<VaultAsset>(path) is not VaultAsset vault)
                    continue;

                entries.Add(new Entry
                {
                    Guid = guid,
                    Path = path,
                    Name = vault.name,
                    Folder = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty,
                    FieldCount = CountFields(vault),
                });
            }

            entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Filter();
        }

        private static int CountFields(VaultAsset vault)
        {
            using var serializedVault = new SerializedObject(vault);
            var count = serializedVault.FindProperty("dataFields.count");

            return count != null ? count.intValue : 0;
        }

        private void Filter()
        {
            visible.Clear();

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(search)
                || entry.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                || entry.Folder.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    visible.Add(entry);
            }
        }

        private Entry Selected => entries.Find(entry => entry.Guid == selectedGuid);

        private void Select(string guid)
        {
            if (selectedGuid == guid)
                return;

            selectedGuid = guid;
            inspectorScroll = Vector2.zero;
            GUIUtility.keyboardControl = 0;
            Repaint();
        }

        private VaultAsset ResolveSelectedVault()
        {
            var entry = Selected;
            var vault = entry != null ? AssetDatabase.LoadAssetAtPath<VaultAsset>(entry.Path) : null;

            if (vault != inspected)
            {
                DestroyInspector();

                if (vault != null)
                {
                    inspector = UnityInspector.CreateEditor(vault);
                    inspected = vault;
                }
            }

            return vault;
        }
        #endregion

        #region GUI:
        private void BuildStyles()
        {
            if (nameStyle != null)
                return;

            nameStyle = Theme.Label(12, FontStyle.Bold);
            folderStyle = Theme.Label(10);
            countStyle = Theme.Label(10, FontStyle.Bold, TextAnchor.MiddleCenter);
            titleStyle = Theme.Label(17, FontStyle.Bold);
            pathStyle = Theme.Label(10);
            emptyStyle = Theme.Label(12, FontStyle.Normal, TextAnchor.MiddleCenter);
            footerStyle = Theme.Label(10, FontStyle.Normal, TextAnchor.MiddleLeft);
        }

        private void OnGUI()
        {
            BuildStyles();

            if (listDirty)
                RefreshEntries();

            listWidth = Mathf.Clamp(listWidth, MinListWidth, Mathf.Max(MinListWidth, position.width - MinInspectorWidth));

            var listRect = new Rect(0f, 0f, listWidth, position.height);
            var splitterRect = new Rect(listWidth - 2f, 0f, SplitterWidth, position.height);
            var inspectorRect = new Rect(listWidth + 1f, 0f, position.width - listWidth - 1f, position.height);

            HandleSplitter(splitterRect);
            DrawList(listRect);
            DrawInspector(inspectorRect);
            HandleKeyboard();

            // Selection changes from this event apply after drawing, so the inspector drawn matches its Layout pass.
            if (pendingGuid != null)
            {
                Select(pendingGuid);
                pendingGuid = null;
            }

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void DrawList(Rect rect)
        {
            Theme.Fill(rect, Theme.Pane);
            Theme.Fill(new Rect(rect.xMax, 0f, 1f, rect.height), Theme.Divider);

            // Search and create.
            var header = new Rect(rect.x, rect.y, rect.width, HeaderHeight);
            var searchRect = new Rect(header.x + 8f, header.y + 8f, header.width - 44f, 18f);
            var addRect = new Rect(header.xMax - 30f, header.y + 7f, 22f, 20f);

            EditorGUI.BeginChangeCheck();
            search = searchField.OnGUI(searchRect, search);

            if (EditorGUI.EndChangeCheck())
                Filter();

            if (GUI.Button(addRect, new GUIContent(EditorGUIUtility.IconContent("Toolbar Plus").image, "Create a Vault"), EditorStyles.iconButton))
            {
                CreateVault();
                GUIUtility.ExitGUI();
            }

            Theme.Fill(new Rect(rect.x, header.yMax - 1f, rect.width, 1f), Theme.Divider);

            // Rows.
            var viewRect = new Rect(rect.x, header.yMax, rect.width, rect.height - HeaderHeight - FooterHeight);
            var contentRect = new Rect(0f, 0f, viewRect.width - (visible.Count * RowHeight > viewRect.height ? 13f : 0f), visible.Count * RowHeight);

            listScroll = GUI.BeginScrollView(viewRect, listScroll, contentRect, false, false, GUIStyle.none, GUI.skin.verticalScrollbar);

            int first = Mathf.Max(0, Mathf.FloorToInt(listScroll.y / RowHeight));
            int last = Mathf.Min(visible.Count, first + Mathf.CeilToInt(viewRect.height / RowHeight) + 1);

            for (int i = first; i < last; i++)
                DrawRow(new Rect(0f, i * RowHeight, contentRect.width, RowHeight), visible[i]);

            GUI.EndScrollView();

            if (visible.Count == 0)
            {
                string message = entries.Count == 0 ? "No Vaults yet" : "No matches";
                Theme.Draw(new Rect(viewRect.x, viewRect.y + 24f, viewRect.width, 20f), message, emptyStyle, Theme.TextFaint);
            }

            // Footer.
            var footer = new Rect(rect.x, rect.yMax - FooterHeight, rect.width, FooterHeight);
            Theme.Fill(new Rect(footer.x, footer.y, footer.width, 1f), Theme.Divider);

            string summary = visible.Count == entries.Count ? $"{entries.Count} Vaults" : $"{visible.Count} of {entries.Count} Vaults";
            Theme.Draw(new Rect(footer.x + 10f, footer.y, footer.width - 40f, footer.height), summary, footerStyle, Theme.TextFaint);

            if (GUI.Button(new Rect(footer.xMax - 28f, footer.y + 3f, 20f, 18f), new GUIContent(EditorGUIUtility.IconContent("Refresh").image, "Rescan the project"), EditorStyles.iconButton))
                MarkDirty();
        }

        private void DrawRow(Rect row, Entry entry)
        {
            var current = Event.current;
            bool selected = entry.Guid == selectedGuid;
            bool hovered = row.Contains(current.mousePosition);

            if (selected)
            {
                Theme.Fill(row, Theme.Selection);
                Theme.Fill(new Rect(row.x, row.y, 3f, row.height), Theme.Accent);
            }
            else if (hovered)
            {
                Theme.Fill(row, Theme.Hover);
            }

            // Field count pill.
            string count = entry.FieldCount.ToString();
            float pillWidth = Mathf.Max(22f, Theme.Measure(count, countStyle).x + 12f);
            var pill = new Rect(row.xMax - pillWidth - 10f, row.y + (row.height - 18f) * 0.5f, pillWidth, 18f);

            Theme.FillRounded(pill, selected ? Theme.WithAlpha(Theme.Accent, 0.3f) : Theme.Hover, 9f);
            Theme.Draw(pill, count, countStyle, selected ? Theme.Text : Theme.TextDim);

            float textWidth = pill.x - row.x - 24f;

            Theme.Draw(new Rect(row.x + 14f, row.y + 6f, textWidth, 16f), Theme.Fit(entry.Name, nameStyle, textWidth), nameStyle, Theme.Text);
            Theme.Draw(new Rect(row.x + 14f, row.y + 22f, textWidth, 14f), Theme.Fit(entry.Folder, folderStyle, textWidth), folderStyle, Theme.TextFaint);

            if (current.type == EventType.MouseDown && hovered)
            {
                if (current.button == 0)
                {
                    pendingGuid = entry.Guid;

                    if (current.clickCount == 2)
                        PingEntry(entry);

                    current.Use();
                }
                else if (current.button == 1)
                {
                    pendingGuid = entry.Guid;
                    ShowEntryMenu(entry);
                    current.Use();
                }
            }
        }

        private void DrawInspector(Rect rect)
        {
            var entry = Selected;
            var vault = ResolveSelectedVault();

            if (entry == null || vault == null || inspector == null)
            {
                string message = entries.Count == 0 ? "Create a Vault with the + button." : "Select a Vault to inspect it.";
                Theme.Draw(rect, message, emptyStyle, Theme.TextFaint);
                return;
            }

            // Header: name, path and actions.
            var header = new Rect(rect.x, rect.y, rect.width, 58f);
            Theme.Fill(header, Theme.Surface);
            Theme.Fill(new Rect(header.x, header.yMax - 1f, header.width, 1f), Theme.Divider);

            var icon = AssetPreview.GetMiniThumbnail(vault);
            if (icon != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(header.x + 14f, header.y + 15f, 28f, 28f), icon, ScaleMode.ScaleToFit);

            var actions = new Rect(header.xMax - 96f, header.y + 17f, 84f, 24f);
            float textWidth = actions.x - header.x - 64f;

            Theme.Draw(new Rect(header.x + 52f, header.y + 9f, textWidth, 24f), Theme.Fit(vault.name, titleStyle, textWidth), titleStyle, Theme.Text);

            string subtitle = entry.Path;
            if (Application.isPlaying)
                subtitle = "Play Mode: edits to this asset persist  ·  " + subtitle;

            Theme.Draw(new Rect(header.x + 52f, header.y + 33f, textWidth, 16f), Theme.Fit(subtitle, pathStyle, textWidth), pathStyle,
                Application.isPlaying ? Theme.Warning : Theme.TextFaint);

            if (GUI.Button(new Rect(actions.x, actions.y, 54f, actions.height), "Ping", EditorStyles.miniButtonLeft))
                PingEntry(entry);

            if (GUI.Button(new Rect(actions.x + 54f, actions.y, 30f, actions.height), new GUIContent("⋯", "More"), EditorStyles.miniButtonRight))
                ShowEntryMenu(entry);

            // The Vault's own inspector, so Odin and custom drawers apply and edits support undo.
            var body = new Rect(rect.x, header.yMax, rect.width, rect.height - header.height);

            GUILayout.BeginArea(body);
            inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll);
            GUILayout.Space(10f);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            EditorGUILayout.BeginVertical();

            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(body.width * 0.35f, 120f, 220f);

            EditorGUI.BeginChangeCheck();
            inspector.OnInspectorGUI();

            if (EditorGUI.EndChangeCheck())
                entry.FieldCount = CountFields(vault);

            EditorGUIUtility.labelWidth = labelWidth;

            EditorGUILayout.EndVertical();
            GUILayout.Space(10f);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12f);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void HandleSplitter(Rect rect)
        {
            var current = Event.current;
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            switch (current.type)
            {
                case EventType.MouseDown when current.button == 0 && rect.Contains(current.mousePosition):
                    resizing = true;
                    current.Use();
                    break;

                case EventType.MouseDrag when resizing:
                    listWidth = current.mousePosition.x;
                    current.Use();
                    Repaint();
                    break;

                case EventType.MouseUp when resizing:
                    resizing = false;
                    current.Use();
                    break;
            }
        }

        /// <summary>Up and down move the selection while no text field has focus.</summary>
        private void HandleKeyboard()
        {
            var current = Event.current;

            if (current.type != EventType.KeyDown || GUIUtility.keyboardControl != 0 || visible.Count == 0)
                return;

            int step = current.keyCode switch
            {
                KeyCode.UpArrow => -1,
                KeyCode.DownArrow => 1,
                _ => 0,
            };

            if (step == 0)
                return;

            int index = visible.FindIndex(entry => entry.Guid == selectedGuid);
            index = Mathf.Clamp(index < 0 ? 0 : index + step, 0, visible.Count - 1);

            pendingGuid = visible[index].Guid;

            // Keep the selection in view.
            float top = index * RowHeight;
            float viewHeight = position.height - HeaderHeight - FooterHeight;

            if (top < listScroll.y)
                listScroll.y = top;
            else if (top + RowHeight > listScroll.y + viewHeight)
                listScroll.y = top + RowHeight - viewHeight;

            current.Use();
        }
        #endregion

        #region Actions:
        private static void PingEntry(Entry entry)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VaultAsset>(entry.Path);

            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        private void ShowEntryMenu(Entry entry)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Ping in Project"), false, () => PingEntry(entry));
            menu.AddItem(new GUIContent("Select in Project"), false, () => Selection.activeObject = AssetDatabase.LoadAssetAtPath<VaultAsset>(entry.Path));
            menu.AddItem(new GUIContent("Reveal in File Browser"), false, () => EditorUtility.RevealInFinder(entry.Path));
            menu.AddItem(new GUIContent("Copy Path"), false, () => EditorGUIUtility.systemCopyBuffer = entry.Path);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateEntry(entry));
            menu.AddItem(new GUIContent("Delete…"), false, () => DeleteEntry(entry));
            menu.ShowAsContext();
        }

        private void CreateVault()
        {
            string folder = Selected?.Folder;

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                folder = "Assets";

            string path = EditorUtility.SaveFilePanelInProject("Create Vault", "New Vault", "asset", "Choose where to save the new Vault.", folder);

            if (string.IsNullOrEmpty(path))
                return;

            var vault = CreateInstance<VaultAsset>();
            AssetDatabase.CreateAsset(vault, path);
            AssetDatabase.SaveAssets();

            search = string.Empty;
            RefreshEntries();
            Select(AssetDatabase.AssetPathToGUID(path));
        }

        private void DuplicateEntry(Entry entry)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(entry.Path);

            if (!AssetDatabase.CopyAsset(entry.Path, path))
                return;

            RefreshEntries();
            Select(AssetDatabase.AssetPathToGUID(path));
        }

        private void DeleteEntry(Entry entry)
        {
            if (!EditorUtility.DisplayDialog("Delete Vault", $"Delete '{entry.Name}'?\n\n{entry.Path}", "Delete", "Cancel"))
                return;

            if (!AssetDatabase.MoveAssetToTrash(entry.Path))
                return;

            if (selectedGuid == entry.Guid)
                selectedGuid = string.Empty;

            RefreshEntries();
        }
        #endregion
    }
}
