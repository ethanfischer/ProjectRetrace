using System.Collections.Generic;
using UnityEngine;

namespace ProjectRetrace
{
    /// <summary>
    /// IMGUI has no text outline, so every legible label in the game is the same text
    /// stamped in black around the eight neighbouring pixels first. White text alone
    /// vanishes against the pale walls of the house, and the default skin's translucent
    /// boxes do not help.
    /// </summary>
    public static class HudText
    {
        private static readonly Dictionary<int, GUIStyle> ButtonLabels = new Dictionary<int, GUIStyle>();

        private static readonly Vector2[] Offsets = { new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(0f, 1f) };

        public static void OutlinedLabel(Rect rect, string text, GUIStyle style, float alpha = 1f)
        {
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            // The default skin recolours labels on hover, which would turn the shadow
            // stamps white and fatten the text under the mouse; every state is pinned.
            var normal = style.normal.textColor;
            var hover = style.hover.textColor;
            var active = style.active.textColor;
            var focused = style.focused.textColor;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = Color.black;
            foreach (var offset in Offsets)
            {
                GUI.Label(new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height), text, style);
            }

            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = normal;
            GUI.Label(rect, text, style);
            style.hover.textColor = hover;
            style.active.textColor = active;
            style.focused.textColor = focused;
            GUI.color = previous;
        }

        /// <summary>A button's own label cannot be outlined, so the button is drawn blank
        /// and the caption stamped over it.</summary>
        public static bool OutlinedButton(Rect rect, string text, GUIStyle style)
        {
            var pressed = GUI.Button(rect, GUIContent.none, style);
            OutlinedLabel(rect, text, ButtonLabel(style.fontSize));
            return pressed;
        }

        private static GUIStyle ButtonLabel(int fontSize)
        {
            if (ButtonLabels.TryGetValue(fontSize, out var label)) return label;
            label = new GUIStyle(GUI.skin.label) { fontSize = fontSize, alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = Color.white;
            ButtonLabels[fontSize] = label;
            return label;
        }
    }
}
