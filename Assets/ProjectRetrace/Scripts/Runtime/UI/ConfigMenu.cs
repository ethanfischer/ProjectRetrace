using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRetrace
{
    /// <summary>
    /// In-game editor for retrace-config.json, reachable from the start menu or the config
    /// key at any point in a run. Built by reflection over RetraceConfig's public fields, so
    /// a new tunable shows up here the moment it is declared -- no second list to forget.
    /// Text fields rather than sliders because a hand-edited file has no ranges to offer,
    /// and typing an exact value is what tuning actually looks like. Fields are split into
    /// tabs by ConfigTabAttribute so the handful of settings a player wants (mouse, keys)
    /// aren't buried in forty tuning numbers.
    /// </summary>
    public class ConfigMenu : MonoBehaviour
    {
        public GameDirector director;

        /// <summary>Static so every other OnGUI can step aside while the menu is up.</summary>
        public static bool IsOpen { get; private set; }

        private static ConfigMenu _instance;

        private RetraceConfig _draft;
        private readonly Dictionary<string, string> _buffers = new Dictionary<string, string>();
        private FieldInfo[] _fields;
        private string[] _tabNames;
        private List<FieldInfo>[] _tabFields;
        private int _tab;
        private Vector2 _scroll;
        private string _status = string.Empty;

        private GUIStyle _title;
        private GUIStyle _label;
        private GUIStyle _hint;

        private void Reset()
        {
            director = GetComponent<GameDirector>();
        }

        private void Awake()
        {
            _instance = this;
            _fields = typeof(RetraceConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);
            BuildTabs();
        }

        /// <summary>Fields come back in declaration order, so a tab is simply every field from
        /// one tag up to the next. Anything declared before the first tag lands in "Other" so a
        /// forgotten tag can't hide a setting.</summary>
        private void BuildTabs()
        {
            var names = new List<string>();
            var groups = new List<List<FieldInfo>>();
            foreach (var field in _fields)
            {
                var tab = field.GetCustomAttribute<ConfigTabAttribute>();
                if (tab != null || groups.Count == 0)
                {
                    names.Add(tab != null ? tab.Name : "Other");
                    groups.Add(new List<FieldInfo>());
                }
                groups[groups.Count - 1].Add(field);
            }

            _tabNames = names.ToArray();
            _tabFields = groups.ToArray();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static void Toggle()
        {
            if (_instance == null) return;
            _instance.SetOpen(!IsOpen);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (open) LoadDraft(RetraceConfig.Current);
            if (director != null) director.SetConfigMenuOpen(open);
        }

        private void LoadDraft(RetraceConfig source)
        {
            _draft = source.Clone();
            _buffers.Clear();
            foreach (var field in _fields)
            {
                _buffers[field.Name] = Format(field.GetValue(_draft));
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard[RetraceConfig.Current.ConfigMenuKey].wasPressedThisFrame) SetOpen(!IsOpen);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;

            HudScale.Apply();
            EnsureStyles();

            var panel = new Rect(HudScale.Width * 0.5f - 320f, 30f, 640f, HudScale.Height - 60f);
            GUI.Box(panel, GUIContent.none);

            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, panel.height - 24f));
            GUILayout.Label("SETTINGS", _title);
            GUILayout.Label(RetraceConfig.FilePath, _hint);
            GUILayout.Space(6f);

            DrawTabs();
            GUILayout.Space(6f);

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var field in _tabFields[_tab]) DrawField(field);
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            DrawButtons();
            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status, _hint);
            GUILayout.EndArea();
        }

        private void DrawTabs()
        {
            var selected = GUILayout.Toolbar(_tab, _tabNames, GUILayout.Height(28f));
            if (selected == _tab) return;
            _tab = selected;
            _scroll = Vector2.zero;
        }

        private void DrawField(FieldInfo field)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(field.Name, _label, GUILayout.Width(300f));

            if (field.FieldType == typeof(bool))
            {
                var value = (bool)field.GetValue(_draft);
                var toggled = GUILayout.Toggle(value, string.Empty);
                if (toggled != value) field.SetValue(_draft, toggled);
            }
            else
            {
                var buffer = _buffers[field.Name];
                var edited = GUILayout.TextField(buffer, GUILayout.Width(200f));
                if (edited != buffer)
                {
                    _buffers[field.Name] = edited;
                    if (TryParse(field.FieldType, edited, out var parsed)) field.SetValue(_draft, parsed);
                }
            }

            GUILayout.EndHorizontal();
        }

        private void DrawButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save & close", GUILayout.Height(32f)))
            {
                RetraceConfig.Save(_draft);
                SetOpen(false);
            }

            if (GUILayout.Button("Reload from file", GUILayout.Height(32f)))
            {
                RetraceConfig.Reload();
                LoadDraft(RetraceConfig.Current);
                _status = "Reloaded.";
            }

            if (GUILayout.Button("Reset to defaults", GUILayout.Height(32f)))
            {
                LoadDraft(new RetraceConfig());
                _status = "Defaults loaded -- save to keep them.";
            }

            if (GUILayout.Button("Cancel", GUILayout.Height(32f)))
            {
                SetOpen(false);
            }

            GUILayout.EndHorizontal();
        }

        private static string Format(object value)
        {
            switch (value)
            {
                case float f: return f.ToString("0.####", CultureInfo.InvariantCulture);
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                default: return value != null ? value.ToString() : string.Empty;
            }
        }

        private static bool TryParse(System.Type type, string text, out object value)
        {
            value = null;
            if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                value = f;
            }
            else if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                value = i;
            }
            else if (type == typeof(string))
            {
                value = text;
            }

            return value != null;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = Color.white;

            _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _label.normal.textColor = Color.white;

            _hint = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _hint.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        }
    }
}
