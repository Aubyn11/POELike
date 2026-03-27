using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using POELike.ECS.Core;
using POELike.ECS.Components;

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
        // ── ECS 引用 ──────────────────────────────────────────────────
        private World  _world;
        private Camera _camera;

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

        // ── 组件缓存（避免每帧 GetComponent）────────────────────────
        private readonly Dictionary<Entity, (MonsterComponent mc, TransformComponent tc)> _compCache
            = new Dictionary<Entity, (MonsterComponent, TransformComponent)>();

        // ── GPU 输入 Buffer（CPU 每帧上传实体数据）────────────────────
        // MonsterInput 结构体：position(float3) + faceYaw(float) + meshTypeIndex(int) + pad*3
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

        // ── 零 GC Query 缓冲 ──────────────────────────────────────────
        private readonly List<Entity> _queryBuffer = new List<Entity>(16384);

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
            public int   pad0, pad1, pad2;
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
        }

        // ── 每帧渲染 ──────────────────────────────────────────────────

        private void Update()
        {
            if (_world == null || _camera == null || _matrixCompute == null) return;

            // ── Step1：Query 实体 ────────────────────────────────────
            _world.Query<MonsterComponent, TransformComponent>(_queryBuffer);
            int entityCount = _queryBuffer.Count;
            if (entityCount == 0) return;

            // ── Step2：确保 GPU Buffer 容量 ──────────────────────────
            EnsureGpuCapacity(entityCount);

            // ── Step3：提取视锥平面（每帧一次）────────────────────
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);

            // ── Step4：一次遍历：视锥剔除 + 注册类型 + 收集输入数据 ──
            int validCount = 0;
            for (int i = 0; i < entityCount; i++)
            {
                var entity = _queryBuffer[i];

                // 从缓存取组件，缓存未命中才调用 GetComponent
                if (!_compCache.TryGetValue(entity, out var cached))
                {
                    var mc = entity.GetComponent<MonsterComponent>();
                    var tc = entity.GetComponent<TransformComponent>();
                    cached = (mc, tc);
                    _compCache[entity] = cached;
                }

                var (monsterComp, transformComp) = cached;

                if (monsterComp == null || transformComp == null ||
                    string.IsNullOrEmpty(monsterComp.MonsterMesh))
                {
                    continue;
                }

                // ── 视锥剔除：包围球测试 ─────────────────────────────
                var pos = transformComp.Position + new Vector3(0f, _yOffset, 0f);
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes,
                    new Bounds(pos, Vector3.one * (_cullRadius * 2f))))
                    continue;

                int typeIdx = GetOrRegisterMeshType(monsterComp.MonsterMesh);
                _inputCpuBuffer[validCount] = new MonsterInputGpu
                {
                    px            = pos.x,
                    py            = pos.y - _yOffset, // 还原原始 Y，Shader 端加 offset
                    pz            = pos.z,
                    faceYaw       = monsterComp.FaceYaw,
                    meshTypeIndex = typeIdx,
                };
                validCount++;
            }

            if (validCount == 0) return;

            // ── Step5：上传输入数据到 GPU（只上传可见实体）────────────
            _inputGpuBuffer.SetData(_inputCpuBuffer, 0, 0, validCount);

            // ── Step5：Dispatch ComputeShader ────────────────────────
            _matrixCompute.SetBuffer(_csKernel, ID_Inputs,         _inputGpuBuffer);
            _matrixCompute.SetBuffer(_csKernel, ID_LocalMatrices,  _localMatricesGpu);
            _matrixCompute.SetBuffer(_csKernel, ID_PartCounts,     _partCountsGpu);
            _matrixCompute.SetBuffer(_csKernel, ID_PartOffsets,    _partOffsetsGpu);
            _matrixCompute.SetBuffer(_csKernel, ID_BatchOffsets,   _batchOffsetsGpu);
            _matrixCompute.SetBuffer(_csKernel, ID_OutputMatrices, _outputMatricesGpu);
            _matrixCompute.SetInt(ID_EntityCount,     validCount);
            _matrixCompute.SetInt(ID_MaxPartsPerType, _maxPartsPerType);
            _matrixCompute.SetFloat(ID_YOffset,         _yOffset);
            _matrixCompute.SetFloat(ID_RotationOffset,  _rotationOffset);
            _matrixCompute.SetFloat(ID_RotationOffsetX, _rotationOffsetX);
            _matrixCompute.SetFloat(ID_ModelScale,      _modelScale);
            _matrixCompute.Dispatch(_csKernel, (validCount + 63) / 64, 1, 1);

            // ── Step6：DrawMeshInstancedIndirect ─────────────────────
            foreach (var kv in _batches)
            {
                var batch = kv.Value;
                EnsureArgsBuffer(batch, validCount);
                batch.Material.SetBuffer("_MatrixBuffer", _outputMatricesGpu);
                batch.Material.SetInt("_BatchOffset", batch.BatchOffset);
                Graphics.DrawMeshInstancedIndirect(
                    batch.Mesh, 0, batch.Material,
                    new Bounds(Vector3.zero, Vector3.one * 10000f),
                    batch.ArgsBuffer,
                    castShadows: ShadowCastingMode.On,
                    receiveShadows: true);
            }
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

            foreach (var b in _batches.Values) b.Dispose();
            _batches.Clear();
            _meshTypeNames.Clear();
            _meshTypeParts.Clear();
            _meshTypeIndexMap.Clear();
            _compCache.Clear();
        }
    }
}