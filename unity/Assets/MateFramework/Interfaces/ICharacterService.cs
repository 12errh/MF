using System;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using UnityEngine;

namespace Mate.Interfaces
{
    /// <summary>Character lifecycle: load/unload a VRM model via an injected loader.</summary>
    public interface ICharacterService
    {
        Task<Result> LoadModel(string path);
        Task<Result> UnloadModel();
        bool IsLoaded { get; }
        GameObject CurrentModel { get; }
        event Action OnModelLoaded;
        event Action OnModelUnloaded;
    }
}