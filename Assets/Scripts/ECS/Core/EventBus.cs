using System;
using System.Collections.Generic;
using UnityEngine;

namespace POELike.ECS.Core
{
    /// <summary>
    /// 事件总线
    /// 用于ECS系统间的解耦通信
    /// </summary>
    public class EventBus
    {
        // 事件处理器字典：事件类型 -> 处理器列表
        private readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>();
        
        /// <summary>
        /// 订阅事件
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }
            list.Add(handler);
        }
        
        /// <summary>
        /// 取消订阅事件
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
                list.Remove(handler);
        }
        
        /// <summary>
        /// 发布事件
        /// </summary>
        public void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list)) return;
            
            // 复制列表防止在回调中修改
            var snapshot = new List<Delegate>(list);
            foreach (var handler in snapshot)
            {
                try
                {
                    ((Action<T>)handler)?.Invoke(eventData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[EventBus] 事件处理异常: {typeof(T).Name} - {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// 清除所有事件订阅
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
        }
        
        /// <summary>
        /// 清除指定类型的事件订阅
        /// </summary>
        public void Clear<T>()
        {
            _handlers.Remove(typeof(T));
        }
    }
}
