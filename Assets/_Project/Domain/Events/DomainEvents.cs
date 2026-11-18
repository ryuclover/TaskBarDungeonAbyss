using System;
using System.Collections.Generic;

namespace TBDA.Domain.Events
{
    public interface IDomainEvent { }

    /// <summary>
    /// Lightweight strongly-typed EventBus with zero UnityEngine dependencies.
    /// Keeps Tamagotchi, Training, MiniMap, Audio, and UI completely decoupled.
    /// </summary>
    public sealed class DomainEventBus
    {
        private readonly Dictionary<Type, Delegate> _handlers = new Dictionary<Type, Delegate>();

        public void Subscribe<T>(Action<T> listener) where T : struct, IDomainEvent
        {
            var key = typeof(T);
            if (_handlers.TryGetValue(key, out var existing))
            {
                _handlers[key] = Delegate.Combine(existing, listener);
            }
            else
            {
                _handlers[key] = listener;
            }
        }

        public void Unsubscribe<T>(Action<T> listener) where T : struct, IDomainEvent
        {
            var key = typeof(T);
            if (!_handlers.TryGetValue(key, out var existing)) return;

            var updated = Delegate.Remove(existing, listener);
            if (updated == null) _handlers.Remove(key);
            else _handlers[key] = updated;
        }

        public void Publish<T>(T evt) where T : struct, IDomainEvent
        {
            if (_handlers.TryGetValue(typeof(T), out var del) && del is Action<T> callback)
            {
                callback.Invoke(evt);
            }
        }

        public void Clear() => _handlers.Clear();
    }

    public struct PetStateUpdatedEvent : IDomainEvent
    {
        public string Reason;
    }

    public struct FloatingFeedbackEvent : IDomainEvent
    {
        public string Message;
        public FeedbackTone Tone;
    }

    public enum FeedbackTone
    {
        Neutral = 0,
        Positive = 1,
        Warning = 2,
        StatLevelUp = 3,
        Love = 4
    }

    public struct StatLevelUpEvent : IDomainEvent
    {
        public string StatCode;
        public int NewLevel;
    }

    public struct MapMutatedEvent : IDomainEvent
    {
        public int MutatedNodesCount;
    }

    public struct ExpeditionCompletedEvent : IDomainEvent
    {
        public bool Victory;
        public string SummaryLog;
        public int ShardsEarned;
        public string ItemRewardId;
    }

    public struct IntroCompletedEvent : IDomainEvent
    {
        public string PetName;
    }
}
