using System;
using System.Collections.Generic;
using System.IO;

namespace Mate.Core
{
    /// <summary>
    /// IConfiguration backed by a project's mate.toml manifest. Uses a naive
    /// section-aware line parser (same pattern as PersonalityService — Unity's
    /// .NET profile has no TOML library). Maps manifest keys (snake_case
    /// sections) to the camelCase keys the services read; unknown keys are
    /// served via raw dotted lookup when present.
    /// </summary>
    public class MateTomlConfig : IConfiguration
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        private static readonly Dictionary<string, string> ManifestToService = new()
        {
            ["audio.threshold"] = "soundThreshold",
            ["audio.allowed_apps"] = "allowedApps",
            ["animation.dance_switch_time"] = "danceSwitchTime",
            ["animation.idle_switch_time"] = "idleSwitchTime",
            ["animation.dance_animation"] = "danceAnimation",
            ["character.head_sensitivity"] = "headSensitivity",
            ["character.eye_sensitivity"] = "eyeSensitivity",
            ["character.spine_sensitivity"] = "spineSensitivity",
            ["character.head_max_angle"] = "headMaxAngle",
            ["character.spine_max_angle"] = "spineMaxAngle",
            ["character.model"] = "modelPath",
            ["project.name"] = "projectName",
            ["window.transparent"] = "transparent",
            ["window.always_on_top"] = "alwaysOnTop",
            ["window.click_through"] = "clickThrough",
            ["window.hide_from_taskbar"] = "hideFromTaskbar",
            ["window.window_type"] = "windowType",
            ["window.initial_position"] = "initialPosition",
            ["system.tray_icon"] = "trayIcon",
            ["system.tray_tooltip"] = "trayTooltip",
            ["mods.mods_path"] = "modsPath",
            ["ai.model"] = "ai.model",
            ["ai.base_url"] = "ai.baseUrl",
            ["ai.enabled"] = "ai.enabled",
        };

        public MateTomlConfig(string projectDir)
        {
            Parse(Path.Combine(projectDir, "mate.toml"));
        }

        /// <summary>Parse mate.toml into dotted section.key values.</summary>
        private void Parse(string path)
        {
            if (!File.Exists(path))
                return;

            var section = string.Empty;
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line[1..^1].Trim();
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq < 0)
                    continue;

                var key = line[..eq].Trim();
                var value = line[(eq + 1)..].Trim();
                var dotted = string.IsNullOrEmpty(section) ? key : section + "." + key;

                // Strip quotes and brackets: "x", ["a", "b"], 1.5, true
                value = value.Trim().Trim('"');
                if (value.StartsWith("[") && value.EndsWith("]"))
                {
                    value = value[1..^1];
                    var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    var cleaned = new List<string>();
                    foreach (var p in parts)
                        cleaned.Add(p.Trim().Trim('"'));
                    value = string.Join(",", cleaned);
                }

                _values[dotted] = value;
            }
        }

        /// <summary>Resolve a service key: mapped manifest key, then raw dotted key.</summary>
        private bool TryGetValue(string key, out string value)
        {
            foreach (var manifestKey in ManifestToService)
            {
                if (manifestKey.Value == key && _values.TryGetValue(manifestKey.Key, out value))
                    return true;
            }
            return _values.TryGetValue(key, out value);
        }

        public float GetFloat(string key, float defaultValue)
            => TryGetValue(key, out var v) && float.TryParse(v, global::System.Globalization.NumberStyles.Float,
                global::System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : defaultValue;

        public int GetInt(string key, int defaultValue)
            => TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : defaultValue;

        public string GetString(string key, string defaultValue)
            => TryGetValue(key, out var v) ? v : defaultValue;

        public bool GetBool(string key, bool defaultValue)
            => TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : defaultValue;

        public void Set(string key, object value) => _values[key] = value?.ToString() ?? string.Empty;

        public void Save() { }

        public void Reload() { }
    }
}
