using System;
using System.Collections.Generic;

namespace Mate.Core
{
    /// <summary>
    /// Thread-agnostic typed event bus. Handlers are invoked synchronously on publish.
    /// </summary>
    public class SimpleEventBus : IEventBus
    {
        private readonly Dictionary<Type, List<(SubscriptionToken Token, Delegate Handler)>> _handlers = new();

        public SubscriptionToken Subscribe<T>(Action<T> handler)
        {
            var token = new SubscriptionToken(Guid.NewGuid());
            var type = typeof(T);

            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<(SubscriptionToken, Delegate)>();

            _handlers[type].Add((token, handler));
            return token;
        }

        public void Unsubscribe(SubscriptionToken token)
        {
            foreach (var kvp in _handlers)
            {
                kvp.Value.RemoveAll(h => h.Token.Equals(token));
            }
        }

        public void Publish<T>(T eventData)
        {
            if (!_handlers.ContainsKey(typeof(T)))
                return;

            foreach (var (_, handler) in _handlers[typeof(T)])
            {
                ((Action<T>)handler).Invoke(eventData);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}