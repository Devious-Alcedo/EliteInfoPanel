// EventAggregator.cs
using System;
using System.Collections.Generic;

namespace EliteInfoPanel.Util
{
    public class EventAggregator
    {
        private static readonly Lazy<EventAggregator> _instance = new Lazy<EventAggregator>(() => new EventAggregator());
        public static EventAggregator Instance => _instance.Value;

        private readonly Dictionary<Type, List<object>> _subscribers = new Dictionary<Type, List<object>>();

        public void Subscribe<TEvent>(Action<TEvent> action)
        {
            var eventType = typeof(TEvent);
            if (!_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType] = new List<object>();
            }
            _subscribers[eventType].Add(action);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> action)
        {
            var eventType = typeof(TEvent);
            if (_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType].Remove(action);
            }
        }

        public void Publish<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (_subscribers.ContainsKey(eventType))
            {
                foreach (var subscriber in _subscribers[eventType].ToArray())
                {
                    var action = subscriber as Action<TEvent>;
                    action?.Invoke(eventData);
                }
            }
        }
    }

    // Event types
    public class CardVisibilityChangedEvent
    {
        public string CardName { get; set; }
        public bool IsVisible { get; set; }
        public bool RequiresLayoutRefresh { get; set; }
    }

    public class LayoutRefreshRequestEvent
    {
        public bool ForceRebuild { get; set; }
    }
}