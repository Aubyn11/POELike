using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.ECS.Systems;

namespace POELike.Game
{
    // ── MonsterMeshRenderer ───────────────────────────────────────────
    /// <summary>
    /// 怪物 Mesh 渲染器（全 GPU 驱动）
    ///
    /// 渲染链路：
    ///   CPU：ECS Query → 收集输入数据（位置/朝向）→ SetData 上传 GPU
    ///   GPU：ComputeShader 并行计算世界矩阵 → DrawMeshInstancedIndirect
    ///
    /// 相比 Burst Job + CPU 拷贝的改进：
    ///   · 消除 CPU 端矩阵计算（float4x4 TRS × 10000）
    ///   · 消除每帧 tempBuf NativeArray 分配和 CPU→GPU 矩阵拷贝
    ///   · 矩阵直接在 GPU 内存中生成，DrawCall 零拷贝读取
    ///   · CPU 每帧只上传输入数据（32 bytes/实体，远小于 64 bytes/矩阵）
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MonsterMeshRenderer : MonoBehaviour
    {
        // ── ECS 引用 ────────────────────────────────────────────
        private World          _world;
        private Camera         _camera;
        private MovementSystem _movementSystem; // 直接读取 GPU 位置 Buffer，零 CPU 中转
        private Entity         _playerEntity;
        private TransformComponent _playerTransform;

        // ── 可调参数 ──────────────────────────────────────────────────
        [Header("模型设置")]
        [SerializeField] private float _modelScale      = 1f;
        [SerializeField] private float _yOffset         = 0f;
        [SerializeField] private float _rotationOffset  = 0f;
        [SerializeField] private float _rotationOffsetX = 0f;

        [Header("ComputeShader")]
        [SerializeField] private ComputeShader _matrixCompute;

        // ── Bundle 缓存 ───────────────────────────────────────────────
        private const string BundlePath = "Monsters/";

        // ── MeshType 注册表 ───────────────────────────────────────────
        private readonly List<string>                                               _meshTypeNames    = new List<string>();
        private readonly List<List<(Mesh mesh, Material mat, Matrix4x4 localMat)>> _meshTypeParts    = new List<List<(Mesh, Material, Matrix4x4)>>();
        private readonly Dictionary<string, int>                                    _meshTypeIndexMap = new Dictionary<string, int>();

        // ── 组件缓存（直接列表，与实体列表下标对应，消除字典查找）────
        private readonly List<Entity>           _monsterEntities = new List<Entity>(16384);
        private readonly List<MonsterComponent> _monsterMCs      = new List<MonsterComponent>(16384);
        private readonly List<TransformComponent> _monsterTCs    = new List<TransformComponent>(16384);
        private readonly List<MovementComponent>  _monsterMVs    = new List<MovementComponent>(16384);
        private readonly HashSet<Entity>          _registered    = new HashSet<Entity>();
        private bool _cacheDirty = true; // 首帧强制同步

        // ── GPU 输入 Buffer（CPU 每帧上传实体数据）────────────────────
        // MonsterInput 结构体：position(float3) + faceYaw(float) + meshTypeIndex(int) + typeInstanceIndex(int) + pad*2
        // 对应 ComputeShader 中的 MonsterInput，stride = 32 bytes
        private const int InputStride = 32;
        private ComputeBuffer _inputGpuBuffer;
        private int           _inputGpuCapacity;

        // CPU 端输入暂存（NativeArray，用于 SetData，避免托管数组 GC）
        private NativeArray<MonsterInputGpu> _inputCpuBuffer;

        // ── GPU 静态 Buffer（类型注册时上传，不变）────────────────────
        private ComputeBuffer _localMatricesGpu;
        private ComputeBuffer _partCountsGpu;
        private ComputeBuffer _partOffsetsGpu;
        private ComputeBuffer _batchOffsetsGpu;

        private int _maxPartsPerType;

        // ── GPU 输出 Buffer（矩阵，ComputeShader 直接写入）────────────
        private ComputeBuffer _outputMatricesGpu;
        private int           _outputGpuCapacity;

        // ── GPU Indirect Instancing 资源 ──────────────────────────────
        private readonly Dictionary<(int, int), IndirectBatch> _batches = new Dictionary<(int, int), IndirectBatch>();
        private const int ArgsStride = sizeof(uint) * 5;

        // ── 静态 Buffer 是否已绑定到 ComputeShader（只需绑定一次）────
        private bool _staticBuffersBound = false;
        // 上一帧有效实体数量（避免重复 SetData）
        private int _lastValidCount = -1;
        // 材质 Buffer 是否已绑定（输出矩阵 Buffer 重建时才需要重新绑定）
        private bool _materialBufferBound = false;
        // 缓存 Bounds 对象，避免每帧 new
        private static readonly Bounds WorldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f);

        // ── 零 GC Query 缓冲（仅用于增量同步缓存）────────────────────
        private readonly List<Entity> _queryBuffer = new List<Entity>(16384);
        // ── 每种 MeshType 的当前实例数（用于间接绘制 args）────────────
        private readonly List<int> _meshTypeInstanceCounts = new List<int>(8);

        // ── 视锥剔除 ──────────────────────────────────────────────────
        [Header("视锥剔除")]
        [Tooltip("怪物包围球半径（用于视锥剔除，设为模型最大半径）")]
        [SerializeField] private float _cullRadius = 1f;
        private Plane[] _frustumPlanes = new Plane[6];

        // ── ComputeShader Kernel ──────────────────────────────────────
        private int _csKernel;
        private static readonly int ID_Inputs          = Shader.PropertyToID("_Inputs");
        private static readonly int ID_LocalMatrices   = Shader.PropertyToID("_LocalMatrices");
        private static readonly int ID_PartCounts      = Shader.PropertyToID("_PartCounts");
        private static readonly int ID_PartOffsets     = Shader.PropertyToID("_PartOffsets");
        private static readonly int ID_BatchOffsets    = Shader.PropertyToID("_BatchOffsets");
        private static readonly int ID_OutputMatrices  = Shader.PropertyToID("_OutputMatrices");
        private static readonly int ID_EntityCount     = Shader.PropertyToID("_EntityCount");
        private static readonly int ID_MaxPartsPerType = Shader.PropertyToID("_MaxPartsPerType");
        private static readonly int ID_YOffset         = Shader.PropertyToID("_YOffset");
        private static readonly int ID_RotationOffset  = Shader.PropertyToID("_RotationOffset");
        private static readonly int ID_RotationOffsetX = Shader.PropertyToID("_RotationOffsetX");
        private static readonly int ID_ModelScale      = Shader.PropertyToID("_ModelScale");
        private static readonly int ID_GpuPositions    = Shader.PropertyToID("_GpuPositions");
        private static readonly int ID_UseGpuPosition  = Shader.PropertyToID("_UseGpuPosition");

        // ── 每个(类型,部件)对应的 GPU 资源 ───────────────────────────
        private class IndirectBatch
        {
            public ComputeBuffer ArgsBuffer;
            public Material      Material;
            public Mesh          Mesh;
            public int           BatchOffset;
            public uint[]        Args = new uint[5];

            public void Dispose()
            {
                ArgsBuffer?.Release();
                if (Material != null) Object.Destroy(Material);
            }
        }

        // ── CPU 端输入结构体（与 ComputeShader MonsterInput 对齐）─────
        private struct MonsterInputGpu
        {
            public float px, py, pz;
            public float faceYaw;
            public int   meshTypeIndex;
            public int   typeInstanceIndex;
            public int   pad0, pad1;
        }

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // 自动加载 ComputeShader（优先使用 Inspector 赋值，否则从 Resources 加载）
            if (_matrixCompute == null)
                _matrixCompute = Resources.Load<ComputeShader>("Shaders/MonsterMatrixCompute");

            if (_matrixCompute != null)
                _csKernel = _matrixCompute.FindKernel("CSMain");
            else
                Debug.LogError("[MonsterMeshRenderer] 找不到 ComputeShader 'Shaders/MonsterMatrixCompute'，请确认文件位于 Assets/Resources/Shaders/ 目录下。");
        }

        public void SetWorld(World world)
        {
            _world = world;
            world.EventBus.Subscribe<EntityCreatedEvent>(OnEntityCreated);
            world.EventBus.Subscribe<EntityDestroyedEvent>(OnEntityDestroyed);
        }

        /// <summary>注入 MovementSystem，启用 GPU 位置直读（零 CPU 中转）</summary>
        public void SetMovementSystem(MovementSystem ms)
        {
            _movementSystem = ms;
        }

        private void OnEntityCreated(EntityCreatedEvent evt)
        {
            if (evt.Entity.Tag == "Monster") _cacheDirty = true;
        }

        private void OnEntityDestroyed(EntityDestroyedEvent evt)
        {
            if (evt.Entity.Tag != "Monster") return;
            if (!_registered.Contains(evt.Entity)) return;
            _registered.Remove(evt.Entity);
            int idx = _monsterEntities.IndexOf(evt.Entity);
            if (idx < 0) return;
            int last = _monsterEntities.Count - 1;
            _monsterEntities[idx] = _monsterEntities[last];
            _monsterMCs[idx]      = _monsterMCs[last];
            _monsterTCs[idx]      = _monsterTCs[last];
            _monsterMVs[idx]      = _monsterMVs[last];
            _monsterEntities.RemoveAt(last);
            _monsterMCs.RemoveAt(last);
            _monsterTCs.RemoveAt(last);
            _monsterMVs.RemoveAt(last);
        }

        // ── 每帧渲染 ──────────────────────────────────────────────────

        private void Update()
        {
            if (_world == null || _camera == null || _matrixCompute == null) return;

            // ── Step1：获取怪物数据来源 ───────────────────────────────
            // 优先使用 MovementSystem 的共享缓存（顺序与 GPU Buffer 完全一致）
            // 回退到自己的 ECS 缓存（MovementSystem 未注入时）
            bool useSharedCache = _movementSystem != null
                               && _movementSystem.MonsterCount > 0;

            int entityCount;
            System.Collections.Generic.List<MonsterComponent>  mcList;
            System.Collections.Generic.List<MovementComponent> mvList;
            System.Collections.Generic.List<TransformComponent> tcList;
            System.Collections.Generic.List<AIComponent> aiList = null;

            if (useSharedCache)
            {
                entityCount = _movementSystem.MonsterCount;
                mcList      = _movementSystem.MonsterMCsShared;
                mvList      = _movementSystem.MonsterMVsShared;
                tcList      = _movementSystem.MonsterTCsShared;
                aiList      = _movementSystem.MonsterAIsShared;
            }
            else
            {
                SyncMonsterCache();
                entityCount = _monsterEntities.Count;
                mcList      = _monsterMCs;
                mvList      = _monsterMVs;
                tcList      = _monsterTCs;
            }

            if (entityCount == 0) return;

            RefreshPlayerTransform();

            // ── Step2：确保 GPU Buffer 容量 ──────────────────────────
            EnsureGpuCapacity(entityCount);
            ResetMeshTypeInstanceCounts();

            // ── Step3：判断是否可以使用 GPU 直读位置（零 CPU 中转）────
            bool useGpuPos = useSharedCache
                          && _movementSystem.GpuPositionBuffer != null;

            // ── Step4：遍历缓存列表，收集朝向、类型和该类型内的实例序号 ──
            // 注意：当 useGpuPos=true 时，位置由 GPU 直接读取，CPU 只需上传 faceYaw+meshTypeIndex+typeInstanceIndex
            float dt = Time.deltaTime;
            int validCount = 0;
            for (int i = 0; i < entityCount; i++)
            {
                var monsterComp  = mcList[i];
                var movementComp = mvList[i];
                var transformComp = tcList[i];
                var aiComp = aiList != null ? aiList[i] : null;

                if (monsterComp == null || string.IsNullOrEmpty(monsterComp.MonsterMesh))
                {
                    if (useGpuPos)
                    {
                        // GPU 直读模式下不能跳过，否则下标错位；用无效类型占位
                        _inputCpuBuffer[validCount] = new MonsterInputGpu
                        {
                            meshTypeIndex     = -1,
                            typeInstanceIndex = -1,
                        };
                        validCount++;
                    }
                    continue;
                }

                bool faceTarget = false;
                if (aiComp != null
                    && aiComp.Target != null
                    && transformComp != null
                    && (aiComp.CurrentState == AIState.Chase || aiComp.CurrentState == AIState.Attack))
                {
                    var targetTransform = aiComp.Target == _playerEntity
                        ? _playerTransform
                        : aiComp.Target.GetComponent<TransformComponent>();

                    if (targetTransform != null)
                    {
                        float dx = targetTransform.Position.x - transformComp.Position.x;
                        float dz = targetTransform.Position.z - transformComp.Position.z;
                        if (dx * dx + dz * dz > 0.0001f)
                        {
                            float targetYaw = Mathf.Atan2(dx, dz) * 57.29578f;
                            float delta = Mathf.DeltaAngle(monsterComp.FaceYaw, targetYaw);
                            float step = 540f * dt;
                            if (delta > step) monsterComp.FaceYaw += step;
                            else if (delta < -step) monsterComp.FaceYaw -= step;
                            else monsterComp.FaceYaw = targetYaw;
                            faceTarget = true;
                        }
                    }
                }

                // 将移动方向同步到朝向角（FaceYaw），供 GPU 渲染使用
                if (!faceTarget && movementComp != null && movementComp.MoveDirection.sqrMagnitude > 0.04f)
                {
                    var dir = movementComp.MoveDirection;
                    float targetYaw = Mathf.Atan2(dir.x, dir.z) * 57.29578f;

                    float delta = Mathf.DeltaAngle(monsterComp.FaceYaw, targetYaw);
                    float step = 180f * dt;
                    if (delta > step) monsterComp.FaceYaw += step;
                    else if (delta < -step) monsterComp.FaceYaw -= step;
                    else monsterComp.FaceYaw = targetYaw;
                }

                int typeIdx = GetOrRegisterMeshType(monsterComp.MonsterMesh);
                int typeInstanceIndex = AllocateTypeInstanceIndex(typeIdx);

                if (useGpuPos)
                {
                    // GPU 直读模式：位置由 ComputeShader 从 _GpuPositions 读取，CPU 只传朝向和类型
                    _inputCpuBuffer[validCount] = new MonsterInputGpu
                    {
                        px = 0, py = 0, pz = 0,   // 占位，GPU 不使用
                        faceYaw           = monsterComp.FaceYaw,
                        meshTypeIndex     = typeIdx,
                        typeInstanceIndex = typeInstanceIndex,
                    };
                }
                else
                {
                    // 回退模式：从 ECS TransformComponent 读取位置
                    if (transformComp == null)
                    {
                        _inputCpuBuffer[validCount] = new MonsterInputGpu
                        {
                            meshTypeIndex     = -1,
                            typeInstanceIndex = -1,
                        };
                        validCount++;
                        continue;
                    }

                    float px2 = transformComp.Position.x;
                    float py2 = transformComp.Position.y;
                    float pz2 = transformComp.Position.z;
                    _inputCpuBuffer[validCount] = new MonsterInputGpu
                    {
                        px                = px2,
                        py                = py2,
                        pz                = pz2,
                        faceYaw           = monsterComp.FaceYaw,
                        meshTypeIndex     = typeIdx,
                        typeInstanceIndex = typeInstanceIndex,
                    };
                }
                validCount++;
            }

            if (validCount == 0) return;

            // 类型可能在填充输入时首次注册，需再次确保输出 Buffer 与批次偏移已按最新类型表重建
            EnsureGpuCapacity(validCount);

            // ── Step5：上传输入数据到 GPU ─────────────────────────────
            _inputGpuBuffer.SetData(_inputCpuBuffer, 0, 0, validCount);

            // ── Step6：Dispatch ComputeShader ────────────────────────
            // 静态 Buffer 只在首次或重建后绑定一次，避免每帧重复绑定
            if (!_staticBuffersBound)
            {
                _matrixCompute.SetBuffer(_csKernel, ID_LocalMatrices,  _localMatricesGpu);
                _matrixCompute.SetBuffer(_csKernel, ID_PartCounts,     _partCountsGpu);
                _matrixCompute.SetBuffer(_csKernel, ID_PartOffsets,    _partOffsetsGpu);
                _matrixCompute.SetBuffer(_csKernel, ID_BatchOffsets,   _batchOffsetsGpu);
                _matrixCompute.SetBuffer(_csKernel, ID_OutputMatrices, _outputMatricesGpu);
                _matrixCompute.SetInt(ID_MaxPartsPerType, _maxPartsPerType);
                _matrixCompute.SetFloat(ID_YOffset,         _yOffset);
                _matrixCompute.SetFloat(ID_RotationOffset,  _rotationOffset);
                _matrixCompute.SetFloat(ID_RotationOffsetX, _rotationOffsetX);
                _matrixCompute.SetFloat(ID_ModelScale,      _modelScale);
                _staticBuffersBound = true;
            }

            // GPU 位置 Buffer 绑定（每帧检查，因为 MovementSystem 可能重建 Buffer）
            if (useGpuPos)
            {
                _matrixCompute.SetBuffer(_csKernel, ID_GpuPositions, _movementSystem.GpuPositionBuffer);
                _matrixCompute.SetInt(ID_UseGpuPosition, 1);
            }
            else
            {
                _matrixCompute.SetInt(ID_UseGpuPosition, 0);
            }

            // 动态 Buffer 和实体数量每帧更新
            _matrixCompute.SetBuffer(_csKernel, ID_Inputs, _inputGpuBuffer);
            _matrixCompute.SetInt(ID_EntityCount, validCount);
            _matrixCompute.Dispatch(_csKernel, (validCount + 63) / 64, 1, 1);

            // ── Step7：DrawMeshInstancedIndirect ─────────────────────
            // 材质 Buffer 只在输出矩阵 Buffer 重建后绑定一次
            if (!_materialBufferBound)
            {
                foreach (var kv in _batches)
                {
                    kv.Value.Material.SetBuffer("_MatrixBuffer", _outputMatricesGpu);
                    kv.Value.Material.SetInt("_BatchOffset", kv.Value.BatchOffset);
                }
                _materialBufferBound = true;
            }
            foreach (var kv in _batches)
            {
                int typeIdx = kv.Key.Item1;
                var batch = kv.Value;
                int instanceCount = typeIdx < _meshTypeInstanceCounts.Count
                    ? _meshTypeInstanceCounts[typeIdx]
                    : 0;

                EnsureArgsBuffer(batch, instanceCount);
                if (instanceCount <= 0) continue;

                Graphics.DrawMeshInstancedIndirect(
                    batch.Mesh, 0, batch.Material,
                    WorldBounds,
                    batch.ArgsBuffer,
                    castShadows: ShadowCastingMode.On,
                    receiveShadows: true);
            }
        }

        private void SyncMonsterCache()
        {
            if (!_cacheDirty) return;
            _cacheDirty = false;

            _world.Query<MonsterComponent>(_queryBuffer);

            foreach (var entity in _queryBuffer)
            {
                if (_registered.Contains(entity)) continue;

                var mc = entity.GetComponent<MonsterComponent>();
                var tc = entity.GetComponent<TransformComponent>();
                var mv = entity.GetComponent<MovementComponent>();
                if (mc == null || tc == null) continue;

                _monsterEntities.Add(entity);
                _monsterMCs.Add(mc);
                _monsterTCs.Add(tc);
                _monsterMVs.Add(mv);
                _registered.Add(entity);
            }
        }

        private void RefreshPlayerTransform()
        {
            if (_world == null) return;

            if (_playerEntity == null || !_playerEntity.IsAlive)
            {
                _playerEntity = _world.FindEntityByTag("Player");
                _playerTransform = null;
            }

            if (_playerTransform == null && _playerEntity != null)
                _playerTransform = _playerEntity.GetComponent<TransformComponent>();
        }

        private void ResetMeshTypeInstanceCounts()
        {
            while (_meshTypeInstanceCounts.Count < _meshTypeParts.Count)
                _meshTypeInstanceCounts.Add(0);

            for (int i = 0; i < _meshTypeInstanceCounts.Count; i++)
                _meshTypeInstanceCounts[i] = 0;
        }

        private int AllocateTypeInstanceIndex(int typeIdx)
        {
            while (_meshTypeInstanceCounts.Count <= typeIdx)
                _meshTypeInstanceCounts.Add(0);

            int typeInstanceIndex = _meshTypeInstanceCounts[typeIdx];
            _meshTypeInstanceCounts[typeIdx] = typeInstanceIndex + 1;
            return typeInstanceIndex;
        }

        // ── GPU Buffer 容量管理 ───────────────────────────────────────

        private void EnsureGpuCapacity(int entityCount)
        {
            if (_inputGpuCapacity >= entityCount && _outputGpuCapacity >= entityCount) return;

            int capacity = Mathf.Max(entityCount, (int)(entityCount * 1.5f));

            // 输入 Buffer
            if (_inputGpuCapacity < capacity)
            {
                _inputGpuBuffer?.Release();
                _inputGpuBuffer    = new ComputeBuffer(capacity, InputStride);
                _inputGpuCapacity  = capacity;

                if (_inputCpuBuffer.IsCreated) _inputCpuBuffer.Dispose();
                _inputCpuBuffer    = new NativeArray<MonsterInputGpu>(capacity, Allocator.Persistent);
            }

            // 输出 Buffer（所有批次共享，按 batchOffset + entityIdx 索引）
            if (_outputGpuCapacity < capacity)
            {
                _outputMatricesGpu?.Release();
                int typeCount  = Mathf.Max(_meshTypeParts.Count, 1);
                int maxParts   = Mathf.Max(_maxPartsPerType, 1);
                int totalSlots = typeCount * maxParts * capacity;
                _outputMatricesGpu  = new ComputeBuffer(totalSlots, 64); // float4x4 = 64 bytes
                _outputGpuCapacity  = capacity;

                // 重建 batchOffsets
                RebuildBatchOffsets(capacity);
            }
        }

        private void RebuildBatchOffsets(int capacity)
        {
            _batchOffsetsGpu?.Release();
            // batchOffsets 重建后需要重新绑定静态 Buffer 和材质 Buffer
            _staticBuffersBound  = false;
            _materialBufferBound = false;

            int typeCount = _meshTypeParts.Count;
            int maxParts  = Mathf.Max(_maxPartsPerType, 1);
            var offsets   = new int[typeCount * maxParts];

            for (int t = 0; t < typeCount; t++)
            {
                int partCount = _meshTypeParts[t].Count;
                for (int p = 0; p < partCount; p++)
                {
                    int batchIdx = t * maxParts + p;
                    offsets[batchIdx] = batchIdx * capacity; // 每个批次占 capacity 个槽位

                    // 同步更新 IndirectBatch.BatchOffset
                    if (_batches.TryGetValue((t, p), out var batch))
                        batch.BatchOffset = offsets[batchIdx];
                }
            }

            _batchOffsetsGpu = new ComputeBuffer(Mathf.Max(offsets.Length, 1), sizeof(int));
            _batchOffsetsGpu.SetData(offsets);
        }

        private void EnsureArgsBuffer(IndirectBatch batch, int instanceCount)
        {
            // 实例数量未变时跳过 SetData，避免每帧 GPU 上传
            if (batch.ArgsBuffer != null && batch.Args[1] == (uint)instanceCount) return;

            if (batch.ArgsBuffer == null)
            {
                batch.ArgsBuffer = new ComputeBuffer(1, ArgsStride, ComputeBufferType.IndirectArguments);
                batch.Args[0] = batch.Mesh.GetIndexCount(0);
                batch.Args[2] = batch.Mesh.GetIndexStart(0);
                batch.Args[3] = (uint)batch.Mesh.GetBaseVertex(0);
                batch.Args[4] = 0;
            }

            batch.Args[1] = (uint)instanceCount;
            batch.ArgsBuffer.SetData(batch.Args);
        }

        // ── 静态 GPU Buffer 重建（类型注册时调用）────────────────────

        private void RebuildStaticGpuBuffers()
        {
            _localMatricesGpu?.Release();
            _partCountsGpu?.Release();
            _partOffsetsGpu?.Release();
            // 标记静态 Buffer 和材质 Buffer 需要重新绑定
            _staticBuffersBound  = false;
            _materialBufferBound = false;

            int typeCount  = _meshTypeParts.Count;
            var partCounts  = new int[typeCount];
            var partOffsets = new int[typeCount];
            int totalParts  = 0;
            _maxPartsPerType = 0;

            for (int t = 0; t < typeCount; t++)
            {
                int cnt = _meshTypeParts[t].Count;
                partCounts[t]  = cnt;
                partOffsets[t] = totalParts;
                totalParts     += cnt;
                if (cnt > _maxPartsPerType) _maxPartsPerType = cnt;
            }

            // 本地矩阵（float4x4 数组）
            var localMats = new Matrix4x4[Mathf.Max(totalParts, 1)];
            for (int t = 0; t < typeCount; t++)
            {
                var parts  = _meshTypeParts[t];
                int offset = partOffsets[t];
                for (int p = 0; p < parts.Count; p++)
                    localMats[offset + p] = parts[p].localMat;
            }

            _localMatricesGpu = new ComputeBuffer(Mathf.Max(totalParts, 1), 64);
            _localMatricesGpu.SetData(localMats);

            _partCountsGpu = new ComputeBuffer(Mathf.Max(typeCount, 1), sizeof(int));
            _partCountsGpu.SetData(partCounts);

            _partOffsetsGpu = new ComputeBuffer(Mathf.Max(typeCount, 1), sizeof(int));
            _partOffsetsGpu.SetData(partOffsets);

            // 输出 buffer 容量变化时重建
            _outputGpuCapacity = 0; // 强制下次 EnsureGpuCapacity 重建输出 buffer
        }

        // ── MeshType 注册 ─────────────────────────────────────────────

        private int GetOrRegisterMeshType(string meshName)
        {
            if (_meshTypeIndexMap.TryGetValue(meshName, out int idx)) return idx;

            var parts  = LoadBundle(meshName);
            int newIdx = _meshTypeNames.Count;
            _meshTypeNames.Add(meshName);
            _meshTypeParts.Add(parts);
            _meshTypeIndexMap[meshName] = newIdx;

            for (int p = 0; p < parts.Count; p++)
            {
                var (mesh, mat, _) = parts[p];
                _batches[(newIdx, p)] = new IndirectBatch
                {
                    Mesh     = mesh,
                    Material = new Material(mat),
                };
            }

            RebuildStaticGpuBuffers();
            return newIdx;
        }

        private List<(Mesh, Material, Matrix4x4)> LoadBundle(string meshName)
        {
            var parts  = new List<(Mesh, Material, Matrix4x4)>();
            string key = BundlePath + meshName + "_Bundle";
            var bundle = Resources.Load<NpcMeshBundle>(key);

            if (bundle == null)
            {
                Debug.LogWarning($"[MonsterMeshRenderer] 找不到 NpcMeshBundle: Resources/{key}");
                return parts;
            }

            var indirectShader = Shader.Find("POELike/MonsterIndirect");
            if (indirectShader == null)
                Debug.LogWarning("[MonsterMeshRenderer] 找不到 Shader 'POELike/MonsterIndirect'");

            foreach (var part in bundle.Parts)
            {
                if (part.Mesh == null || part.Material == null) continue;

                Material indirectMat;
                if (indirectShader != null)
                {
                    indirectMat = new Material(indirectShader);
                    if (part.Material.HasProperty("_MainTex"))
                        indirectMat.SetTexture("_MainTex", part.Material.GetTexture("_MainTex"));
                    if (part.Material.HasProperty("_BaseMap"))
                        indirectMat.SetTexture("_MainTex", part.Material.GetTexture("_BaseMap"));
                }
                else
                {
                    indirectMat = new Material(part.Material);
                    indirectMat.enableInstancing = true;
                }

                parts.Add((part.Mesh, indirectMat, part.LocalMatrix));
            }

            Debug.Log($"[MonsterMeshRenderer] 加载 Bundle [{meshName}]，共 {parts.Count} 个部件。");
            return parts;
        }

        // ── 释放 ──────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _inputGpuBuffer?.Release();
            _outputMatricesGpu?.Release();
            _localMatricesGpu?.Release();
            _partCountsGpu?.Release();
            _partOffsetsGpu?.Release();
            _batchOffsetsGpu?.Release();

            if (_inputCpuBuffer.IsCreated) _inputCpuBuffer.Dispose();

            if (_world != null)
            {
                _world.EventBus.Unsubscribe<EntityCreatedEvent>(OnEntityCreated);
                _world.EventBus.Unsubscribe<EntityDestroyedEvent>(OnEntityDestroyed);
            }
            foreach (var b in _batches.Values) b.Dispose();
            _batches.Clear();
            _meshTypeNames.Clear();
            _meshTypeParts.Clear();
            _meshTypeIndexMap.Clear();
            _monsterEntities.Clear();
            _monsterMCs.Clear();
            _monsterTCs.Clear();
            _monsterMVs.Clear();
            _registered.Clear();
        }
    }
}