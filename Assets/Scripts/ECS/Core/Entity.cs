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
        
        /// <summary>
        /// 实体唯一ID
        /// </summary>
        public int Id { get; private set; }
        
        /// <summary>
        /// 实体标签（用于快速分类查找）
        /// </summary>
        public string Tag { get; set; }
        
        /// <summary>
        /// 实体是否存活
        /// </summary>
        public bool IsAlive { get; internal set; } = true;
        
        // 组件字典：类型 -> 组件实例
        private readonly Dictionary<Type, IComponent> _components = new Dictionary<Type, IComponent>();
        
        public Entity(string tag = "")
        {
            Id = _nextId++;
            Tag = tag;
        }
        
        /// <summary>
        /// 添加组件
        /// </summary>
        public T AddComponent<T>(T component) where T : IComponent
        {
            var type = typeof(T);
            _components[type] = component;
            return component;
        }
        
        /// <summary>
        /// 获取组件
        /// </summary>
        public T GetComponent<T>() where T : class, IComponent
        {
            var type = typeof(T);
            if (_components.TryGetValue(type, out var component))
                return component as T;
            return null;
        }
        
        /// <summary>
        /// 是否拥有组件
        /// </summary>
        public bool HasComponent<T>() where T : IComponent
        {
            return _components.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// 移除组件
        /// </summary>
        public bool RemoveComponent<T>() where T : IComponent
        {
            return _components.Remove(typeof(T));
        }
        
        /// <summary>
        /// 获取所有组件
        /// </summary>
        public IEnumerable<IComponent> GetAllComponents()
        {
            return _components.Values;
        }
        
        /// <summary>
        /// 重置所有组件
        /// </summary>
        public void ResetAllComponents()
        {
            foreach (var component in _components.Values)
                component.Reset();
        }
        
        public override string ToString()
        {
            return $"Entity[{Id}:{Tag}]";
        }
    }
}
