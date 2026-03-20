using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace POELike.ECS.Core
{
    /// <summary>
    /// ECS世界（World）
    /// 管理所有实体和系统的核心容器
    /// </summary>
    public class World
    {
        // 单例
        private static World _instance;
        public static World Instance => _instance ??= new World();
        
        // 实体列表
        private readonly List<Entity> _entities = new List<Entity>();
        // 待销毁实体队列
        private readonly Queue<Entity> _destroyQueue = new Queue<Entity>();
        // 系统列表（按优先级排序）
        private readonly List<ISystem> _systems = new List<ISystem>();
        // 事件总线
        public EventBus EventBus { get; } = new EventBus();
        
        private bool _isInitialized = false;
        
        /// <summary>
        /// 初始化世界
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            
            // 按优先级排序系统
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            
            foreach (var system in _systems)
            {
                try
                {
                    system.Initialize(this);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[World] 系统初始化失败: {system.GetType().Name} - {e.Message}");
                }
            }
            
            Debug.Log($"[World] 初始化完成，共 {_systems.Count} 个系统");
        }
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Update(float deltaTime)
        {
            // 处理待销毁实体
            ProcessDestroyQueue();
            
            foreach (var system in _systems)
            {
                if (!system.IsEnabled) continue;
                try
                {
                    system.Update(deltaTime);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[World] 系统Update异常: {system.GetType().Name} - {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// 固定物理更新
        /// </summary>
        public void FixedUpdate(float fixedDeltaTime)
        {
            foreach (var system in _systems)
            {
                if (!system.IsEnabled) continue;
                try
                {
                    system.FixedUpdate(fixedDeltaTime);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[World] 系统FixedUpdate异常: {system.GetType().Name} - {e.Message}");
                }
            }
        }
        
        #region 实体管理
        
        /// <summary>
        /// 创建实体
        /// </summary>
        public Entity CreateEntity(string tag = "")
        {
            var entity = new Entity(tag);
            _entities.Add(entity);
            EventBus.Publish(new EntityCreatedEvent { Entity = entity });
            return entity;
        }
        
        /// <summary>
        /// 销毁实体（延迟到帧末）
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            if (entity == null || !entity.IsAlive) return;
            entity.IsAlive = false;
            _destroyQueue.Enqueue(entity);
            EventBus.Publish(new EntityDestroyedEvent { Entity = entity });
        }
        
        /// <summary>
        /// 处理销毁队列
        /// </summary>
        private void ProcessDestroyQueue()
        {
            while (_destroyQueue.Count > 0)
            {
                var entity = _destroyQueue.Dequeue();
                _entities.Remove(entity);
            }
        }
        
        /// <summary>
        /// 查询拥有指定组件的所有实体
        /// </summary>
        public List<Entity> Query<T>() where T : IComponent
        {
            var result = new List<Entity>();
            foreach (var entity in _entities)
            {
                if (entity.IsAlive && entity.HasComponent<T>())
                    result.Add(entity);
            }
            return result;
        }
        
        /// <summary>
        /// 查询同时拥有两个组件的实体
        /// </summary>
        public List<Entity> Query<T1, T2>() 
            where T1 : IComponent 
            where T2 : IComponent
        {
            var result = new List<Entity>();
            foreach (var entity in _entities)
            {
                if (entity.IsAlive && entity.HasComponent<T1>() && entity.HasComponent<T2>())
                    result.Add(entity);
            }
            return result;
        }
        
        /// <summary>
        /// 查询同时拥有三个组件的实体
        /// </summary>
        public List<Entity> Query<T1, T2, T3>() 
            where T1 : IComponent 
            where T2 : IComponent
            where T3 : IComponent
        {
            var result = new List<Entity>();
            foreach (var entity in _entities)
            {
                if (entity.IsAlive && entity.HasComponent<T1>() && entity.HasComponent<T2>() && entity.HasComponent<T3>())
                    result.Add(entity);
            }
            return result;
        }
        
        /// <summary>
        /// 通过Tag查找实体
        /// </summary>
        public Entity FindEntityByTag(string tag)
        {
            return _entities.FirstOrDefault(e => e.IsAlive && e.Tag == tag);
        }
        
        /// <summary>
        /// 获取所有存活实体数量
        /// </summary>
        public int EntityCount => _entities.Count(e => e.IsAlive);
        
        #endregion
        
        #region 系统管理
        
        /// <summary>
        /// 注册系统
        /// </summary>
        public void RegisterSystem(ISystem system)
        {
            if (_systems.Contains(system)) return;
            _systems.Add(system);
            
            if (_isInitialized)
            {
                // 如果世界已初始化，立即初始化新系统
                system.Initialize(this);
                _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }
        
        /// <summary>
        /// 获取系统
        /// </summary>
        public T GetSystem<T>() where T : class, ISystem
        {
            return _systems.OfType<T>().FirstOrDefault();
        }
        
        /// <summary>
        /// 移除系统
        /// </summary>
        public void RemoveSystem<T>() where T : ISystem
        {
            var system = _systems.OfType<T>().FirstOrDefault();
            if (system != null)
            {
                system.Dispose();
                _systems.Remove(system);
            }
        }
        
        #endregion
        
        /// <summary>
        /// 销毁世界，清理所有资源
        /// </summary>
        public void Dispose()
        {
            foreach (var system in _systems)
            {
                try { system.Dispose(); }
                catch (Exception e) { Debug.LogError($"[World] 系统Dispose异常: {system.GetType().Name} - {e.Message}"); }
            }
            _systems.Clear();
            _entities.Clear();
            _destroyQueue.Clear();
            _instance = null;
            _isInitialized = false;
            Debug.Log("[World] 世界已销毁");
        }
    }
    
    // 世界事件
    public struct EntityCreatedEvent { public Entity Entity; }
    public struct EntityDestroyedEvent { public Entity Entity; }
}
