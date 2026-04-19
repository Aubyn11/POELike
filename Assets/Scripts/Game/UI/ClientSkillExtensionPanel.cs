using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.ECS.Systems;
using POELike.Game.Skills;

namespace POELike.Game.UI
{
    /// <summary>
    /// 客户端技能拓展面板。
    /// 使用 IMGUI 提供临时技能 Prefab 的点击入口。
    /// </summary>
    public class ClientSkillExtensionPanel : MonoBehaviour
    {
        private static ClientSkillExtensionPanel _activeInstance;

        private readonly Rect[] _buttonRects = new Rect[4];
        private Rect _windowRect = new Rect(580f, 10f, 260f, 240f);
        private bool _isVisible = true;
        private string _status = "点击按钮生成临时技能 Prefab";

        private InputAction _toggleAction;
        private int _lastToggleFrame = -1;

        private World _world;
        private Entity _playerEntity;
        private SkillSystem _skillSystem;

        private struct RuntimeSkillPrefabDefinition
        {
            public string PrefabKey;
            public string DisplayName;
            public string Description;
            public SkillData Skill;
        }

        private RuntimeSkillPrefabDefinition[] _definitions;

        public static bool TryGetVisibleScreenRect(out Rect screenRect)
        {
            screenRect = default;
            if (_activeInstance == null || !_activeInstance._isVisible)
                return false;

            screenRect = UIGamePanelManager.GuiRectToScreenRect(_activeInstance._windowRect);
            return true;
        }

        private void Awake()
        {
            _activeInstance = this;
            _toggleAction = new InputAction("ClientSkillExtensionToggle", InputActionType.Button, "<Keyboard>/f2");
            _toggleAction.performed += OnTogglePerformed;
            _toggleAction.Enable();
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;

            if (_toggleAction != null)
            {
                _toggleAction.performed -= OnTogglePerformed;
                _toggleAction.Dispose();
            }
        }

        public void Init(World world, Entity playerEntity)
        {
            _world = world;
            _playerEntity = playerEntity;
            _skillSystem = _world?.GetSystem<SkillSystem>();
            BuildDefinitions();
        }

        private void Update()
        {
            if (_toggleAction != null && _toggleAction.WasPressedThisFrame())
                ToggleVisibility();
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleVisibility();
        }

        private void ToggleVisibility()
        {
            if (_lastToggleFrame == Time.frameCount)
                return;

            _lastToggleFrame = Time.frameCount;
            _isVisible = !_isVisible;
        }

        private void BuildDefinitions()
        {
            _definitions = new[]
            {
                new RuntimeSkillPrefabDefinition
                {
                    PrefabKey = "FireballTempPrefab",
                    DisplayName = "火球临时Prefab",
                    Description = "点击后发射火球，命中范围交给 GPU 判定",
                    Skill = SkillFactory.CreateFireball().WithSupportGem(SkillFactory.CreateAddedFireDamageGem(18f))
                },
                new RuntimeSkillPrefabDefinition
                {
                    PrefabKey = "NovaTempPrefab",
                    DisplayName = "冰环临时Prefab",
                    Description = "点击后在目标点生成范围新星",
                    Skill = SkillFactory.CreateFrostNova()
                },
                new RuntimeSkillPrefabDefinition
                {
                    PrefabKey = "CycloneTempPrefab",
                    DisplayName = "旋风临时Prefab",
                    Description = "点击后在角色脚下生成持续范围技能",
                    Skill = SkillFactory.CreateCyclone()
                },
                new RuntimeSkillPrefabDefinition
                {
                    PrefabKey = "BlinkTempPrefab",
                    DisplayName = "闪现临时Prefab",
                    Description = "点击后瞬移到鼠标位置",
                    Skill = SkillFactory.CreateBlink()
                }
            };
        }

        private void OnGUI()
        {
            var currentEvent = Event.current;
            if (currentEvent != null && currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.F2)
            {
                ToggleVisibility();
                currentEvent.Use();
            }

            if (!_isVisible)
                return;

            _windowRect = GUI.Window(10001, _windowRect, DrawWindow, "客户端技能拓展 [F2]");
        }

        private void DrawWindow(int id)
        {
            if (_definitions == null || _definitions.Length == 0)
                BuildDefinitions();

            GUILayout.Space(6f);
            GUILayout.Label("临时技能 Prefab：");

            for (int i = 0; i < _definitions.Length; i++)
            {
                var definition = _definitions[i];
                GUILayout.BeginVertical("box");
                GUILayout.Label(definition.DisplayName);
                GUILayout.Label(definition.Description, GUILayout.Width(220f));

                if (GUILayout.Button("点击生成 / 释放", GUILayout.Height(28f)))
                    TriggerDefinition(definition);

                if (Event.current.type == EventType.Repaint)
                    _buttonRects[i] = GUILayoutUtility.GetLastRect();

                GUILayout.EndVertical();
            }

            GUILayout.Space(6f);
            GUILayout.Label(_status);
            GUI.DragWindow(new Rect(0f, 0f, _windowRect.width, 20f));
        }

        private void TriggerDefinition(RuntimeSkillPrefabDefinition definition)
        {
            if (_playerEntity == null || !_playerEntity.IsAlive)
            {
                _status = "❌ 玩家实体不可用";
                return;
            }

            if (_skillSystem == null)
            {
                _skillSystem = _world?.GetSystem<SkillSystem>();
                if (_skillSystem == null)
                {
                    _status = "❌ SkillSystem 不可用";
                    return;
                }
            }

            var input = _playerEntity.GetComponent<PlayerInputComponent>();
            var transform = _playerEntity.GetComponent<TransformComponent>();
            Vector3 targetPos = input != null && input.MouseWorldPosition != Vector3.zero
                ? input.MouseWorldPosition
                : (transform != null ? transform.Position + transform.Forward * Mathf.Max(1f, definition.Skill.Range) : Vector3.zero);

            _skillSystem.ExecuteSkillAtTarget(_playerEntity, definition.Skill, targetPos, definition.PrefabKey);
            _status = $"✅ 已触发 {definition.DisplayName}";
        }
    }
}
