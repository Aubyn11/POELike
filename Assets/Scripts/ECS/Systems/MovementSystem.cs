using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// 移动系统
    /// 处理所有实体的移动逻辑
    ///
    /// 性能架构：
    ///   · 玩家（CharacterController/Rigidbody）：CPU 每帧处理，数量极少
    ///   · 怪物（纯逻辑移动）：GPU ComputeShader 并行计算移动 + 玩家分离
    ///     CPU 只负责上传移动方向，GPU 并行写回新位置，AsyncGPUReadback 异步回读
    ///   · 怪物间分离：空间网格 CPU 计算（GPU O(N²) 代价过高）
    ///
    /// 优先级：100
    /// </summary>
    public class MovementSystem : SystemBase
    {
        public override int Priority => 100;

        private const float Gravity = -9.81f;

        // ── 怪物碰撞参数 ──────────────────────────────────────────────
        private const float MonsterRadius           = MonsterSpawner.CollisionRadius;
        private const float SeparationDiam          = MonsterRadius * 2f;
        private const float SeparationDiamSq        = SeparationDiam * SeparationDiam;
        private const float CellSize                = SeparationDiam;
        private const float PlayerRadius            = 0.4f;
        private const float PlayerMonsterMinDist    = PlayerRadius + MonsterRadius;
        private const float PlayerMonsterMinDistSq  = PlayerMonsterMinDist * PlayerMonsterMinDist;

        // ── 玩家缓存 ──────────────────────────────────────────────────
        private Entity             _playerEntity;
        private TransformComponent _playerTransform;

        // ── 零 GC Query 缓冲 ──────────────────────────────────────────
        private readonly List<Entity> _queryBuffer = new List<Entity>(4096);

        // ── 怪物组件缓存（避免每帧 GetComponent，首次注册后不再查找）──
        // 与 GPU Buffer 下标一一对应
        private readonly List<TransformComponent> _monsterTCs = new List<TransformComponent>(4096);
        private readonly List<MovementComponent>  _monsterMCs = new List<MovementComponent>(4096);
        private readonly List<AIComponent>        _monsterAIs = new List<AIComponent>(4096);
        private readonly HashSet<Entity>          _registeredMonsters = new HashSet<Entity>();
        private readonly List<Entity>             _monsterEntities = new List<Entity>(4096);
        // 是否有新怪物待注册（事件驱动，避免每帧 Query）
        private bool _needSync = true;

        // ── GPU 计算资源 ───────────────────────────────────────────────
        private ComputeShader _simulateCS;
        private int           _kernelMove;
        private int           _kernelSeparate;

        // GPU Buffer：位置常驻 GPU，仅每帧上传移动方向和速度
        // MonsterMoveInput: float3 moveDirection + float currentSpeed = 16 bytes
        private const int InputStride  = 16;
        // MonsterMoveOutput: float3 position + float pad = 16 bytes
        private const int OutputStride = 16;

        private ComputeBuffer _gpuInputBuffer;
        private ComputeBuffer _gpuOutputBuffer;
        private int           _gpuCapacity;

        // CPU 端暂存（NativeArray，SetData 零 GC）
        private NativeArray<MonsterMoveInputGpu>  _cpuInputArray;
        private NativeArray<MonsterMoveOutputGpu> _cpuOutputArray;
        private NativeArray<MonsterMoveOutputGpu> _cpuReadbackArray;
        // GPU 位置是否需要由 ECS 重新上传（创建/销毁/扩容后置脏）
        private bool _gpuPositionsDirty = true;

        // GPU 异步回读（结果只在固定更新点统一应用，避免异步回调直接改写当前帧 GPU 位置导致闪现）
        private bool _readbackPending      = false;  // 是否有回读请求在飞行中
        private bool _hasPendingReadback   = false;  // 是否有待在主更新点落地的回读结果
        private int  _pendingReadbackCount = 0;
        private const int ReadbackInterval = 2;      // 每 2 次 GPU 步进请求一次回读，降低读回压力并减少帧时间抖动
        private const float MaxGpuDispatchDelta = 0.10f;

        private float _pendingGpuDelta = 0f;
        private int  _readbackFrame    = 0;
        private bool _disposed         = false;
        private int  _gpuGeneration    = 0;
        private int  _layoutVersion    = 0;

        // ── 暴露给 MonsterMeshRenderer 直接读取（零拷贝渲染）────────
        /// <summary>GPU 输出位置 Buffer（float3+pad，16 bytes/实体），MonsterMeshRenderer 直接读取</summary>
        public ComputeBuffer GpuPositionBuffer => _gpuOutputBuffer;
        /// <summary>当前帧有效的怪物数量</summary>
        public int MonsterCount => _monsterTCs.Count;
        /// <summary>怪物 MonsterComponent 缓存（与 GPU Buffer 下标一一对应），供渲染器共享</summary>
        public List<MonsterComponent> MonsterMCsShared => _monsterMCsShared;
        /// <summary>怪物 MovementComponent 缓存（与 GPU Buffer 下标一一对应），供渲染器共享</summary>
        public List<MovementComponent> MonsterMVsShared => _monsterMVsShared;
        /// <summary>怪物 AIComponent 缓存（与 GPU Buffer 下标一一对应），供渲染器共享</summary>
        public List<AIComponent> MonsterAIsShared => _monsterAIs;
        /// <summary>怪物 TransformComponent 缓存（与 GPU Buffer 下标一一对应），供渲染器共享</summary>
        public List<TransformComponent> MonsterTCsShared => _monsterTCs;

        // 渲染器共享的组件缓存（与 _monsterTCs 下标一一对应）
        private readonly List<MonsterComponent>  _monsterMCsShared  = new List<MonsterComponent>(4096);
        private readonly List<MovementComponent> _monsterMVsShared  = new List<MovementComponent>(4096);

        // Shader Property IDs
        private static readonly int ID_MoveInputs          = Shader.PropertyToID("_MoveInputs");
        private static readonly int ID_MoveOutputs         = Shader.PropertyToID("_MoveOutputs");
        private static readonly int ID_MonsterCount        = Shader.PropertyToID("_MonsterCount");
        private static readonly int ID_DeltaTime           = Shader.PropertyToID("_DeltaTime");
        private static readonly int ID_PlayerPosition      = Shader.PropertyToID("_PlayerPosition");
        private static readonly int ID_PlayerMonsterMinDist= Shader.PropertyToID("_PlayerMonsterMinDist");
        private static readonly int ID_SeparationDiam      = Shader.PropertyToID("_SeparationDiam");

        // ── 空间网格（怪物间分离，CPU）────────────────────────────────
        private readonly Dictionary<long, List<int>> _grid       = new Dictionary<long, List<int>>(512);
        private readonly List<List<int>>             _gridPooled = new List<List<int>>(512);
        // 分离降频：每 N 帧执行一次（怪物分离不需要每帧执行）
        private const int SeparationInterval = 3;
        private int _separationFrame = 0;

        // ── CPU 端 GPU 输入结构体（与 ComputeShader 对齐，16 bytes）──
        private struct MonsterMoveInputGpu
        {
            public float dx, dy, dz;        // moveDirection
            public float currentSpeed;
        }

        // ── CPU 端 GPU 输出结构体（16 bytes）─────────────────────────
        private struct MonsterMoveOutputGpu
        {
            public float px, py, pz;
            public float pad;
        }

        // ── 初始化 ────────────────────────────────────────────────────

        protected override void OnInitialize()
        {
            _disposed = false;
            _hasPendingReadback = false;
            _pendingReadbackCount = 0;
            _pendingGpuDelta = 0f;
            _readbackFrame = 0;
            _simulateCS = Resources.Load<ComputeShader>("Shaders/MonsterSimulateCompute");
            if (_simulateCS != null)
            {
                _kernelMove     = _simulateCS.FindKernel("CSMove");
                _kernelSeparate = _simulateCS.FindKernel("CSSeparate");
                Debug.Log("[MovementSystem] GPU 怪物模拟 ComputeShader 加载成功");
            }
            else
            {
                Debug.LogWarning("[MovementSystem] 找不到 MonsterSimulateCompute，将回退到 CPU 模式");
            }

            World.EventBus.Subscribe<EntityCreatedEvent>(OnEntityCreated);
            World.EventBus.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        private void OnEntityCreated(EntityCreatedEvent evt)
        {
            // 怪物创建时标记需要同步缓存（AddComponent 在 CreateEntity 之后，下帧 SyncMonsterCache 处理）
            if (evt.Entity.Tag == "Monster")
            {
                _needSync = true;
                _gpuPositionsDirty = true;
            }
        }

        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            if (evt.Entity.Tag != "Monster") return;
            if (!_registeredMonsters.Contains(evt.Entity)) return;
            // 立即 swap-remove，避免每帧扫描死亡怪物
            _registeredMonsters.Remove(evt.Entity);
            int idx = _monsterEntities.IndexOf(evt.Entity);
            if (idx < 0) return;
            int last = _monsterEntities.Count - 1;
            _monsterTCs[idx]      = _monsterTCs[last];
            _monsterMCs[idx]      = _monsterMCs[last];
            _monsterAIs[idx]      = _monsterAIs[last];
            _monsterEntities[idx] = _monsterEntities[last];
            _monsterMCsShared[idx] = _monsterMCsShared[last];  // 同步 swap-remove
            _monsterMVsShared[idx] = _monsterMVsShared[last];  // 同步 swap-remove
            _monsterTCs.RemoveAt(last);
            _monsterMCs.RemoveAt(last);
            _monsterAIs.RemoveAt(last);
            _monsterEntities.RemoveAt(last);
            _monsterMCsShared.RemoveAt(last);
            _monsterMVsShared.RemoveAt(last);
            _gpuPositionsDirty = true;
            _layoutVersion++;
        }

        // ── 每帧更新 ──────────────────────────────────────────────────

        protected override void OnUpdate(float deltaTime)
        {
            // ── Step1：处理玩家等非怪物实体（CharacterController/Rigidbody）──
            UpdateNonMonsterEntities(deltaTime);

            // ── Step2：增量注册新怪物到缓存 ───────────────────────────
            SyncMonsterCache();

            int monsterCount = _monsterTCs.Count;
            if (monsterCount == 0) return;

            // ── Step3：每帧都推进 GPU 模拟，CPU 镜像通过异步回读按节流频率刷新 ──
            if (_simulateCS != null)
            {
                _pendingGpuDelta += deltaTime;
                ApplyPendingGpuReadback();

                if (_pendingGpuDelta > 0.0001f)
                {
                    float stepDelta = Mathf.Min(_pendingGpuDelta, MaxGpuDispatchDelta);
                    _pendingGpuDelta -= stepDelta;
                    DispatchGpuMove(monsterCount, stepDelta);
                }
            }
            else
            {
                _pendingGpuDelta = 0f;
                ApplyPendingGpuReadback();
                FallbackCpuMove(monsterCount, deltaTime);
            }

            // ── Step4：怪物间分离（CPU 空间网格，每3帧执行一次）──────
            // 当渲染直接读取 GPU 位置时，CPU 分离结果不会反映到当前渲染帧，属于高成本低收益。
            // 这里先跳过该步骤，把玩家-怪物分离完全交给 GPU，显著降低 10000 怪时的 CPU 压力。
            if (_simulateCS == null)
            {
                _separationFrame++;
                if (_separationFrame >= SeparationInterval)
                {
                    _separationFrame = 0;
                    ResolveMonsterSeparation(monsterCount);
                    if (_playerTransform != null)
                        ResolveAllPlayerMonsterSeparation(monsterCount, _playerTransform.Position);
                }
            }

        }

        // ── 非怪物实体（玩家）移动 ────────────────────────────────────
        // 玩家实体缓存（避免每帧 Query 遍历 10000 只怪物的组件集合）
        private TransformComponent _playerTC;
        private MovementComponent  _playerMC;
        private bool _playerCached = false;

        private void UpdateNonMonsterEntities(float deltaTime)
        {
            // 只处理玩家，不再 Query 全量实体（避免遍历 10000 只怪物的组件集合）
            if (!_playerCached)
            {
                RefreshPlayerTransform();
                if (_playerEntity != null)
                {
                    _playerTC = _playerEntity.GetComponent<TransformComponent>();
                    _playerMC = _playerEntity.GetComponent<MovementComponent>();
                    _playerCached = (_playerTC != null && _playerMC != null);
                }
                if (!_playerCached) return;
            }

            // 直接处理玩家，无需遍历
            var entity   = _playerEntity;
            var transform = _playerTC;
            var movement  = _playerMC;

            if (entity == null || !entity.IsAlive) { _playerCached = false; return; }
            if (transform == null || movement == null) return;

            bool canMove = movement.IsEnabled && !movement.IsImmobilized;
            if (canMove)
            {
                // 点击寻路
                if (movement.HasTarget)
                {
                    var currentPos = transform.UnityTransform != null
                        ? transform.UnityTransform.position
                        : transform.Position;
                    var targetFlat = new Vector3(movement.TargetPosition.x, currentPos.y, movement.TargetPosition.z);
                    var toTarget   = targetFlat - currentPos;
                    float dist     = toTarget.magnitude;

                    if (dist <= movement.ArrivalDistance)
                    {
                        movement.HasTarget     = false;
                        movement.MoveDirection = Vector3.zero;
                    }
                    else
                    {
                        movement.MoveDirection = toTarget.normalized;
                    }
                }

                if (movement.CharacterController != null)
                    UpdateCharacterControllerMovement(movement, deltaTime);
                else if (movement.Rigidbody != null)
                    UpdateRigidbodyMovement(movement, deltaTime);
                else
                    UpdateLogicMovement(transform, movement, deltaTime);
            }
            if (movement.MoveDirection.sqrMagnitude > 0.01f && transform.UnityTransform != null)
            {
                var lookDir = new Vector3(movement.MoveDirection.x, 0, movement.MoveDirection.z);
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    transform.UnityTransform.rotation = Quaternion.Slerp(
                        transform.UnityTransform.rotation,
                        Quaternion.LookRotation(lookDir),
                        deltaTime * 15f);
                }
            }
        }

        // ── 怪物缓存同步（增量注册新怪物，死亡清理由事件驱动）────

        private void SyncMonsterCache()
        {
            // 没有新怪物创建时跳过，避免每帧 Query
            if (!_needSync) return;
            _needSync = false;

            bool addedAny = false;
            World.Query<MovementComponent>(_queryBuffer);

            foreach (var entity in _queryBuffer)
            {
                if (entity.Tag != "Monster") continue;
                if (_registeredMonsters.Contains(entity)) continue;

                var tc = entity.GetComponent<TransformComponent>();
                var mc = entity.GetComponent<MovementComponent>();
                var ai = entity.GetComponent<AIComponent>();
                var monsterMC = entity.GetComponent<MonsterComponent>();
                if (tc == null || mc == null) continue;

                _monsterTCs.Add(tc);
                _monsterMCs.Add(mc);
                _monsterAIs.Add(ai);
                _monsterEntities.Add(entity);
                _registeredMonsters.Add(entity);
                _monsterMCsShared.Add(monsterMC);  // 渲染器共享
                _monsterMVsShared.Add(mc);          // 渲染器共享
                EnsureGpuCapacity(_monsterTCs.Count);
                _gpuPositionsDirty = true;
                addedAny = true;
            }

            if (addedAny)
                _layoutVersion++;
        }

        // ── GPU 移动 Dispatch ─────────────────────────────────────────

        private void DispatchGpuMove(int count, float deltaTime)
        {
            // 获取玩家位置
            RefreshPlayerTransform();
            Vector3 playerPos = _playerTransform != null ? _playerTransform.Position : Vector3.zero;

            // 仅上传动态数据（移动方向 + 速度）
            for (int i = 0; i < count; i++)
            {
                var mc = _monsterMCs[i];
                var ai = _monsterAIs[i];
                var dir = mc.MoveDirection;
                bool lockPosition = ai != null && ai.CurrentState == AIState.Attack;
                _cpuInputArray[i] = new MonsterMoveInputGpu
                {
                    dx = dir.x,
                    dy = dir.y,
                    dz = dir.z,
                    currentSpeed = lockPosition ? -Mathf.Abs(mc.CurrentSpeed) : mc.CurrentSpeed
                };
            }
            _gpuInputBuffer.SetData(_cpuInputArray, 0, 0, count);

            // 位置只在创建/销毁/扩容后重新上传一次，避免每帧 10000 次位置拷贝
            if (_gpuPositionsDirty)
            {
                for (int i = 0; i < count; i++)
                {
                    var pos = _monsterTCs[i].Position;
                    _cpuOutputArray[i] = new MonsterMoveOutputGpu
                    {
                        px = pos.x,
                        py = pos.y,
                        pz = pos.z,
                        pad = 0f
                    };
                }
                _gpuOutputBuffer.SetData(_cpuOutputArray, 0, 0, count);
                _gpuPositionsDirty = false;
            }

            // 设置参数
            _simulateCS.SetInt(ID_MonsterCount, count);
            _simulateCS.SetFloat(ID_DeltaTime, deltaTime);
            _simulateCS.SetVector(ID_PlayerPosition, playerPos);
            _simulateCS.SetFloat(ID_PlayerMonsterMinDist, PlayerMonsterMinDist);
            _simulateCS.SetFloat(ID_SeparationDiam, SeparationDiam);

            // Kernel 0: CSMove（移动）
            _simulateCS.SetBuffer(_kernelMove, ID_MoveInputs,  _gpuInputBuffer);
            _simulateCS.SetBuffer(_kernelMove, ID_MoveOutputs, _gpuOutputBuffer);
            _simulateCS.Dispatch(_kernelMove, (count + 63) / 64, 1, 1);

            // Kernel 1: CSSeparate（怪物-玩家分离）
            _simulateCS.SetBuffer(_kernelSeparate, ID_MoveInputs,  _gpuInputBuffer);
            _simulateCS.SetBuffer(_kernelSeparate, ID_MoveOutputs, _gpuOutputBuffer);
            _simulateCS.Dispatch(_kernelSeparate, (count + 63) / 64, 1, 1);

            // 异步回读：GPU 每帧都可以继续推进，CPU 只按固定频率同步快照回 ECS，
            // 避免把“等待回读完成”变成 GPU 模拟的主循环节流点。
            _readbackFrame++;
            if (_readbackFrame < ReadbackInterval) return;
            _readbackFrame = 0;

            if (_readbackPending || _hasPendingReadback || _disposed) return;
            _readbackPending = true;
            int readbackGeneration = _gpuGeneration;
            int readbackLayoutVersion = _layoutVersion;
            int readbackCount = count;
            AsyncGPUReadback.Request(_gpuOutputBuffer, req => OnGpuReadbackComplete(req, readbackGeneration, readbackLayoutVersion, readbackCount));
        }

        // ── 将 GPU 回读结果在固定时机写入 ECS / GPU，避免异步回调直接改写当前位置 ────────────────

        private void ApplyPendingGpuReadback()
        {
            if (!_hasPendingReadback)
                return;

            _hasPendingReadback = false;
            int n = Mathf.Min(_pendingReadbackCount, _monsterTCs.Count);
            _pendingReadbackCount = 0;
            if (n <= 0)
                return;
            if (!_cpuReadbackArray.IsCreated || _gpuOutputBuffer == null)
                return;

            // GPU 链路已经完成怪物移动与玩家分离，这里只把结果同步回 ECS，
            // 不再重复执行 CPU 怪物分离和整批回写 GPU，避免 10000 怪时的高额主线程开销。
            for (int i = 0; i < n; i++)
            {
                var r = _cpuReadbackArray[i];
                _monsterTCs[i].Position = new Vector3(r.px, r.py, r.pz);
            }
        }

        private void OnGpuReadbackComplete(AsyncGPUReadbackRequest req, int generation, int layoutVersion, int readbackCount)
        {
            _readbackPending = false;
            if (_disposed || req.hasError)
                return;
            if (generation != _gpuGeneration || layoutVersion != _layoutVersion)
                return;
            if (!_cpuReadbackArray.IsCreated || _gpuOutputBuffer == null)
                return;

            var data = req.GetData<MonsterMoveOutputGpu>();
            int n = System.Math.Min(readbackCount, System.Math.Min(_monsterTCs.Count, data.Length));
            if (n <= 0)
                return;

            NativeArray<MonsterMoveOutputGpu>.Copy(data, 0, _cpuReadbackArray, 0, n);
            _pendingReadbackCount = n;
            _hasPendingReadback = true;
        }

        // ── CPU 回退（无 ComputeShader 时）───────────────────────────

        private void FallbackCpuMove(int count, float deltaTime)
        {
            RefreshPlayerTransform();
            Vector3 playerPos = _playerTransform != null ? _playerTransform.Position : Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                var tc = _monsterTCs[i];
                var mc = _monsterMCs[i];
                var ai = _monsterAIs[i];
                bool lockPosition = ai != null && ai.CurrentState == AIState.Attack;
                if (!mc.IsEnabled || mc.IsImmobilized) continue;

                if (!lockPosition && mc.MoveDirection.sqrMagnitude > 0.01f)
                    tc.Position += mc.MoveDirection * (mc.CurrentSpeed * deltaTime);

                if (_playerTransform != null)
                    ResolvePlayerMonsterSeparationAt(i, playerPos);
            }
        }

        private void ResolveAllPlayerMonsterSeparation(int count, Vector3 playerPos)
        {
            for (int i = 0; i < count; i++)
                ResolvePlayerMonsterSeparationAt(i, playerPos);
        }

        private void ResolvePlayerMonsterSeparationAt(int idx, Vector3 playerPos)
        {
            var tc = _monsterTCs[idx];
            var pos = tc.Position;
            float dx = pos.x - playerPos.x;
            float dz = pos.z - playerPos.z;
            float sqDist = dx * dx + dz * dz;

            if (sqDist < 0.0001f)
            {
                float fx;
                float fz;
                GetFallbackPlayerSeparationAxis(idx, out fx, out fz);
                tc.Position = new Vector3(
                    playerPos.x + fx * PlayerMonsterMinDist,
                    pos.y,
                    playerPos.z + fz * PlayerMonsterMinDist);
                return;
            }

            if (sqDist >= PlayerMonsterMinDistSq)
                return;

            float dist = Mathf.Sqrt(sqDist);
            float push = PlayerMonsterMinDist - dist;
            tc.Position = new Vector3(pos.x + (dx / dist) * push, pos.y, pos.z + (dz / dist) * push);
        }

        private void GetFallbackPlayerSeparationAxis(int idx, out float nx, out float nz)
        {
            var mc = _monsterMCs[idx];
            if (mc != null && mc.MoveDirection.sqrMagnitude > 0.0001f)
            {
                Vector3 dir = mc.MoveDirection.normalized;
                nx = dir.x;
                nz = dir.z;
                return;
            }

            uint hash = (uint)((idx + 1) * 73856093);
            float angle = (hash & 1023u) * (Mathf.PI * 2f / 1024f);
            nx = Mathf.Sin(angle);
            nz = Mathf.Cos(angle);
        }

        // ── 怪物间分离（CPU 空间网格）────────────────────────────────

        private void ResolveMonsterSeparation(int n)
        {
            if (n < 2) return;

            ReturnGridToPool();
            for (int i = 0; i < n; i++)
            {
                var pos = _monsterTCs[i].Position;
                int cx  = Mathf.FloorToInt(pos.x / CellSize);
                int cz  = Mathf.FloorToInt(pos.z / CellSize);
                long key = ((long)cx << 32) | (uint)cz;

                if (!_grid.TryGetValue(key, out var list))
                {
                    list = GetPooledList();
                    _grid[key] = list;
                }
                list.Add(i);
            }

            foreach (var kv in _grid)
            {
                var cellList  = kv.Value;
                int cellCount = cellList.Count;

                for (int a = 0; a < cellCount - 1; a++)
                for (int b = a + 1; b < cellCount; b++)
                    TrySeparate(cellList[a], cellList[b]);

                long key = kv.Key;
                int  cx  = (int)(key >> 32);
                int  cz  = (int)(key & 0xFFFFFFFFL);
                CheckNeighbor(cellList, cx + 1, cz);
                CheckNeighbor(cellList, cx,     cz + 1);
                CheckNeighbor(cellList, cx + 1, cz + 1);
                CheckNeighbor(cellList, cx + 1, cz - 1);
            }

            ReturnGridToPool();
        }

        private void CheckNeighbor(List<int> cellA, int nx, int nz)
        {
            long nkey = ((long)nx << 32) | (uint)nz;
            if (!_grid.TryGetValue(nkey, out var cellB)) return;
            foreach (int a in cellA)
            foreach (int b in cellB)
                TrySeparate(a, b);
        }

        // 分离死区：重叠量必须超过此值才触发推离，避免微小浮点误差导致持续震动
        private const float SeparationSlack = 0.12f;
        private const float SeparationResolveRatio = 0.72f;
        private const float StaticAttackResolveRatio = 0.42f;
        private const float SeparationMaxPushPerMonster = MonsterRadius * 0.26f;
        private const float StaticAttackMaxPushPerMonster = MonsterRadius * 0.14f;

        private void TrySeparate(int idxA, int idxB)
        {
            var posA = _monsterTCs[idxA].Position;
            var posB = _monsterTCs[idxB].Position;

            float dx = posA.x - posB.x;
            float dz = posA.z - posB.z;
            float sqDist = dx * dx + dz * dz;

            float nx;
            float nz;
            float dist;
            if (sqDist < 0.0001f)
            {
                GetFallbackSeparationAxis(posA, posB, idxA, idxB, out nx, out nz);
                dist = 0f;
            }
            else
            {
                dist = Mathf.Sqrt(sqDist);
                nx = dx / dist;
                nz = dz / dist;
            }

            float overlap = SeparationDiam - dist;
            if (overlap <= 0f) return;
            if (dist > 0f && overlap < SeparationSlack) return;

            var aiA = _monsterAIs[idxA];
            var aiB = _monsterAIs[idxB];
            bool attackLockedA = aiA != null && aiA.CurrentState == AIState.Attack;
            bool attackLockedB = aiB != null && aiB.CurrentState == AIState.Attack;
            bool bothAttackLocked = attackLockedA && attackLockedB;

            float mobilityA = GetSeparationMobility(idxA);
            float mobilityB = GetSeparationMobility(idxB);
            float totalMobility = mobilityA + mobilityB;
            if (totalMobility < 0.001f)
            {
                mobilityA = 1f;
                mobilityB = 1f;
                totalMobility = 2f;
            }

            float resolveRatio = bothAttackLocked ? StaticAttackResolveRatio : SeparationResolveRatio;
            float maxPush = bothAttackLocked ? StaticAttackMaxPushPerMonster : SeparationMaxPushPerMonster;
            float pushTotal = overlap * resolveRatio;
            float pushA = Mathf.Min(pushTotal * (mobilityA / totalMobility), maxPush);
            float pushB = Mathf.Min(pushTotal * (mobilityB / totalMobility), maxPush);
            float applied = pushA + pushB;

            if (applied <= 0.0001f)
                return;

            if (applied > overlap)
            {
                float scale = overlap / applied;
                pushA *= scale;
                pushB *= scale;
            }

            _monsterTCs[idxA].Position = posA + new Vector3(nx * pushA, 0f, nz * pushA);
            _monsterTCs[idxB].Position = posB - new Vector3(nx * pushB, 0f, nz * pushB);
        }

        private float GetSeparationMobility(int idx)
        {
            var mc = _monsterMCs[idx];
            var ai = _monsterAIs[idx];
            bool attackLocked = ai != null && ai.CurrentState == AIState.Attack;
            bool moving = mc != null && mc.MoveDirection.sqrMagnitude > 0.001f;

            if (moving) return 1.25f;
            if (attackLocked) return 0.75f;
            return 0.95f;
        }

        private void GetFallbackSeparationAxis(Vector3 posA, Vector3 posB, int idxA, int idxB, out float nx, out float nz)
        {
            Vector3 center = (posA + posB) * 0.5f;
            if (_playerTransform != null)
            {
                Vector3 playerPos = _playerTransform.Position;
                float rx = center.x - playerPos.x;
                float rz = center.z - playerPos.z;
                float radialSq = rx * rx + rz * rz;
                if (radialSq > 0.0001f)
                {
                    float radialLen = Mathf.Sqrt(radialSq);
                    nx = -rz / radialLen;
                    nz = rx / radialLen;
                    return;
                }
            }

            uint hash = (uint)(((idxA + 1) * 73856093) ^ ((idxB + 1) * 19349663));
            float angle = (hash & 1023u) * (Mathf.PI * 2f / 1024f);
            nx = Mathf.Sin(angle);
            nz = Mathf.Cos(angle);
        }

        // ── GPU Buffer 容量管理 ───────────────────────────────────────

        private void EnsureGpuCapacity(int count)
        {
            if (_gpuCapacity >= count) return;

            int capacity = Mathf.Max(count, (int)(count * 1.5f));

            _gpuInputBuffer?.Release();
            _gpuOutputBuffer?.Release();
            if (_cpuInputArray.IsCreated)    _cpuInputArray.Dispose();
            if (_cpuOutputArray.IsCreated)   _cpuOutputArray.Dispose();
            if (_cpuReadbackArray.IsCreated) _cpuReadbackArray.Dispose();

            _gpuInputBuffer   = new ComputeBuffer(capacity, InputStride);
            _gpuOutputBuffer  = new ComputeBuffer(capacity, OutputStride);
            _cpuInputArray    = new NativeArray<MonsterMoveInputGpu>(capacity, Allocator.Persistent);
            _cpuOutputArray   = new NativeArray<MonsterMoveOutputGpu>(capacity, Allocator.Persistent);
            _cpuReadbackArray = new NativeArray<MonsterMoveOutputGpu>(capacity, Allocator.Persistent);
            _gpuCapacity      = capacity;
            _gpuPositionsDirty = true;
            _hasPendingReadback = false;
            _pendingReadbackCount = 0;
            _readbackFrame = 0;
            _gpuGeneration++;
        }

        // ── 玩家 Transform 缓存刷新 ───────────────────────────────────

        private void RefreshPlayerTransform()
        {
            if (_playerEntity == null || !_playerEntity.IsAlive)
            {
                _playerEntity    = World.FindEntityByTag("Player");
                _playerTransform = null;
            }
            if (_playerTransform == null && _playerEntity != null)
                _playerTransform = _playerEntity.GetComponent<TransformComponent>();
        }

        // ── 对象池 ────────────────────────────────────────────────────

        private List<int> GetPooledList()
        {
            if (_gridPooled.Count > 0)
            {
                var l = _gridPooled[_gridPooled.Count - 1];
                _gridPooled.RemoveAt(_gridPooled.Count - 1);
                l.Clear();
                return l;
            }
            return new List<int>(8);
        }

        private void ReturnGridToPool()
        {
            foreach (var kv in _grid)
                _gridPooled.Add(kv.Value);
            _grid.Clear();
        }

        // ── 移动实现（玩家/非怪物）────────────────────────────────────

        private void UpdateCharacterControllerMovement(MovementComponent movement, float deltaTime)
        {
            var cc = movement.CharacterController;
            if (movement.UseGravity)
            {
                if (cc.isGrounded) movement.VerticalVelocity = -2f;
                else               movement.VerticalVelocity += Gravity * deltaTime;
            }
            var moveVelocity = movement.MoveDirection * movement.CurrentSpeed;
            moveVelocity.y   = movement.VerticalVelocity;
            cc.Move(moveVelocity * deltaTime);
        }

        private void UpdateRigidbodyMovement(MovementComponent movement, float deltaTime)
        {
            var rb             = movement.Rigidbody;
            var targetVelocity = movement.MoveDirection * movement.CurrentSpeed;
            targetVelocity.y   = rb.linearVelocity.y;
            rb.linearVelocity  = targetVelocity;
        }

        private void UpdateLogicMovement(TransformComponent transform, MovementComponent movement, float deltaTime)
        {
            if (movement.MoveDirection.sqrMagnitude > 0.01f)
            {
                float step = movement.CurrentSpeed * deltaTime;
                if (movement.HasTarget)
                {
                    var targetFlat = new Vector3(movement.TargetPosition.x, transform.Position.y, movement.TargetPosition.z);
                    float dist     = (targetFlat - transform.Position).magnitude;
                    step           = Mathf.Min(step, dist);
                }
                transform.Position += movement.MoveDirection * step;
            }
        }

        // ── 释放 ──────────────────────────────────────────────────────

        protected override void OnDispose()
        {
            _disposed = true;
            _readbackPending = false;
            _hasPendingReadback = false;
            _pendingReadbackCount = 0;
            _readbackFrame = 0;
            World.EventBus.Unsubscribe<EntityCreatedEvent>(OnEntityCreated);
            World.EventBus.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);

            _gpuInputBuffer?.Release();
            _gpuOutputBuffer?.Release();
            if (_cpuInputArray.IsCreated)    _cpuInputArray.Dispose();
            if (_cpuOutputArray.IsCreated)   _cpuOutputArray.Dispose();
            if (_cpuReadbackArray.IsCreated) _cpuReadbackArray.Dispose();

            _monsterTCs.Clear();
            _monsterMCs.Clear();
            _monsterAIs.Clear();
            _monsterEntities.Clear();
            _registeredMonsters.Clear();
        }
    }
}