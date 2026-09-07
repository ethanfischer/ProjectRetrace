using ProjectRetrace;
using UnityEditor;

namespace ProjectRetrace.EditorTools
{
    /// <summary>
    /// Shader globals are empty in edit mode until something sets them, so the scene view
    /// would show the shaders' built-in fallbacks rather than the config. Applying the
    /// config on load and on every play-mode change keeps what you see in the editor equal
    /// to what the game applies.
    /// </summary>
    [InitializeOnLoad]
    internal static class LookSettingsEditorSync
    {
        static LookSettingsEditorSync()
        {
            LookSettings.ApplyShaderGlobals(RetraceConfig.Current);
            EditorApplication.playModeStateChanged += _ => LookSettings.ApplyShaderGlobals(RetraceConfig.Current);
        }
    }
}
