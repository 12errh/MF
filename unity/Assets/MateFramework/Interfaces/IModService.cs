using System.Collections.Generic;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;

namespace Mate.Interfaces
{
    /// <summary>Mod discovery and lifecycle.</summary>
    public interface IModService
    {
        IReadOnlyList<ModInfo> InstalledMods { get; }
        Task<Result> LoadMods(string modsPath);
        Task<Result> ReloadMods();
    }

    public record ModInfo(string Name, string Version, string Description, string Path);
}