using System.Threading.Tasks;
using UnityEngine;

namespace Mate.Interfaces
{
    /// <summary>
    /// Loads a VRM model into a GameObject. The default implementation wraps the
    /// grabbed VRMLoader monolith; tests inject a fake. This is the single place
    /// allowed to touch the scene for model loading.
    /// </summary>
    public interface IVrmLoader
    {
        Task<GameObject> LoadAsync(string path);
        Task UnloadAsync(GameObject model);
    }
}