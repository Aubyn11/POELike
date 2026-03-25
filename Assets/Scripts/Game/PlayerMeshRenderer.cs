using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game
{
    /// <summary>
    /// 玩家角色 Mesh 渲染器（无 GameObject，纯 GPU 渲染）
    /// 挂载在摄像机 GameObject 上，不创建任何额外 GameObject。
    /// QX.fbx 包含 7 个独立 Mesh（每个部位一个），分别对应一个材质和贴图。
    /// 每帧通过 Graphics.DrawMesh + MaterialPropertyBlock 渲染，
    /// 贴图完全由 Shader 属性控制，不在 CPU 上做材质计算。
    /// </summary>
    public class PlayerMeshRenderer : MonoBehaviour
    {
        // ── ECS 引用 ──────────────────────────────────────────────────
        private Entity             _playerEntity;
        private TransformComponent _playerTransform;
        private MovementComponent  _playerMovement;

        // ── 渲染资源（每个部位一组）──────────────────────────────────
        private Mesh[]               _meshes;
        private Material[]           _materials;
        private MaterialPropertyBlock[] _propBlocks;

        // ── 可调参数 ──────────────────────────────────────────────────
        [Header("模型设置")]
        [Tooltip("模型缩放（QX.fbx 原始单位为厘米，0.01 = 1m）")]
        [SerializeField] private float _modelScale      = 1.5f;
        [Tooltip("模型在 Y 轴上的额外偏移（脚底对齐地面）")]
        [SerializeField] private float _yOffset         = 0f;
        [Tooltip("模型朝向旋转偏移（度），用于修正 FBX 导入朝向（Y轴）")]
        [SerializeField] private float _rotationOffset  = 0f;
        [Tooltip("模型X轴旋转偏移（度），用于修正 FBX 躺倒问题，-90 = 站立）")]
        [SerializeField] private float _rotationOffsetX = -90f;

        // ── 旋转平滑 ──────────────────────────────────────────────────
        private float _currentYaw             = 0f;
        private const float RotateSmoothSpeed = 720f; // 度/秒

        // ── 资源路径 ──────────────────────────────────────────────────
        private const string FbxPath = "FBX/QX/Model/QX";

        /// <summary>
        /// 部位名称关键字 → 材质路径 → 贴图路径，顺序与 FBX meta 材质槽一致
        /// </summary>
        private static readonly (string meshKeyword, string matPath, string texPath)[] PartDefs = new[]
        {
            ("Alpha", "FBX/QX/Model/MI_R2T1QianXiaMd10011Alpha", "FBX/QX/Textures/T_R2T1QianXiaMd10011Alpha_D"),
            ("Bangs", "FBX/QX/Model/MI_R2T1QianXiaMd10011Bangs", "FBX/QX/Textures/T_R2T1QianXiaMd10011Bangs_D"),
            ("Down",  "FBX/QX/Model/MI_R2T1QianXiaMd10011Down",  "FBX/QX/Textures/T_R2T1QianXiaMd10011Down_D"),
            ("Eye",   "FBX/QX/Model/MI_R2T1QianXiaMd10011Eye",   "FBX/QX/Textures/T_R2T1QianXiaMd10011Eye_D"),
            ("Face",  "FBX/QX/Model/MI_R2T1QianXiaMd10011Face",  "FBX/QX/Textures/T_R2T1QianXiaMd10011Face_D"),
            ("Hair",  "FBX/QX/Model/MI_R2T1QianXiaMd10011Hair",  "FBX/QX/Textures/T_R2T1QianXiaMd10011Hair_D"),
            ("Up",    "FBX/QX/Model/MI_R2T1QianXiaMd10011Up",    "FBX/QX/Textures/T_R2T1QianXiaMd10011Up_D"),
        };

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            LoadAssets();
        }

        private void LoadAssets()
        {
            // ── 加载 FBX 中所有 Mesh ──────────────────────────────────
            // QX.fbx 包含 7 个独立 Mesh，每个部位一个，名称含部位关键字
            var allMeshes = Resources.LoadAll<Mesh>(FbxPath);
            if (allMeshes == null || allMeshes.Length == 0)
            {
                Debug.LogError($"[PlayerMeshRenderer] 无法从 Resources/{FbxPath} 加载 Mesh，" +
                               "请确认 QX.fbx 已放入 Resources 文件夹。");
                enabled = false;
                return;
            }

            Debug.Log($"[PlayerMeshRenderer] FBX 中共找到 {allMeshes.Length} 个 Mesh：" +
                      string.Join(", ", System.Array.ConvertAll(allMeshes, m => m.name)));

            int partCount = PartDefs.Length;
            _meshes     = new Mesh[partCount];
            _materials  = new Material[partCount];
            _propBlocks = new MaterialPropertyBlock[partCount];

            for (int i = 0; i < partCount; i++)
            {
                var (keyword, matPath, texPath) = PartDefs[i];

                // ── 按名称关键字匹配对应 Mesh ─────────────────────────
                foreach (var m in allMeshes)
                {
                    if (m != null && m.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _meshes[i] = m;
                        break;
                    }
                }

                // 若关键字未匹配到，按顺序取
                if (_meshes[i] == null && i < allMeshes.Length)
                    _meshes[i] = allMeshes[i];

                if (_meshes[i] == null)
                {
                    Debug.LogWarning($"[PlayerMeshRenderer] 部位 [{keyword}] 未找到对应 Mesh，跳过。");
                    continue;
                }

                Debug.Log($"[PlayerMeshRenderer] 部位[{keyword}] → Mesh: {_meshes[i].name}");

                // ── 加载材质（直接使用 Resources 中已有的 .mat）────────
                var mat = Resources.Load<Material>(matPath);
                if (mat == null)
                {
                    Debug.LogWarning($"[PlayerMeshRenderer] 材质加载失败: {matPath}");
                    mat = new Material(Shader.Find("Standard"));
                }
                _materials[i] = mat;

                // ── 通过 MaterialPropertyBlock 注入贴图到 Shader ───────
                // 不修改共享材质，完全在 GPU 侧通过 Shader 属性控制贴图
                _propBlocks[i] = new MaterialPropertyBlock();
                var tex = Resources.Load<Texture2D>(texPath);
                if (tex != null)
                {
                    _propBlocks[i].SetTexture("_BaseMap", tex);
                    _propBlocks[i].SetTexture("_MainTex", tex);
                    Debug.Log($"[PlayerMeshRenderer] 部位[{keyword}] 贴图: {tex.name}");
                }
                else
                {
                    Debug.LogWarning($"[PlayerMeshRenderer] 贴图加载失败: {texPath}");
                }
            }
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>是否已成功加载角色 Mesh</summary>
        public bool HasMesh => _meshes != null && _meshes.Length > 0 && _meshes[0] != null;

        /// <summary>
        /// 由 CameraController.SetPlayerEntity 调用，绑定 ECS 玩家实体
        /// </summary>
        public void SetPlayerEntity(Entity entity)
        {
            _playerEntity    = entity;
            _playerTransform = entity?.GetComponent<TransformComponent>();
            _playerMovement  = entity?.GetComponent<MovementComponent>();
        }

        // ── 每帧渲染 ──────────────────────────────────────────────────

        private void Update()
        {
            if (_playerEntity == null || !_playerEntity.IsAlive) return;
            if (_playerTransform == null) return;
            if (_meshes == null || _materials == null) return;

            // ── 计算世界坐标 ──────────────────────────────────────────
            Vector3 worldPos = _playerTransform.Position + new Vector3(0f, _yOffset, 0f);

            // ── 计算朝向（平滑旋转）──────────────────────────────────
            if (_playerMovement != null && _playerMovement.MoveDirection.sqrMagnitude > 0.0001f)
            {
                var d = _playerMovement.MoveDirection;
                float targetYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, targetYaw, RotateSmoothSpeed * Time.deltaTime);
            }

            Quaternion rotation = Quaternion.Euler(_rotationOffsetX, _currentYaw + _rotationOffset, 0f);
            Vector3    scale    = Vector3.one * _modelScale;
            Matrix4x4  matrix   = Matrix4x4.TRS(worldPos, rotation, scale);

            // ── 逐部位绘制（每个部位独立 Mesh + 材质 + 贴图）─────────
            // Graphics.DrawMesh 是纯 GPU 渲染调用，不创建任何 GameObject
            for (int i = 0; i < _meshes.Length; i++)
            {
                if (_meshes[i] == null || _materials[i] == null) continue;
                Graphics.DrawMesh(_meshes[i], matrix, _materials[i], 0, null, 0, _propBlocks[i]);
            }
        }

        private void OnDestroy()
        {
            // 只销毁动态创建的材质（从 Resources 加载的不需要手动销毁）
            if (_materials != null)
            {
                foreach (var mat in _materials)
                {
                    if (mat != null && !mat.name.StartsWith("MI_"))
                        Destroy(mat);
                }
            }
        }
    }
}