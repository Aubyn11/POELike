using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Managers;
using POELike.Game.UI;

namespace POELike.Game
{
    /// <summary>
    /// 技能运行时范围显示器。
    /// 挂载在主摄像机上，以轻量方式显示技能实体的作用范围。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SkillRuntimeRenderer : MonoBehaviour
    {
        private readonly List<Entity> _skillEntities = new List<Entity>(128);

        private Material _material;
        private Camera _camera;

        private static readonly int PropColor = Shader.PropertyToID("_Color");
        private static readonly int PropCenter = Shader.PropertyToID("_CenterScreenPos");
        private static readonly int PropRadius = Shader.PropertyToID("_RadiusPx");
        private static readonly int PropThickness = Shader.PropertyToID("_ThicknessPx");
        private static readonly int PropScreenSize = Shader.PropertyToID("_SkillScreenSize");
        private static readonly int PropFillAlpha = Shader.PropertyToID("_FillAlpha");

        [SerializeField] private float _thicknessPx = 3f;
        [SerializeField] private float _minPixelRadius = 24f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            var shader = Resources.Load<Shader>("SkillRuntimeOverlay");
            if (shader == null)
                shader = Shader.Find("POELike/SkillRuntimeOverlay");

            if (shader == null)
            {
                Debug.LogError("[SkillRuntimeRenderer] 找不到 Shader 'SkillRuntimeOverlay'");
                enabled = false;
                return;
            }

            _material = new Material(shader)
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
            if (_material != null)
                Destroy(_material);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != _camera)
                return;

            DrawAll();
        }

        private void OnPostRender()
        {
            DrawAll();
        }

        private void DrawAll()
        {
            if (_material == null || GameManager.Instance?.World == null)
                return;

            GameManager.Instance.World.Query<SkillRuntimeComponent>(_skillEntities);
            if (_skillEntities.Count == 0)
                return;

            for (int i = 0; i < _skillEntities.Count; i++)
            {
                var entity = _skillEntities[i];
                var runtime = entity.GetComponent<SkillRuntimeComponent>();
                var transform = entity.GetComponent<TransformComponent>();
                if (runtime == null || transform == null || !runtime.IsEnabled)
                    continue;

                DrawRuntime(runtime, transform);
            }
        }

        private void DrawRuntime(SkillRuntimeComponent runtime, TransformComponent transform)
        {
            Vector3 screenPos = _camera.WorldToViewportPoint(transform.Position);
            if (screenPos.z < 0f)
                return;

            float radiusPx = Mathf.Max(_minPixelRadius, EstimatePixelRadius(transform.Position, runtime.AreaRadius));
            Rect screenRect = BuildScreenRect(screenPos, radiusPx);
            if (UIGamePanelManager.IsScreenRectOverAnyPanel(screenRect))
                return;

            float normalizedLife = runtime.TotalLifetime > 0.001f
                ? Mathf.Clamp01(runtime.RemainingTime / runtime.TotalLifetime)
                : 0f;

            Color color = runtime.DisplayColor;
            color.a = Mathf.Clamp01(color.a);

            _material.SetColor(PropColor, color);
            _material.SetVector(PropCenter, new Vector4(screenPos.x, screenPos.y, 0f, 0f));
            _material.SetFloat(PropRadius, radiusPx);
            _material.SetFloat(PropThickness, _thicknessPx);
            _material.SetVector(PropScreenSize, new Vector4(Screen.width, Screen.height, 0f, 0f));
            _material.SetFloat(PropFillAlpha, Mathf.Lerp(0.08f, 0.22f, normalizedLife));

            GL.PushMatrix();
            GL.LoadOrtho();
            _material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Vertex3(0f, 0f, 0f);
            GL.Vertex3(1f, 0f, 0f);
            GL.Vertex3(1f, 1f, 0f);
            GL.Vertex3(0f, 1f, 0f);
            GL.End();
            GL.PopMatrix();
        }

        private float EstimatePixelRadius(Vector3 worldCenter, float worldRadius)
        {
            Vector3 edgeWorld = worldCenter + Vector3.right * Mathf.Max(0.1f, worldRadius);
            Vector3 centerScreen = _camera.WorldToScreenPoint(worldCenter);
            Vector3 edgeScreen = _camera.WorldToScreenPoint(edgeWorld);
            return Mathf.Abs(edgeScreen.x - centerScreen.x);
        }

        private static Rect BuildScreenRect(Vector3 viewportPos, float radiusPx)
        {
            float centerX = viewportPos.x * Screen.width;
            float centerY = viewportPos.y * Screen.height;
            float extent = radiusPx + 8f;
            return Rect.MinMaxRect(centerX - extent, centerY - extent, centerX + extent, centerY + extent);
        }
    }
}
