using System;
using System.Collections.Generic;

namespace Mate.Core
{
    /// <summary>
    /// Lightweight service container replacing all singletons (ADR-005).
    /// Register services at startup, resolve when needed.
    /// </summary>
    public class MateContext : IDisposable
    {
        private readonly Dictionary<Type, Func<object>> _factories = new();
        private readonly Dictionary<Type, object> _singletons = new();

        /// <summary>Register a transient service (new instance each resolve).</summary>
        public void Register<TInterface>(Func<TInterface> factory)
        {
            _factories[typeof(TInterface)] = () => factory();
        }

        /// <summary>Register a singleton instance.</summary>
        public void RegisterSingleton<TInterface>(TInterface instance)
        {
            _singletons[typeof(TInterface)] = instance;
        }

        /// <summary>Resolve a registered service.</summary>
        public TInterface Resolve<TInterface>()
        {
            var type = typeof(TInterface);

            if (_singletons.TryGetValue(type, out var singleton))
                return (TInterface)singleton;

            if (_factories.TryGetValue(type, out var factory))
                return (TInterface)factory();

            throw new InvalidOperationException(
                $"No service registered for {type.Name}. " +
                "Call Register<T>() or RegisterSingleton<T>() first.");
        }

        /// <summary>Check if a service is registered.</summary>
        public bool IsRegistered<TInterface>()
        {
            var type = typeof(TInterface);
            return _singletons.ContainsKey(type) || _factories.ContainsKey(type);
        }

        /// <summary>Dispose all IDisposable singletons.</summary>
        public void Dispose()
        {
            foreach (var kvp in _singletons)
            {
                if (kvp.Value is IDisposable disposable)
                    disposable.Dispose();
            }
            _singletons.Clear();
            _factories.Clear();
        }
    }
}