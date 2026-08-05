using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mate.Core
{
    /// <summary>
    /// File-based configuration that reads/writes a JSON settings file.
    /// Migrates from SaveLoadHandler.SettingsData.
    /// </summary>
    public class FileConfiguration : IConfiguration
    {
        private readonly string _filePath;
        private Dictionary<string, object> _values;

        public FileConfiguration(string dataDir)
        {
            _filePath = Path.Combine(dataDir, "settings.json");
            _values = new Dictionary<string, object>();
            Load();
        }

        public float GetFloat(string key, float defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is long l) return l;
                if (val is double d) return (float)d;
                if (val is JToken token)
                {
                    if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                        return token.Value<float>();
                }
            }
            return defaultValue;
        }

        public int GetInt(string key, int defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is long l) return (int)l;
                if (val is double d) return (int)d;
                if (val is JToken token)
                {
                    if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                        return token.Value<int>();
                }
            }
            return defaultValue;
        }

        public string GetString(string key, string defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is string s) return s;
                if (val is JToken token && token.Type == JTokenType.String) return token.Value<string>();
            }
            return defaultValue;
        }

        public bool GetBool(string key, bool defaultValue)
        {
            if (_values.TryGetValue(key, out var val))
            {
                if (val is bool b) return b;
                if (val is JToken token && token.Type == JTokenType.Boolean) return token.Value<bool>();
            }
            return defaultValue;
        }

        public void Set(string key, object value)
        {
            _values[key] = value;
        }

        public void Save()
        {
            var json = JsonConvert.SerializeObject(_values, Formatting.Indented);
            File.WriteAllText(_filePath, json);
        }

        public void Reload()
        {
            Load();
        }

        private void Load()
        {
            if (File.Exists(_filePath))
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    _values = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                              ?? new Dictionary<string, object>();
                }
                catch
                {
                    _values = new Dictionary<string, object>();
                }
            }
            else
            {
                _values = new Dictionary<string, object>();
            }
        }
    }
}