using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mate.AI
{
    /// <summary>
    /// Loads a character personality from a personality.toml file. Parses name,
    /// greeting, trait_* (int 1-10), and response_* (string) keys. Missing file
    /// yields defaults; missing traits default to 5.
    /// </summary>
    public class PersonalityService
    {
        private string _name = "Mate";
        private string _greeting = string.Empty;
        private readonly string _personalityPath;
        private readonly Dictionary<string, int> _traits = new();
        private readonly Dictionary<string, string> _responses = new();

        public string Name => _name;
        public string Greeting => _greeting;

        public PersonalityService(string projectDir)
        {
            _personalityPath = Path.Combine(projectDir, "personality.toml");
            Load();
        }

        public int GetTrait(string traitName) =>
            _traits.TryGetValue(traitName, out var val) ? val : 5;

        public string GetResponseForEvent(string eventName) =>
            _responses.TryGetValue(eventName, out var response) ? response : string.Empty;

        public string GenerateSystemPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"You are {_name}, a desktop companion.");
            sb.AppendLine();

            if (_traits.Count > 0)
            {
                sb.AppendLine("Personality traits:");
                foreach (var (trait, value) in _traits)
                    sb.AppendLine($"  - {trait}: {value}/10");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(_greeting))
                sb.AppendLine($"Your greeting: {_greeting}");

            return sb.ToString();
        }

        private void Load()
        {
            if (!File.Exists(_personalityPath)) return;

            foreach (var line in File.ReadAllLines(_personalityPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex < 0) continue;

                var key = trimmed[..eqIndex].Trim();
                var value = trimmed[(eqIndex + 1)..].Trim().Trim('"');

                if (key == "name") _name = value;
                else if (key == "greeting") _greeting = value;
                else if (key.StartsWith("trait_"))
                    _traits[key["trait_".Length..]] = int.TryParse(value, out var v) ? v : 5;
                else if (key.StartsWith("response_"))
                    _responses[key["response_".Length..]] = value;
            }
        }
    }
}