namespace Threadlink.Editor
{
    using UnityEditor;
    using UnityEngine;

    /// <summary>
    /// Shared drawing helpers and palette for Threadlink's editor windows.
    /// </summary>
    internal static class ThreadlinkEditorTheme
    {
        internal static bool Dark => EditorGUIUtility.isProSkin;

        internal static Color Accent => Dark ? new Color(0.35f, 0.62f, 1f) : new Color(0.16f, 0.42f, 0.86f);
        internal static Color Text => EditorStyles.label.normal.textColor;
        internal static Color TextDim => WithAlpha(Text, 0.6f);
        internal static Color TextFaint => WithAlpha(Text, 0.38f);
        internal static Color Pane => Dark ? new Color(0.155f, 0.155f, 0.155f) : new Color(0.86f, 0.86f, 0.86f);
        internal static Color Surface => Dark ? new Color(0.205f, 0.205f, 0.205f) : new Color(0.93f, 0.93f, 0.93f);
        internal static Color Divider => Dark ? new Color(0f, 0f, 0f, 0.45f) : new Color(0f, 0f, 0f, 0.14f);
        internal static Color Hover => Dark ? new Color(1f, 1f, 1f, 0.045f) : new Color(0f, 0f, 0f, 0.05f);
        internal static Color Selection => WithAlpha(Accent, Dark ? 0.22f : 0.18f);
        internal static Color Warning => new(0.96f, 0.69f, 0.25f);

        internal static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        internal static void Fill(Rect rect, Color color)
        {
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, color);
        }

        /// <summary>A filled rounded rectangle; a radius of half the height draws a pill, and of half a square's side, a circle.</summary>
        internal static void FillRounded(Rect rect, Color color, float radius)
        {
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
        }

        /// <summary>A rounded outline of <paramref name="width"/> pixels.</summary>
        internal static void StrokeRounded(Rect rect, Color color, float radius, float width)
        {
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, width, radius);
        }

        internal static Rect Circle(Vector2 centre, float radius) => new(centre.x - radius, centre.y - radius, radius * 2f, radius * 2f);

        internal static GUIStyle Label(int size, FontStyle fontStyle = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                fontStyle = fontStyle,
                alignment = anchor,
                clipping = TextClipping.Clip,
                wordWrap = false,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
        }

        private static readonly GUIContent TempContent = new();

        /// <summary>Draws <paramref name="text"/> in <paramref name="color"/> with <paramref name="style"/>, leaving the style unchanged.</summary>
        internal static void Draw(Rect rect, string text, GUIStyle style, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var normal = style.normal.textColor;

            TempContent.text = text;
            style.normal.textColor = color;
            style.Draw(rect, TempContent, false, false, false, false);
            style.normal.textColor = normal;
        }

        internal static Vector2 Measure(string text, GUIStyle style)
        {
            TempContent.text = text;
            return style.CalcSize(TempContent);
        }

        /// <summary><paramref name="text"/>, shortened with an ellipsis to fit <paramref name="width"/>.</summary>
        internal static string Fit(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || Measure(text, style).x <= width)
                return text;

            int low = 0, high = text.Length;

            while (low < high)
            {
                int mid = (low + high + 1) / 2;

                if (Measure(text.Substring(0, mid) + "…", style).x <= width)
                    low = mid;
                else
                    high = mid - 1;
            }

            return low <= 0 ? "…" : text.Substring(0, low) + "…";
        }

        internal static string Bytes(long bytes)
        {
            const double KB = 1024d, MB = KB * 1024d, GB = MB * 1024d;

            if (bytes < 0) return "—";
            if (bytes >= GB) return (bytes / GB).ToString("0.00") + " GB";
            if (bytes >= MB) return (bytes / MB).ToString("0.0") + " MB";
            if (bytes >= KB) return (bytes / KB).ToString("0.0") + " KB";

            return bytes + " B";
        }
    }
}
