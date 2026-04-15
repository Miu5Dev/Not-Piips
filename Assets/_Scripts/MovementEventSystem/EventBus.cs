using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple event bus for decoupled communication between systems.
/// Supports subscribing, unsubscribing, and raising events.
/// </summary>
public static class EventBus
{
    private static readonly Dictionary<Type, List<Delegate>> handlers = new();
    
    /// <summary>
    /// Subscribe to an event type with a handler
    /// </summary>
    public static void Subscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null) return;
        
        Type eventType = typeof(T);
        
        if (!handlers.ContainsKey(eventType))
            handlers[eventType] = new List<Delegate>();
        
        if (!handlers[eventType].Contains(handler))
            handlers[eventType].Add(handler);
    }
    
    /// <summary>
    /// Unsubscribe from an event type
    /// </summary>
    public static void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (handler == null) return;
        
        Type eventType = typeof(T);
        
        if (handlers.TryGetValue(eventType, out var list))
        {
            list.Remove(handler);
        }
    }
    
    /// <summary>
    /// Raise an event to all subscribers
    /// </summary>
    public static void Raise<T>(T eventData) where T : class
    {
        if (eventData == null) return;
        
        Type eventType = typeof(T);
        
        if (handlers.TryGetValue(eventType, out var list))
        {
            foreach (var handler in list.ToArray())
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] Error invoking handler for {eventType.Name}: {e.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// Clear all handlers (useful for scene transitions)
    /// </summary>
    public static void Clear()
    {
        handlers.Clear();
    }
    
    /// <summary>
    /// Clear handlers for a specific event type
    /// </summary>
    public static void Clear<T>() where T : class
    {
        Type eventType = typeof(T);
        if (handlers.ContainsKey(eventType))
        {
            handlers[eventType].Clear();
        }
    }
    
    /// <summary>
    /// Check if there are any subscribers for an event type
    /// </summary>
    public static bool HasSubscribers<T>() where T : class
    {
        Type eventType = typeof(T);
        return handlers.TryGetValue(eventType, out var list) && list.Count > 0;
    }
}
