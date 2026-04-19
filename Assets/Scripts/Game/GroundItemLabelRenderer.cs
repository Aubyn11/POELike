using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game.UI;
using POELike.Managers;

namespace POELike.Game
{
    /// <summary>
    /// 地面掉落名称渲染器。
    /// 挂载在摄像机上，监听掉落事件并在地面掉落位置绘制装备名称标签。
    /// 标签风格对齐 NPC 名称，额外支持背景底板与鼠标移入高亮。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class GroundItemLabelRenderer : MonoBehaviour
    {
        private struct GroundItemLabel
        {
            public Vector3 Position;
            public string Name;
            public ItemRarity Rarity;
            public int StackIndex;
            public ItemData Item;

            public GroundItemLabel(Vector3 position, ItemData item, int stackIndex)
            {
                Position = position;
                Name = item?.Name ?? string.Empty;
                Rarity = item?.Rarity ?? ItemRarity.Normal;
                StackIndex = stackIndex;
                Item = item;
            }
        }

        private const float SameSpotThreshold = 0.75f;
        private const float MinLabelWidth = 72f;
        private const float MinLabelHeight = 22f;
        private const float BorderThickness = 1f;

        private readonly List<GroundItemLabel> _labels = new List<GroundItemLabel>(64);

        private World _world;
        private Camera _camera;
        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;
        private GUIStyle _pickupHintStyle;
        private string _pickupHintMessage;
        private float _pickupHintExpireTime;

        public bool ClickConsumedThisFrame { get; private set; }

        [Header("地面掉落名称")]
        [SerializeField] private int _labelFontSize = 13;
        [SerializeField] private float _labelWorldY = 0.45f;
        [SerializeField] private float _stackOffsetY = 24f;
        [SerializeField] private float _horizontalPadding = 12f;
        [SerializeField] private float _verticalPadding = 5f;
        [SerializeField] private int _maxVisibleLabels = 64;
        [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.72f);
        [SerializeField] private Color _backgroundHighlightColor = new Color(0.82f, 0.64f, 0.18f, 0.90f);
        [SerializeField] private Color _borderColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _borderHighlightColor = new Color(1f, 0.93f, 0.68f, 0.95f);
        [SerializeField] private float _pickupHintDuration = 1.5f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void SetWorld(World world)
        {
            if (_world == world)
                return;

            if (_world != null)
                _world.EventBus.Unsubscribe<GroundItemDroppedEvent>(OnGroundItemDropped);

            _world = world;

            if (_world != null)
                _world.EventBus.Subscribe<GroundItemDroppedEvent>(OnGroundItemDropped);
        }

        private void OnGroundItemDropped(GroundItemDroppedEvent evt)
        {
            if (evt.Item == null || string.IsNullOrWhiteSpace(evt.Item.Name))
                return;

            int stackIndex = ResolveStackIndex(evt.Position);
            if (_labels.Count >= _maxVisibleLabels)
                _labels.RemoveAt(0);

            _labels.Add(new GroundItemLabel(evt.Position, evt.Item, stackIndex));
        }

        private int ResolveStackIndex(Vector3 dropPosition)
        {
            int stackIndex = 0;
            float thresholdSq = SameSpotThreshold * SameSpotThreshold;

            foreach (var label in _labels)
            {
                var delta = new Vector2(label.Position.x - dropPosition.x, label.Position.z - dropPosition.z);
                if (delta.sqrMagnitude <= thresholdSq)
                    stackIndex = Mathf.Max(stackIndex, label.StackIndex + 1);
            }

            return stackIndex;
        }

        private void OnGUI()
        {
            ClickConsumedThisFrame = false;

            EnsureStyles();

            if (_camera != null && _labels.Count > 0)
            {
                var mouse = Mouse.current;
                Vector2 mousePixel = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
                Vector2 mouseGui = new Vector2(mousePixel.x, Screen.height - mousePixel.y);
                bool mouseDown = mouse != null && mouse.leftButton.wasPressedThisFrame;
                var content = new GUIContent();

                for (int i = _labels.Count - 1; i >= 0; i--)
                {
                    var label = _labels[i];
                    Vector3 worldPos = label.Position + new Vector3(0f, _labelWorldY, 0f);
                    Vector3 screenPos = _camera.WorldToScreenPoint(worldPos);
                    if (screenPos.z <= 0f)
                        continue;

                    float guiY = Screen.height - screenPos.y - label.StackIndex * _stackOffsetY;

                    content.text = label.Name;
                    Vector2 textSize = _labelStyle.CalcSize(content);
                    float labelW = Mathf.Max(MinLabelWidth, textSize.x + _horizontalPadding * 2f);
                    float labelH = Mathf.Max(MinLabelHeight, textSize.y + _verticalPadding * 2f);
                    float labelX = screenPos.x - labelW * 0.5f;
                    float labelY = guiY - labelH * 0.5f;
                    Rect rect = new Rect(labelX, labelY, labelW, labelH);

                    if (IsGuiRectOccluded(rect))
                        continue;

                    bool hovered = rect.Contains(mouseGui);
                    if (mouseDown && hovered && !UIGamePanelManager.IsPointerOverAnyPanel(mousePixel))
                    {
                        ClickConsumedThisFrame = true;
                        TryPickupLabel(i);
                        continue;
                    }

                    DrawLabelBackground(rect, hovered);
                    DrawLabelText(rect, label.Name, label.Rarity, hovered);
                }
            }

            DrawPickupHint();
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null)
                return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = _labelFontSize,
                clipping = TextClipping.Clip,
            };

            _shadowStyle = new GUIStyle(_labelStyle);
            _shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);

            _pickupHintStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(18, 18, 10, 10),
            };
            _pickupHintStyle.normal.textColor = Color.white;
        }

        private void TryPickupLabel(int labelIndex)
        {
            if (labelIndex < 0 || labelIndex >= _labels.Count)
                return;

            var label = _labels[labelIndex];
            if (label.Item == null)
                return;

            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                ShowPickupHint("背包不可用");
                return;
            }

            var bagPanel = uiManager.GetOrCreateBagPanel(false);
            if (bagPanel == null)
            {
                ShowPickupHint("背包不可用");
                return;
            }

            var bagItem = CreateBagItemData(label.Item);
            if (bagItem == null)
            {
                ShowPickupHint("掉落数据异常");
                return;
            }

            if (!bagPanel.CanAddItemToBag(bagItem, out var capacityFailureReason))
            {
                ShowPickupHint(string.IsNullOrWhiteSpace(capacityFailureReason) ? "背包放不下了" : "背包放不下了");
                return;
            }

            if (!bagPanel.TryAddItemToBag(bagItem, out var failureReason))
            {
                ShowPickupHint(string.IsNullOrWhiteSpace(failureReason) ? "拾取失败" : failureReason);
                return;
            }

            _labels.RemoveAt(labelIndex);
        }

        private static BagItemData CreateBagItemData(ItemData item)
        {
            if (item == null)
                return null;

            var bagItem = new BagItemData(item.Id, item.Name, ResolveGridWidth(item.Type), ResolveGridHeight(item.Type))
            {
                ItemKind = item.Type == ItemType.Flask ? BagItemKind.Flask : BagItemKind.Equipment,
                ItemColor = ResolveRarityColor(item.Rarity),
                RuntimeItemData = item,
            };

            switch (item.Type)
            {
                case ItemType.Weapon:
                    bagItem.AcceptedEquipmentSlot = EquipmentSlot.MainHand;
                    bagItem.SetAcceptedEquipmentSlots(new[] { EquipmentSlot.MainHand, EquipmentSlot.OffHand });
                    break;
                case ItemType.Armour:
                    bagItem.AcceptedEquipmentSlot = EquipmentSlot.BodyArmour;
                    bagItem.SetAcceptedEquipmentSlots(new[] { EquipmentSlot.BodyArmour, EquipmentSlot.Helmet, EquipmentSlot.Gloves, EquipmentSlot.Boots });
                    break;
                case ItemType.Accessory:
                    bagItem.AcceptedEquipmentSlot = EquipmentSlot.Amulet;
                    bagItem.SetAcceptedEquipmentSlots(new[] { EquipmentSlot.RingLeft, EquipmentSlot.RingRight, EquipmentSlot.Amulet, EquipmentSlot.Belt });
                    break;
            }

            foreach (var modifier in item.Prefixes)
            {
                bagItem.PrefixDescriptions.Add(FormatModifierDescription(modifier));
            }

            foreach (var modifier in item.Suffixes)
            {
                bagItem.SuffixDescriptions.Add(FormatModifierDescription(modifier));
            }

            return bagItem;
        }

        private static int ResolveGridWidth(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Weapon => 1,
                ItemType.Armour => 2,
                ItemType.Accessory => 1,
                _ => 1,
            };
        }

        private static int ResolveGridHeight(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.Weapon => 3,
                ItemType.Armour => 3,
                ItemType.Accessory => 1,
                _ => 1,
            };
        }

        private static string FormatModifierDescription(StatModifier modifier)
        {
            return $"{modifier.StatType} {modifier.Value:+0.##;-0.##;0.##}";
        }

        private void ShowPickupHint(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            _pickupHintMessage = message;
            _pickupHintExpireTime = Time.unscaledTime + Mathf.Max(0.1f, _pickupHintDuration);
        }

        private void DrawPickupHint()
        {
            if (string.IsNullOrWhiteSpace(_pickupHintMessage) || Time.unscaledTime > _pickupHintExpireTime)
                return;

            const float hintWidth = 220f;
            const float hintHeight = 44f;
            Rect hintRect = new Rect(
                Screen.width * 0.5f - hintWidth * 0.5f,
                Screen.height - 180f,
                hintWidth,
                hintHeight);

            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.Box(hintRect, GUIContent.none);
            GUI.color = previousColor;
            GUI.Label(hintRect, _pickupHintMessage, _pickupHintStyle);
        }

        private void DrawLabelBackground(Rect rect, bool hovered)
        {
            Color previousColor = GUI.color;
            Color backgroundColor = hovered ? _backgroundHighlightColor : _backgroundColor;
            Color borderColor = hovered ? _borderHighlightColor : _borderColor;

            GUI.color = backgroundColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            DrawBorder(new Rect(rect.x, rect.y, rect.width, BorderThickness), borderColor);
            DrawBorder(new Rect(rect.x, rect.yMax - BorderThickness, rect.width, BorderThickness), borderColor);
            DrawBorder(new Rect(rect.x, rect.y, BorderThickness, rect.height), borderColor);
            DrawBorder(new Rect(rect.xMax - BorderThickness, rect.y, BorderThickness, rect.height), borderColor);
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void DrawLabelText(Rect rect, string name, ItemRarity rarity, bool hovered)
        {
            _labelStyle.fontSize = _labelFontSize;
            _shadowStyle.fontSize = _labelFontSize;
            _labelStyle.normal.textColor = hovered ? Color.white : ResolveRarityColor(rarity);

            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), name, _shadowStyle);
            GUI.Label(rect, name, _labelStyle);
        }

        private static Color ResolveRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Magic => new Color(0.4f, 0.6f, 1f),
                ItemRarity.Rare => new Color(1f, 0.8f, 0.2f),
                ItemRarity.Unique => new Color(1f, 0.52f, 0.10f),
                _ => Color.white,
            };
        }

        private static bool IsGuiRectOccluded(Rect guiRect)
        {
            return UIGamePanelManager.IsScreenRectOverAnyPanel(
                UIGamePanelManager.GuiRectToScreenRect(guiRect));
        }

        private void OnDestroy()
        {
            if (_world != null)
                _world.EventBus.Unsubscribe<GroundItemDroppedEvent>(OnGroundItemDropped);

            _labels.Clear();
        }
    }

    public struct GroundItemDroppedEvent
    {
        public ItemData Item;
        public Vector3 Position;
    }
}
