using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace POELike.ECS.Core
{
    /// <summary>
    /// ECS世界（World）
    /// 管理所有实体和系统的核心容器
    ///
    /// 性能优化：
    ///   维护组件类型索引 _componentIndex，Query 直接按类型取交集，
    ///   避免每帧遍历全量实体列表，从 O(N) 降至 O(M_匹配)。
    /// </summary>
    public class World
    {
        // 单例
        private static World _instance;
        public static World Instance => _instance ??= new World();

        // 实体列表（用于顺序遍历，swap-remove 保证 O(1) 删除）
        private readonly List<Entity> _entities = new List<Entity>();
        // 实体ID -> 在_entities中的下标（加速 swap-remove）
        private readonly Dictionary<int, int> _entityIndex = new Dictionary<int, int>();

        // 组件类型索引：Type -> 拥有该组件的实体集合（HashSet 保证 O(1) 增删查）
        private readonly Dictionary<Type, HashSet<Entity>> _componentIndex
            = new Dictionary<Type, HashSet<Entity>>();

        // 待销毁实体队列
        private readonly Queue<Entity> _destroyQueue = new Queue<Entity>();
        // 系统列表（按优先级排序）
        private readonly List<ISystem> _systems = new List<ISystem>();
        // 事件总线
        public EventBus EventBus { get; } = new EventBus();

        private bool _isInitialized = false;
        private int  _aliveCount    = 0;

        // ── 初始化 ────────────────────────────────────────────────────

        public void Initialize()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));

            foreach (var system in _systems)
            {
                try { system.Initialize(this); }
                catch (Exception e) { Debug.LogError($"[World] 系统初始化失败: {system.GetType().Name} - {e.Message}"); }
            }

            Debug.Log($"[World] 初始化完成，共 {_systems.Count} 个系统");
        }

        // ── 每帧更新 ──────────────────────────────────────────────────

        public void Update(float deltaTime)
        {
            ProcessDestroyQueue();

            foreach (var system in _systems)
            {
                if (!system.IsEnabled) continue;
                try { system.Update(deltaTime); }
                catch (Exception e) { Debug.LogError($"[World] 系统Update异常: {system.GetType().Name} - {e.Message}"); }
            }
        }

        public void FixedUpdate(float fixedDeltaTime)
        {
            foreach (var system in _systems)
            {
                if (!system.IsEnabled) continue;
                try { system.FixedUpdate(fixedDeltaTime); }
                catch (Exception e) { Debug.LogError($"[World] 系统FixedUpdate异常: {system.GetType().Name} - {e.Message}"); }
            }
        }

        #region 实体管理

        public Entity CreateEntity(string tag = "")
        {
            var entity = new Entity(tag, this);
            int idx = _entities.Count;
            _entities.Add(entity);
            _entityIndex[entity.Id] = idx;
            _aliveCount++;
            EventBus.Publish(new EntityCreatedEvent { Entity = entity });
            return entity;
        }

        public void DestroyEntity(Entity entity)
        {
            if (entity == null || !entity.IsAlive) return;
            entity.IsAlive = false;
            _aliveCount--;
            _destroyQueue.Enqueue(entity);
            EventBus.Publish(new EntityDestroyedEvent { Entity = entity });
        }

        private void ProcessDestroyQueue()
        {
            while (_destroyQueue.Count > 0)
            {
                var entity = _destroyQueue.Dequeue();

                // 从组件索引中移除
                foreach (var type in entity.GetAllComponentTypes())
                    RemoveFromIndex(type, entity);

                // O(1) swap-remove：将末尾元素换到被删位置
                if (_entityIndex.TryGetValue(entity.Id, out int idx))
                {
                    int lastIdx = _entities.Count - 1;
                    if (idx != lastIdx)
                    {
                        var last = _entities[lastIdx];
                        _entities[idx] = last;
                        _entityIndex[last.Id] = idx;
                    }
                    _entities.RemoveAt(lastIdx);
                    _entityIndex.Remove(entity.Id);
                }
            }
        }

        // ── 组件索引维护（由 Entity 回调）────────────────────────────

        /// <summary>实体添加组件时，同步更新索引（由 Entity 内部调用）</summary>
        internal void OnComponentAdded(Entity entity, Type componentType)
        {
            if (!_componentIndex.TryGetValue(componentType, out var set))
            {
                set = new HashSet<Entity>();
                _componentIndex[componentType] = set;
            }
            set.Add(entity);
        }

        /// <summary>实体移除组件时，同步更新索引（由 Entity 内部调用）</summary>
        internal void OnComponentRemoved(Entity entity, Type componentType)
        {
            RemoveFromIndex(componentType, entity);
        }

        private void RemoveFromIndex(Type componentType, Entity entity)
        {
            if (_componentIndex.TryGetValue(componentType, out var set))
                set.Remove(entity);
        }

        // ── Query：直接查索引，O(M_匹配) ─────────────────────────────

        /// <summary>查询拥有指定组件的所有实体（每帧 new List，小规模使用）</summary>
        public List<Entity> Query<T>() where T : IComponent
        {
            var result = new List<Entity>();
            Query<T>(result);
            return result;
        }

        /// <summary>查询拥有指定组件的所有实体（零 GC 版本，复用传入的 List）</summary>
        public void Query<T>(List<Entity> result) where T : IComponent
        {
            result.Clear();
            if (!_componentIndex.TryGetValue(typeof(T), out var set)) return;
            foreach (var entity in set)
            {
                if (entity.IsAlive) result.Add(entity);
            }
        }

        /// <summary>查询同时拥有两个组件的实体（每帧 new List，小规模使用）</summary>
        public List<Entity> Query<T1, T2>()
            where T1 : IComponent
            where T2 : IComponent
        {
            var result = new List<Entity>();
            Query<T1, T2>(result);
            return result;
        }

        /// <summary>查询同时拥有两个组件的实体（零 GC 版本）</summary>
        public void Query<T1, T2>(List<Entity> result)
            where T1 : IComponent
            where T2 : IComponent
        {
            result.Clear();
            var t1 = typeof(T1);
            var t2 = typeof(T2);

            // 取较小的集合遍历，用较大的集合做 Contains 检查
            if (!_componentIndex.TryGetValue(t1, out var set1)) return;
            if (!_componentIndex.TryGetValue(t2, out var set2)) return;

            var (smaller, larger) = set1.Count <= set2.Count ? (set1, set2) : (set2, set1);
            foreach (var entity in smaller)
            {
                if (entity.IsAlive && larger.Contains(entity))
                    result.Add(entity);
            }
        }

        /// <summary>查询同时拥有三个组件的实体（每帧 new List，小规模使用）</summary>
        public List<Entity> Query<T1, T2, T3>()
            where T1 : IComponent
            where T2 : IComponent
            where T3 : IComponent
        {
            var result = new List<Entity>();
            Query<T1, T2, T3>(result);
            return result;
        }

        /// <summary>查询同时拥有三个组件的实体（零 GC 版本）</summary>
        public void Query<T1, T2, T3>(List<Entity> result)
            where T1 : IComponent
            where T2 : IComponent
            where T3 : IComponent
        {
            result.Clear();
            if (!_componentIndex.TryGetValue(typeof(T1), out var set1)) return;
            if (!_componentIndex.TryGetValue(typeof(T2), out var set2)) return;
            if (!_componentIndex.TryGetValue(typeof(T3), out var set3)) return;

            // 找最小集合遍历
            HashSet<Entity> smallest, mid, big;
            if (set1.Count <= set2.Count && set1.Count <= set3.Count)
                (smallest, mid, big) = (set1, set2, set3);
            else if (set2.Count <= set1.Count && set2.Count <= set3.Count)
                (smallest, mid, big) = (set2, set1, set3);
            else
                (smallest, mid, big) = (set3, set1, set2);

            foreach (var entity in smallest)
            {
                if (entity.IsAlive && mid.Contains(entity) && big.Contains(entity))
                    result.Add(entity);
            }
        }

        /// <summary>通过Tag查找实体</summary>
        public Entity FindEntityByTag(string tag)
        {
            foreach (var entity in _entities)
            {
                if (entity.IsAlive && entity.Tag == tag) return entity;
            }
            return null;
        }

        /// <summary>获取所有存活实体数量（O(1)）</summary>
        public int EntityCount => _aliveCount;

        #endregion

        #region 系统管理

        public void RegisterSystem(ISystem system)
        {
            if (_systems.Contains(system)) return;
            _systems.Add(system);

            if (_isInitialized)
            {
                system.Initialize(this);
                _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            }
        }

        public T GetSystem<T>() where T : class, ISystem
        {
            return _systems.OfType<T>().FirstOrDefault();
        }

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

        public void Dispose()
        {
            foreach (var system in _systems)
            {
                try { system.Dispose(); }
                catch (Exception e) { Debug.LogError($"[World] 系统Dispose异常: {system.GetType().Name} - {e.Message}"); }
            }
            _systems.Clear();
            _entities.Clear();
            _entityIndex.Clear();
            _componentIndex.Clear();
            _destroyQueue.Clear();
            _aliveCount = 0;
            _instance = null;
            _isInitialized = false;
            Debug.Log("[World] 世界已销毁");
        }
    }

    // 世界事件
    public struct EntityCreatedEvent { public Entity Entity; }
    public struct EntityDestroyedEvent { public Entity Entity; }
}