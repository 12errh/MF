using System;
using System.Threading;
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
        private const int PollIntervalMs = 50;
        private const int LoadTimeoutMs = 30000;

        public async Task<GameObject> LoadAsync(string path)
        {
            // The grabbed VRMLoader.LoadVRM is async void (fire-and-forget) with
            // internal importer awaits, so the model appears only after
            // FinalizeLoadedModel runs. Poll GetCurrentModel() until it is set.
            var loader = UnityEngine.Object.FindFirstObjectByType<VRMLoader>();
            if (loader == null)
                throw new InvalidOperationException("VRMLoader component not found in scene");

            loader.LoadVRM(path);

            var deadline = DateTime.UtcNow.AddMilliseconds(LoadTimeoutMs);
            while (loader.GetCurrentModel() == null)
            {
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException($"VRM model did not finish loading within {LoadTimeoutMs}ms");
                await Task.Delay(PollIntervalMs);
            }

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