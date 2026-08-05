using System;
using System.Threading.Tasks;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character
{
    /// <summary>
    /// Default IVrmLoader wrapping the grabbed VRMLoader monolith. This is the
    /// sanctioned exception to the no-FindFirstObjectByType rule: it locates the
    /// scene VRMLoader exactly once per load.
    /// </summary>
    public class VrmLoaderAdapter : IVrmLoader
    {
        public async Task<GameObject> LoadAsync(string path)
        {
            // The grabbed VRMLoader.LoadVRM is async void (fire-and-forget),
            // so yield a frame to let it complete before reading the model.
            var loader = UnityEngine.Object.FindFirstObjectByType<VRMLoader>();
            if (loader == null)
                throw new InvalidOperationException("VRMLoader component not found in scene");

            loader.LoadVRM(path);
            await Task.Yield();
            return loader.GetCurrentModel();
        }

        public Task UnloadAsync(GameObject model)
        {
            if (model != null)
                UnityEngine.Object.Destroy(model);
            return Task.CompletedTask;
        }
    }
}