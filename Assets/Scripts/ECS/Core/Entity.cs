using System;
using System.Collections.Generic;

namespace POELike.ECS.Core
{
    /// <summary>
    /// 实体（Entity）
    /// ECS中的基本单位，本身只是一个ID容器
    /// </summary>
    public class Entity
    {
        // 全局唯一ID计数器
        private static int _nextId = 0;

        public int    Id      { get; private set; }
        public string Tag     { get; set; }
        public bool   IsAlive { get; internal set; } = true;

        // 所属 World（用于回调索引维护）
        private readonly World _world;

        // 组件字典：类型 -> 组件实例
        private readonly Dictionary<Type, IComponent> _components = new Dictionary<Type, IComponent>();

        public Entity(string tag = "", World world = null)
        {
            Id     = _nextId++;
            Tag    = tag;
            _world = world;
        }

        /// <summary>添加组件，同步通知 World 更新索引</summary>
        public T AddComponent<T>(T component) where T : IComponent
        {
            var type = typeof(T);
            _components[type] = component;
            _world?.OnComponentAdded(this, type);
            return component;
        }

        /// <summary>获取组件</summary>
        public T GetComponent<T>() where T : class, IComponent
        {
            if (_components.TryGetValue(typeof(T), out var component))
                return component as T;
            return null;
        }

        /// <summary>是否拥有组件</summary>
        public bool HasComponent<T>() where T : IComponent
        {
            return _components.ContainsKey(typeof(T));
        }

        /// <summary>移除组件，同步通知 World 更新索引</summary>
        public bool RemoveComponent<T>() where T : IComponent
        {
            var type = typeof(T);
            bool removed = _components.Remove(type);
            if (removed) _world?.OnComponentRemoved(this, type);
            return removed;
        }

        /// <summary>获取所有组件（用于销毁时清理索引）</summary>
        public IEnumerable<IComponent> GetAllComponents()
        {
            return _components.Values;
        }

        /// <summary>获取所有组件类型（用于销毁时清理索引）</summary>
        internal IEnumerable<Type> GetAllComponentTypes()
        {
            return _components.Keys;
        }

        public void ResetAllComponents()
        {
            foreach (var component in _components.Values)
                component.Reset();
        }

        public override string ToString() => $"Entity[{Id}:{Tag}]";
    }
}