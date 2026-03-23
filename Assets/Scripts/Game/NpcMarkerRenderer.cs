using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.Game
{
    /// <summary>
    /// NPC位置标记渲染器
    /// 挂载在摄像机 GameObject 上，使用 GL 在屏幕上绘制所有NPC的实心圆标记。
    /// 鼠标悬停NPC时显示白色描边高亮，并更新 NPCComponent.IsHovered。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class NpcMarkerRenderer : MonoBehaviour
    {
        // ── ECS 引用 ──────────────────────────────────────────────────
        private World _world;

        // ── 渲染资源 ──────────────────────────────────────────────────
        private Material _npcMaterial;
        private Camera   _camera;

        // ── 可调参数 ──────────────────────────────────────────────────
        [Header("NPC标记外观")]
        [SerializeField] private Color _npcColor     = new Color(0.9f, 0.7f, 0.2f, 1.0f);
        [SerializeField] private Color _outlineColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [SerializeField] private float _radius       = 14f;
        [SerializeField] private float _outlineWidth = 3f;

        // ── 悬停检测半径（像素）──────────────────────────────────────
        private const float HoverRadiusPx = 20f;

        // ── 名称标签样式 ──────────────────────────────────────────────
        [Header("NPC名称标签")]
        [SerializeField] private Color  _labelColor    = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private int    _labelFontSize = 13;
        [SerializeField] private float  _labelOffsetY  = 22f;  // 圆点上方偏移像素

        private GUIStyle _labelStyle;

        // ── 圆形纹理（对话框打开时用OnGUI替代GL绘制）────────────────
        private Texture2D _circleTexture;

        // 每帧缓存：(屏幕像素坐标, 名称, 是否悬停, NPC世界坐标)
        private readonly List<(Vector2 pixel, string name, bool hovered, Vector3 worldPos)> _npcLabels
            = new List<(Vector2, string, bool, Vector3)>();

        // ── 点击NPC名称回调 ───────────────────────────────────────────
        /// <summary>
        /// 当玩家点击NPC名称标签时触发，参数为NPC世界坐标
        /// </summary>
        public System.Action<Vector3> OnNpcLabelClicked;

        /// <summary>
        /// 本帧是否消费了点击事件（点击了NPC名称），用于阻止地面寻路
        /// </summary>
        public bool ClickConsumedThisFrame { get; private set; }

        /// <summary>
        /// 对话框是否打开（打开时跳过NPC标记渲染，避免覆盖在对话框上方）
        /// </summary>
        public bool IsDialogOpen { get; set; }

        // ── Shader 属性 ID ────────────────────────────────────────────
        private static readonly int PropColor       = Shader.PropertyToID("_Color");
        private static readonly int PropOutline     = Shader.PropertyToID("_OutlineColor");
        private static readonly int PropRadius      = Shader.PropertyToID("_Radius");
        private static readonly int PropOutlineW    = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PropHovered     = Shader.PropertyToID("_Hovered");
        private static readonly int PropNpcPos      = Shader.PropertyToID("_NpcScreenPos");
        private static readonly int PropScreenSize  = Shader.PropertyToID("_NpcScreenSize");

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _labelStyle = null; // 延迟到 OnGUI 中初始化（需要 GUI 上下文）

            var shader = Resources.Load<Shader>("NpcMarker");
            if (shader == null)
                shader = Shader.Find("POELike/NpcMarker");

            if (shader == null)
            {
                Debug.LogError("[NpcMarkerRenderer] 找不到 NpcMarker Shader！请确认 Assets/Resources/NpcMarker.shader 存在");
                enabled = false;
                return;
            }

            _npcMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnDestroy()
        {
            if (_npcMaterial != null)
                Destroy(_npcMaterial);
            if (_circleTexture != null)
                Destroy(_circleTexture);
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        public void SetWorld(World world)
        {
            _world = world;
        }

        // ── 渲染回调 ──────────────────────────────────────────────────

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera) return;
            DrawNpcMarkers();
        }

        private void OnPostRender()
        {
            // 内置管线备用
            DrawNpcMarkers();
        }

        private void OnGUI()
        {
            if (_npcLabels.Count == 0) return;

            // 延迟初始化 GUIStyle（必须在 OnGUI 上下文中创建）
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                };
                _labelStyle.normal.textColor  = _labelColor;
                _labelStyle.hover.textColor   = _labelColor;
            }
            _labelStyle.fontSize = _labelFontSize;

            // 延迟初始化圆形纹理
            if (_circleTexture == null)
                _circleTexture = MakeCircleTexture(Mathf.RoundToInt(_radius * 2 + _outlineWidth * 2 + 4));

            foreach (var (pixel, name, hovered, worldPos) in _npcLabels)
            {
                // Unity GUI 坐标系：Y 从屏幕顶部为 0，需要翻转
                float guiY = Screen.height - pixel.y;

                // ── 对话框打开时：用OnGUI绘制圆形替代GL（GL比OnGUI晚执行会覆盖对话框）──
                if (IsDialogOpen)
                {
                    float texSize = _radius * 2f + _outlineWidth * 2f + 4f;
                    Rect circleRect = new Rect(
                        pixel.x - texSize * 0.5f,
                        guiY    - texSize * 0.5f,
                        texSize, texSize);
                    Color tint = hovered ? Color.Lerp(_npcColor, Color.white, 0.25f) : _npcColor;
                    GUI.color = tint;
                    GUI.DrawTexture(circleRect, _circleTexture);
                    GUI.color = Color.white;
                }

                // ── 圆形区域点击检测（透明Button覆盖圆形）────────────────
                {
                    float btnSize = (_radius + _outlineWidth) * 2f;
                    Rect circleClickRect = new Rect(
                        pixel.x - btnSize * 0.5f,
                        guiY    - btnSize * 0.5f,
                        btnSize, btnSize);
                    GUIStyle transparentBtn = new GUIStyle(GUIStyle.none);
                    if (GUI.Button(circleClickRect, GUIContent.none, transparentBtn))
                    {
                        ClickConsumedThisFrame = true;
                        OnNpcLabelClicked?.Invoke(worldPos);
                    }
                }

                if (string.IsNullOrEmpty(name)) continue;

                // 标签宽高
                const float labelW = 120f;
                const float labelH = 20f;

                // 圆点上方偏移（GUI Y 轴向下为正，所以减去偏移）
                float labelX = pixel.x - labelW * 0.5f;
                float labelY = guiY - _radius - _labelOffsetY - labelH;

                Rect rect = new Rect(labelX, labelY, labelW, labelH);

                // 悬停时文字变亮
                if (hovered)
                {
                    _labelStyle.normal.textColor = Color.white;
                    _labelStyle.fontSize = _labelFontSize + 1;
                }
                else
                {
                    _labelStyle.normal.textColor = _labelColor;
                    _labelStyle.fontSize = _labelFontSize;
                }

                // 绘制阴影（偏移1像素，黑色）
                GUIStyle shadowStyle = new GUIStyle(_labelStyle);
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                GUI.Label(new Rect(labelX + 1, labelY + 1, labelW, labelH), name, shadowStyle);

                // 使用透明Button检测点击，同时绘制文字
                GUIStyle transparentLabelBtn = new GUIStyle(GUIStyle.none);
                if (GUI.Button(rect, GUIContent.none, transparentLabelBtn))
                {
                    // 点击了NPC名称标签，触发寻路
                    ClickConsumedThisFrame = true;
                    OnNpcLabelClicked?.Invoke(worldPos);
                }

                // 绘制正文
                GUI.Label(rect, name, _labelStyle);
            }
        }

        /// <summary>
        /// 生成一张圆形纹理（用于对话框打开时替代GL绘制）
        /// </summary>
        private static Texture2D MakeCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            float center = size * 0.5f;
            float r = center - 1f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float alpha = 1f - Mathf.Clamp01((dist - (r - 1.5f)) / 1.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ── 绘制逻辑 ──────────────────────────────────────────────────

        private void Update()
        {
            // 每帧重置点击消费标志
            ClickConsumedThisFrame = false;
        }

        private void DrawNpcMarkers()
        {
            if (_npcMaterial == null || _world == null) return;
            // 对话框打开时跳过GL圆形标记绘制（GL在endCameraRendering中执行，比OnGUI更晚，会覆盖在对话框上方）
            if (IsDialogOpen) return;

            // 清空上一帧的标签缓存
            _npcLabels.Clear();

            // 获取鼠标屏幕坐标（像素）
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 mousePixel = mouse.position.ReadValue();

            // 查询所有NPC实体
            var npcEntities = _world.Query<NPCComponent, TransformComponent>();

            foreach (var entity in npcEntities)
            {
                if (!entity.IsAlive) continue;

                var npcComp  = entity.GetComponent<NPCComponent>();
                var transform = entity.GetComponent<TransformComponent>();
                if (npcComp == null || transform == null) continue;

                // 世界坐标 → 视口坐标
                // Y 轴固定为 0（地面平面），避免斜视角摄像机移动时圆点上下漂移
                Vector3 worldPos  = new Vector3(transform.Position.x, 0f, transform.Position.z);
                Vector3 screenPos = _camera.WorldToViewportPoint(worldPos);

                // NPC在摄像机背后时跳过
                if (screenPos.z < 0f) continue;

                // 视口坐标 → 像素坐标（用于悬停检测）
                // 注意：鼠标坐标 Y 轴从底部为 0（与 WorldToViewportPoint 一致），无需翻转
                Vector2 npcPixel = new Vector2(screenPos.x * Screen.width, screenPos.y * Screen.height);

                // 悬停检测
                bool isHovered = (mousePixel - npcPixel).magnitude <= HoverRadiusPx;
                npcComp.IsHovered = isHovered;

                // 缓存标签信息（OnGUI 中绘制文字）
                _npcLabels.Add((npcPixel, npcComp.NPCName, isHovered, worldPos));

                // D3D11 平台 Shader UV 的 Y 轴从顶部为 0，而 WorldToViewportPoint 的 Y 从底部为 0
                // Shader 内部已对 UV.y 做了 UNITY_UV_STARTS_AT_TOP 翻转，
                // 因此传入的 _NpcScreenPos.y 也需要翻转，保持一致
                float shaderY = SystemInfo.graphicsUVStartsAtTop ? 1.0f - screenPos.y : screenPos.y;

                // 更新 Shader 参数
                _npcMaterial.SetColor(PropColor,      _npcColor);
                _npcMaterial.SetColor(PropOutline,    _outlineColor);
                _npcMaterial.SetFloat(PropRadius,     _radius);
                _npcMaterial.SetFloat(PropOutlineW,   _outlineWidth);
                _npcMaterial.SetFloat(PropHovered,    isHovered ? 1f : 0f);
                _npcMaterial.SetVector(PropNpcPos,    new Vector4(screenPos.x, shaderY, 0f, 0f));
                _npcMaterial.SetVector(PropScreenSize, new Vector4(Screen.width, Screen.height, 0f, 0f));

                // GL 绘制全屏 Quad（Shader 内部按距离裁剪，只在NPC位置附近显示圆形）
                GL.PushMatrix();
                GL.LoadOrtho();

                _npcMaterial.SetPass(0);

                GL.Begin(GL.QUADS);
                GL.Vertex3(0f, 0f, 0f);
                GL.Vertex3(1f, 0f, 0f);
                GL.Vertex3(1f, 1f, 0f);
                GL.Vertex3(0f, 1f, 0f);
                GL.End();

                GL.PopMatrix();
            }
        }
    }
}
