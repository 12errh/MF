using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;

namespace Mate.Mods
{
    /// <summary>
    /// Scans a mods/ directory for mod.toml manifests. Each subdirectory with a
    /// mod.toml becomes a ModInfo (name/version/description from the file, falling
    /// back to the directory name).
    /// </summary>
    public class ModService : IModService
    {
        private readonly List<ModInfo> _mods = new();
        private string _modsPath;

        public IReadOnlyList<ModInfo> InstalledMods => _mods;

        public async Task<Result> LoadMods(string modsPath)
        {
            _modsPath = modsPath;
            return await ScanMods();
        }

        public async Task<Result> ReloadMods()
        {
            _mods.Clear();
            return await ScanMods();
        }

        private Task<Result> ScanMods()
        {
            if (string.IsNullOrEmpty(_modsPath) || !Directory.Exists(_modsPath))
                return Task.FromResult(Result.Ok());

            foreach (var modDir in Directory.GetDirectories(_modsPath))
            {
                var tomlPath = Path.Combine(modDir, "mod.toml");
                if (!File.Exists(tomlPath)) continue;

                var lines = File.ReadAllLines(tomlPath);
                string name = Path.GetFileName(modDir);
                string version = "0.0.0";
                string description = string.Empty;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    var eqIndex = trimmed.IndexOf('=');
                    if (eqIndex < 0) continue;

                    var key = trimmed[..eqIndex].Trim();
                    var value = trimmed[(eqIndex + 1)..].Trim().Trim('"');

                    if (key == "name") name = value;
                    else if (key == "version") version = value;
                    else if (key == "description") description = value;
                }

                _mods.Add(new ModInfo(name, version, description, modDir));
            }

            return Task.FromResult(Result.Ok());
        }
    }
}