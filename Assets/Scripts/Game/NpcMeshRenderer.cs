using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game.UI;

namespace POELike.Game
{
    /// <summary>
    /// NPC Mesh 渲染器（无 GameObject，纯 GPU 渲染）
    /// 挂载在摄像机 GameObject 上，不创建任何额外 GameObject。
    ///
    /// 加载方式：
    ///   从 Resources/Prefabs/{meshName}_Bundle 加载 NpcMeshBundle（ScriptableObject），
    ///   该 Bundle 由编辑器工具「POELike → NPC → 同步 NPC Mesh 到 Resources」生成，
    ///   只包含 Mesh + Material 引用，不含动画/碰撞体等冗余数据，节省内存。
    ///
    /// 每帧通过 Graphics.DrawMesh 渲染所有 NPC，贴图由 Shader 属性控制。
    /// 同时在 OnGUI 中于 NPC 头顶绘制名称标签，并处理点击事件。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NpcMeshRenderer : MonoBehaviour
    {
        // ── ECS 引用 ──────────────────────────────────────────────────
        private World  _world;
        private Camera _camera;

        // ── 对话朝向 ──────────────────────────────────────────────────
        /// <summary>当前正在对话的 NPC 实体（null 表示无对话）</summary>
        private Entity _talkingNpcEntity;
        /// <summary>玩家 TransformComponent 引用（用于计算朝向）</summary>
        private TransformComponent _playerTransform;
        [Header("对话朝向")]
        [Tooltip("NPC 转向玩家的平滑速度（度/秒）")]
        [SerializeField] private float _faceRotateSpeed = 360f;

        // ── 可调参数 ──────────────────────────────────────────────────
        [Header("模型设置")]
        [Tooltip("模型缩放比例")]
        [SerializeField] private float _modelScale     = 1f;
        [Tooltip("模型在 Y 轴上的额外偏移（脚底对齐地面）")]
        [SerializeField] private float _yOffset        = 0f;
        [Tooltip("模型朝向旋转偏移（度），用于修正 FBX 导入朝向（Y轴）")]
        [SerializeField] private float _rotationOffset  = 0f;
        [Tooltip("模型X轴旋转偏移（度），用于修正 FBX 躺倒问题，-90 = 站立")]
        [SerializeField] private float _rotationOffsetX = 0f;

        [Header("名称标签")]
        [Tooltip("标签颜色")]
        [SerializeField] private Color _labelColor    = new Color(1f, 0.9f, 0.5f, 1f);
        [Tooltip("字体大小")]
        [SerializeField] private int   _labelFontSize = 13;
        [Tooltip("标签在头顶的世界空间 Y 偏移（模型高度）")]
        [SerializeField] private float _labelWorldY   = 2.2f;

        // ── Bundle 缓存 ───────────────────────────────────────────────
        // key: meshName（如 "TT_Archer"），value: 已加载的 Bundle 中的部件列表（含本地矩阵）
        private readonly Dictionary<string, List<(Mesh mesh, Material material, Matrix4x4 localMatrix)>> _cache
            = new Dictionary<string, List<(Mesh, Material, Matrix4x4)>>();

        // ── Bundle 资源路径前缀（相对于 Resources/）──────────────────
        private const string BundlePath = "Prefabs/";

        // ── 每帧缓存：(屏幕像素坐标, NPC名称, 是否悬停, NPC世界坐标) ──
        private readonly List<(Vector2 pixel, string name, bool hovered, Vector3 worldPos)> _labels
            = new List<(Vector2, string, bool, Vector3)>();

        // ── 零 GC Query 缓冲 ──────────────────────────────────────────
        private readonly List<Entity> _queryBuffer = new List<Entity>(256);

        // ── GUIStyle（延迟初始化）────────────────────────────────────
        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;

        // ── 悬停检测半径（像素）──────────────────────────────────────
        private const float HoverRadiusPx = 20f;

        [Header("Mesh 点击检测")]
        [Tooltip("NPC 包围盒宽度（X/Z，世界单位）")]
        [SerializeField] private float _hitBoxWidth  = 1.0f;
        [Tooltip("NPC 包围盒高度（Y，世界单位）")]
        [SerializeField] private float _hitBoxHeight = 2.2f;

        // ── 点击回调 ──────────────────────────────────────────────────
        /// <summary>点击 NPC 名称标签或 Mesh 时触发，参数为 NPC 世界坐标</summary>
        public System.Action<Vector3> OnNpcLabelClicked;

        /// <summary>本帧是否消费了点击事件（阻止地面寻路）</summary>
        public bool ClickConsumedThisFrame { get; private set; }

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        public void SetWorld(World world)
        {
            _world = world;
        }

        /// <summary>
        /// 设置玩家 TransformComponent，用于对话时 NPC 朝向玩家
        /// </summary>
        public void SetPlayerTransform(TransformComponent playerTransform)
        {
            _playerTransform = playerTransform;
        }

        /// <summary>
        /// 设置当前对话的 NPC 实体（传 null 表示结束对话）
        /// </summary>
        public void SetTalkingNpc(Entity npcEntity)
        {
            _talkingNpcEntity = npcEntity;
        }

        // ── 每帧渲染 ──────────────────────────────────────────────────

        private void Update()
        {
            ClickConsumedThisFrame = false;
            _labels.Clear();

            if (_world == null || _camera == null) return;

            var mouse      = Mouse.current;
            Vector2 mousePixel = mouse != null ? mouse.position.ReadValue() : Vector2.zero;

            // ── 鼠标左键本帧是否刚按下 ───────────────────────────────
            bool mouseDown = mouse != null && mouse.leftButton.wasPressedThisFrame;

            // ── 从鼠标位置发射射线（用于 Mesh 包围盒点击检测）────────
            Ray ray = _camera.ScreenPointToRay(new Vector3(mousePixel.x, mousePixel.y, 0f));

            _world.Query<NPCComponent, TransformComponent>(_queryBuffer);

            foreach (var entity in _queryBuffer)
            {
                if (!entity.IsAlive) continue;

                var npcComp       = entity.GetComponent<NPCComponent>();
                var transformComp = entity.GetComponent<TransformComponent>();
                if (npcComp == null || transformComp == null) continue;

                // ── 计算头顶屏幕坐标（用于名称标签）──────────────────
                Vector3 npcBase    = transformComp.Position;
                Vector3 labelWorld = new Vector3(npcBase.x, npcBase.y + _labelWorldY, npcBase.z);
                Vector3 screenPos  = _camera.WorldToScreenPoint(labelWorld);

                // NPC 在摄像机背后时跳过
                if (screenPos.z > 0f)
                {
                    Vector2 pixel     = new Vector2(screenPos.x, screenPos.y);
                    bool    isOccluded = IsLabelOccluded(pixel);
                    bool    isHovered = !isOccluded && (mousePixel - pixel).magnitude <= HoverRadiusPx;
                    npcComp.IsHovered = isHovered;
                    _labels.Add((pixel, npcComp.NPCName, isHovered, npcBase));
                }
                else
                {
                    npcComp.IsHovered = false;
                }

                // ── Mesh 包围盒点击检测 ───────────────────────────────
                if (mouseDown && !ClickConsumedThisFrame && !UIGamePanelManager.IsPointerOverAnyPanel(mousePixel))
                {
                    var bounds = new Bounds(
                        new Vector3(npcBase.x, npcBase.y + _hitBoxHeight * 0.5f + _yOffset, npcBase.z),
                        new Vector3(_hitBoxWidth, _hitBoxHeight, _hitBoxWidth));

                    if (bounds.IntersectRay(ray))
                    {
                        ClickConsumedThisFrame = true;
                        OnNpcLabelClicked?.Invoke(npcBase);
                    }
                }

                if (string.IsNullOrEmpty(npcComp.NPCMesh)) continue;

                // ── 获取或加载该 NPC 类型的部件列表 ──────────────────
                var parts = GetOrLoadParts(npcComp.NPCMesh);
                if (parts == null || parts.Count == 0) continue;

                // ── 对话时平滑转向玩家 ────────────────────────────────
                if (_talkingNpcEntity != null && entity == _talkingNpcEntity && _playerTransform != null)
                {
                    Vector3 toPlayer = _playerTransform.Position - npcBase;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude > 0.0001f)
                    {
                        float targetYaw = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
                        npcComp.FaceYaw = Mathf.MoveTowardsAngle(
                            npcComp.FaceYaw, targetYaw, _faceRotateSpeed * Time.deltaTime);
                    }
                }

                // ── 计算世界变换矩阵（根节点）────────────────────────
                Vector3    worldPos = transformComp.Position + new Vector3(0f, _yOffset, 0f);
                Quaternion rotation = Quaternion.Euler(_rotationOffsetX, npcComp.FaceYaw + _rotationOffset, 0f);
                Vector3    scale    = Vector3.one * _modelScale;
                Matrix4x4  rootMatrix = Matrix4x4.TRS(worldPos, rotation, scale);

                // ── 逐部件绘制（纯 GPU 渲染，不创建 GameObject）──────
                // 每个部件的最终矩阵 = 根节点矩阵 × 部件本地矩阵
                foreach (var (mesh, material, localMatrix) in parts)
                {
                    if (mesh == null || material == null) continue;
                    Matrix4x4 partMatrix = rootMatrix * localMatrix;
                    Graphics.DrawMesh(mesh, partMatrix, material, 0);
                }
            }
        }

        // ── 名称标签 OnGUI ────────────────────────────────────────────

        private void OnGUI()
        {
            if (_labels.Count == 0) return;

            // 延迟初始化 GUIStyle（必须在 OnGUI 上下文中）

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                _shadowStyle = new GUIStyle(_labelStyle);
                _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }

            const float labelW = 120f;
            const float labelH = 20f;

            foreach (var (pixel, name, hovered, worldPos) in _labels)
            {
                // Unity GUI 坐标系 Y 从顶部为 0，WorldToScreenPoint Y 从底部为 0，需翻转
                float guiY = Screen.height - pixel.y;

                // ── 透明点击区域（覆盖标签，触发寻路）────────────────
                float labelX = pixel.x - labelW * 0.5f;
                float labelY = guiY - labelH * 0.5f;
                Rect  rect   = new Rect(labelX, labelY, labelW, labelH);

                if (IsGuiRectOccluded(rect))
                    continue;

                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    ClickConsumedThisFrame = true;
                    OnNpcLabelClicked?.Invoke(worldPos);
                }

                if (string.IsNullOrEmpty(name)) continue;

                // ── 字体大小 & 颜色 ───────────────────────────────────
                _labelStyle.fontSize = hovered ? _labelFontSize + 1 : _labelFontSize;
                _labelStyle.normal.textColor  = hovered ? Color.white : _labelColor;
                _shadowStyle.fontSize         = _labelStyle.fontSize;

                // ── 阴影（偏移 1px）──────────────────────────────────
                GUI.Label(new Rect(labelX + 1f, labelY + 1f, labelW, labelH), name, _shadowStyle);

                // ── 正文 ─────────────────────────────────────────────
                GUI.Label(rect, name, _labelStyle);
            }
        }

        private bool IsLabelOccluded(Vector2 pixel)
        {
            const float labelW = 120f;
            const float labelH = 20f;
            Rect labelRect = Rect.MinMaxRect(
                pixel.x - labelW * 0.5f,
                pixel.y - labelH * 0.5f,
                pixel.x + labelW * 0.5f,
                pixel.y + labelH * 0.5f);
            return UIGamePanelManager.IsScreenRectOverAnyPanel(labelRect);
        }

        private static bool IsGuiRectOccluded(Rect guiRect)
        {
            return UIGamePanelManager.IsScreenRectOverAnyPanel(
                UIGamePanelManager.GuiRectToScreenRect(guiRect));
        }

        // ── Bundle 加载 ───────────────────────────────────────────────

        /// <summary>
        /// 获取或加载指定 meshName 对应的 NpcMeshBundle 部件列表。
        /// Bundle 路径：Resources/Prefabs/{meshName}_Bundle
        /// </summary>
        private List<(Mesh, Material, Matrix4x4)> GetOrLoadParts(string meshName)
        {
            if (_cache.TryGetValue(meshName, out var cached))
                return cached;

            var parts = new List<(Mesh, Material, Matrix4x4)>();

            // 从 Resources 加载 NpcMeshBundle
            string bundleKey = BundlePath + meshName + "_Bundle";
            var bundle = Resources.Load<NpcMeshBundle>(bundleKey);

            if (bundle == null)
            {
                Debug.LogWarning($"[NpcMeshRenderer] 找不到 NpcMeshBundle: Resources/{bundleKey}\n" +
                                 "请在编辑器中执行「POELike → NPC → 同步 NPC Mesh 到 Resources」。");
                _cache[meshName] = parts; // 缓存空列表，避免重复报错
                return parts;
            }

            // 从 Bundle 中提取部件（含本地矩阵）
            if (bundle.Parts != null)
            {
                foreach (var part in bundle.Parts)
                {
                    if (part.Mesh != null && part.Material != null)
                        parts.Add((part.Mesh, part.Material, part.LocalMatrix));
                }
            }

            Debug.Log($"[NpcMeshRenderer] 加载 Bundle [{meshName}]，共 {parts.Count} 个部件。");
            _cache[meshName] = parts;
            return parts;
        }

        private void OnDestroy()
        {
            _cache.Clear();
            _labels.Clear();
        }
    }
}