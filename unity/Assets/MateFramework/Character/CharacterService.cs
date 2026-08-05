using System;
using System.IO;
using System.Threading.Tasks;
using Mate.Core;
using Mate.Core.Models;
using Mate.Interfaces;
using UnityEngine;

namespace Mate.Character
{
    /// <summary>Character lifecycle service. Model loading is delegated to an injected IVrmLoader.</summary>
    public class CharacterService : ICharacterService
    {
        private readonly IConfiguration _config;
        private readonly IEventBus _eventBus;
        private readonly IVrmLoader _loader;
        private GameObject _currentModel;

        public bool IsLoaded => _currentModel != null;
        public GameObject CurrentModel => _currentModel;

        public event Action OnModelLoaded;
        public event Action OnModelUnloaded;

        public CharacterService(IConfiguration config, IEventBus eventBus, IVrmLoader loader = null)
        {
            _config = config;
            _eventBus = eventBus;
            _loader = loader ?? new VrmLoaderAdapter();
        }

        public async Task<Result> LoadModel(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Result.Fail($"VRM model not found at {path}");

            try
            {
                _currentModel = await _loader.LoadAsync(path);
            }
            catch (Exception ex)
            {
                return Result.Fail($"Failed to load VRM model: {ex.Message}");
            }

            if (_currentModel == null)
                return Result.Fail($"VRM model could not be loaded from {path}");

            OnModelLoaded?.Invoke();
            _eventBus.Publish(new ModelLoadedEvent(path));
            return Result.Ok();
        }

        public async Task<Result> UnloadModel()
        {
            if (_currentModel != null)
            {
                await _loader.UnloadAsync(_currentModel);
                _currentModel = null;

                OnModelUnloaded?.Invoke();
                _eventBus.Publish(new ModelUnloadedEvent());
            }
            return Result.Ok();
        }
    }

    public record ModelLoadedEvent(string Path);
    public record ModelUnloadedEvent();
}