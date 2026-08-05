using System;

namespace Mate.Core
{
    public interface IEventBus
    {
        SubscriptionToken Subscribe<T>(Action<T> handler);
        void Unsubscribe(SubscriptionToken token);
        void Publish<T>(T eventData);
        void Clear();
    }

    public struct SubscriptionToken : IEquatable<SubscriptionToken>
    {
        public Guid Id { get; }
        public SubscriptionToken(Guid id) => Id = id;
        public bool Equals(SubscriptionToken other) => Id == other.Id;
        public override bool Equals(object obj) => obj is SubscriptionToken other && Equals(other);
        public override int GetHashCode() => Id.GetHashCode();
    }
}