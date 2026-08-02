using UnityEngine;
using UnityEditor;

namespace EasyLine
{
    /// <summary>
    /// Shared look and feel for EasyLine's custom inspectors.
    ///
    /// Everything here is drawn from IMGUI primitives plus one small cached gradient texture, so
    /// there are no image assets to ship, nothing to import, and the whole palette can be retuned
    /// from the constants at the top. Section identity is carried by an accent colour and a stripe
    /// rather than by glyphs in the label text.
    /// </summary>
    internal static class EasyLineEditorUI
    {
        // --- Palette: solid accents. Tints and hairlines are derived from these. ---
        internal static readonly Color Title  = new Color(0.25f, 0.62f, 1.00f);
        internal static readonly Color Curve  = new Color(0.35f, 0.85f, 0.45f);
        internal static readonly Color Source = new Color(1.00f, 0.72f, 0.25f);
        internal static readonly Color Array  = new Color(0.72f, 0.45f, 1.00f);
        internal static readonly Color Export = new Color(1.00f, 0.32f, 0.55f);

        private const float StripeWidth = 3f;

        private static Texture2D s_Gradient;
        private static Color s_GradientKey;
        private static GUIStyle s_TitleStyle;
        private static GUIStyle s_SectionStyle;

        private static bool Pro => EditorGUIUtility.isProSkin;

        private static Color Tint(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        // One gradient is enough: the title bar is the only place that uses it, and the accent
        // rarely changes, so we rebuild only when a different colour comes in.
        private static Texture2D GetGradient(Color accent)
        {
            if (s_Gradient != null && s_GradientKey == accent) return s_Gradient;
            if (s_Gradient != null) Object.DestroyImmediate(s_Gradient);

            const int width = 128;
            s_Gradient = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);
                // Quadratic falloff reads as a soft sheen rather than a flat ramp.
                s_Gradient.SetPixel(x, 0, Tint(accent, Mathf.Lerp(0.40f, 0.02f, t * t)));
            }

            s_Gradient.Apply();
            s_GradientKey = accent;
            return s_Gradient;
        }

        /// <summary>Component title bar: accent gradient with a solid underline.</summary>
        internal static void TitleBar(string text, Color accent)
        {
            Rect r = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(r, GetGradient(accent), ScaleMode.StretchToFill);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), Tint(accent, 0.85f));
            }

            if (s_TitleStyle == null)
            {
                s_TitleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14
                };
            }
            s_TitleStyle.normal.textColor = Pro ? Color.white : Color.black;

            GUI.Label(r, text, s_TitleStyle);
        }

        /// <summary>Section divider: left accent stripe, soft fill, hairline along the bottom.</summary>
        internal static void SectionHeader(string text, Color accent)
        {
            EditorGUILayout.Space(3);
            Rect r = GUILayoutUtility.GetRect(0, 21, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(r, Tint(accent, Pro ? 0.13f : 0.18f));
                EditorGUI.DrawRect(new Rect(r.x, r.y, StripeWidth, r.height), accent);
                EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), Tint(accent, 0.45f));
            }

            if (s_SectionStyle == null)
            {
                s_SectionStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };
                s_SectionStyle.padding.left = 10;
            }
            s_SectionStyle.normal.textColor = Pro ? new Color(0.92f, 0.92f, 0.92f) : Color.black;

            GUI.Label(r, text, s_SectionStyle);
        }

        /// <summary>
        /// Opens a boxed group wearing an accent stripe. Use it to lift one setting out of the
        /// flat list of rows. Must be paired with <see cref="EndHighlightCard"/>.
        /// </summary>
        internal static void BeginHighlightCard(Color accent)
        {
            Rect r = EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // The rect is only meaningful once layout has run, hence the width guard.
            if (Event.current.type == EventType.Repaint && r.width > 1f)
            {
                EditorGUI.DrawRect(new Rect(r.x + StripeWidth, r.y, r.width - StripeWidth, r.height), Tint(accent, 0.07f));
                EditorGUI.DrawRect(new Rect(r.x, r.y, StripeWidth, r.height), accent);
            }
        }

        internal static void EndHighlightCard()
        {
            EditorGUILayout.EndVertical();
        }
    }
}
