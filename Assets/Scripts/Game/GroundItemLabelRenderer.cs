using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using POELike.ECS.Core;
using POELike.ECS.Components;
using POELike.Game.Currency;
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
            public Vector2 LayoutOffset;
            public bool HasLayoutOffset;
            public bool WasLayoutBlocked;

            public GroundItemLabel(Vector3 position, ItemData item, int stackIndex)
            {
                Position = position;
                Name = BuildLabelDisplayName(item);
                Rarity = item?.Rarity ?? ItemRarity.Normal;
                StackIndex = stackIndex;
                Item = item;
                LayoutOffset = Vector2.zero;
                HasLayoutOffset = false;
                WasLayoutBlocked = false;
            }
        }

        private const float SameSpotThreshold = 0.75f;
        private const float MinLabelWidth = 72f;
        private const float MinLabelHeight = 22f;
        private const float BorderThickness = 1f;
        private const float MinVisibleSegmentWidth = 6f;

        private readonly List<GroundItemLabel> _labels = new List<GroundItemLabel>(64);
        private readonly List<Rect> _occupiedLabelRects = new List<Rect>(64);
        private readonly List<Rect> _screenOccluderRects = new List<Rect>(16);
        private readonly List<Rect> _visibleLabelSegments = new List<Rect>(8);
        private readonly List<Rect> _visibleLabelSegmentBuffer = new List<Rect>(8);

        private World _world;

        private Camera _camera;
        private GUIStyle _labelStyle;
        private GUIStyle _shadowStyle;
        private GUIStyle _pickupHintStyle;
        private string _pickupHintMessage;
        private float _pickupHintExpireTime;
        private bool _layoutDirty = true;

        public bool ClickConsumedThisFrame { get; private set; }
        public Action<ItemData, Vector3> OnGroundItemLabelClicked;

        [Header("地面掉落名称")]
        [SerializeField] private int _labelFontSize = 13;
        [SerializeField] private float _labelWorldY = 0.45f;
        [SerializeField] private float _stackOffsetY = 24f;
        [SerializeField] private float _labelLayoutHorizontalGap = 10f;
        [SerializeField] private float _labelLayoutVerticalGap = 6f;
        [SerializeField] private int _maxLabelLayoutDistance = 14;
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
            _layoutDirty = true;

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
            _occupiedLabelRects.Clear();
            _visibleLabelSegments.Clear();
            UIGamePanelManager.GetScreenOccluderRects(_screenOccluderRects);

            EnsureStyles();

            if (_camera != null && _labels.Count > 0)
            {
                var mouse = Mouse.current;
                Vector2 mousePixel = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
                Vector2 mouseGui = new Vector2(mousePixel.x, Screen.height - mousePixel.y);
                bool mouseDown = mouse != null && mouse.leftButton.wasPressedThisFrame;
                var content = new GUIContent();

                RefreshLabelLayouts(content);

                for (int i = _labels.Count - 1; i >= 0; i--)
                {
                    var label = _labels[i];
                    if (!TryBuildCurrentLabelRect(label, content, out var rect))
                        continue;

                    BuildVisibleLabelSegments(rect, _visibleLabelSegments);
                    if (_visibleLabelSegments.Count == 0)
                        continue;

                    bool hovered = ContainsPoint(_visibleLabelSegments, mouseGui);

                    if (!ClickConsumedThisFrame && mouseDown && hovered && !UIGamePanelManager.IsPointerOverAnyPanel(mousePixel))
                    {
                        ClickConsumedThisFrame = true;
                        if (OnGroundItemLabelClicked != null && label.Item != null)
                        {
                            // 标签可以为了避让而重排，但拾取/寻路仍然必须使用原始掉落世界坐标。
                            OnGroundItemLabelClicked.Invoke(label.Item, label.Position);
                        }
                        else
                        {
                            TryPickupLabelAtIndex(i);
                        }
                        continue;
                    }

                    DrawLabelBackgroundClipped(rect, _visibleLabelSegments, hovered);
                    DrawLabelTextClipped(rect, _visibleLabelSegments, label.Name, label.Rarity, hovered);
                }
            }

            DrawPickupHint();
        }

        private void RefreshLabelLayouts(GUIContent content)
        {
            bool shouldRefreshLayout = _layoutDirty;

            for (int i = 0; i < _labels.Count; i++)
            {
                var label = _labels[i];
                bool isBlocked = false;

                if (!label.HasLayoutOffset)
                {
                    shouldRefreshLayout = true;
                }
                else if (TryBuildCurrentLabelRect(label, content, out var currentRect))
                {
                    isBlocked = IsGuiRectOccluded(currentRect);
                    if (isBlocked && !label.WasLayoutBlocked)
                        shouldRefreshLayout = true;
                }

                label.WasLayoutBlocked = isBlocked;
                _labels[i] = label;
            }

            if (!shouldRefreshLayout)
                return;

            _occupiedLabelRects.Clear();
            _visibleLabelSegments.Clear();

            for (int i = _labels.Count - 1; i >= 0; i--)
            {
                var label = _labels[i];
                if (!TryBuildBaseLabelRect(label, content, out var baseRect))
                {
                    label.HasLayoutOffset = false;
                    label.WasLayoutBlocked = false;
                    _labels[i] = label;
                    continue;
                }

                Rect preferredRect = label.HasLayoutOffset ? OffsetRect(baseRect, label.LayoutOffset) : baseRect;
                bool preferExistingRect = label.HasLayoutOffset && !label.WasLayoutBlocked;
                Rect resolvedRect = ResolveLabelRect(baseRect, preferredRect, preferExistingRect);

                BuildVisibleLabelSegments(resolvedRect, _visibleLabelSegments);
                for (int segmentIndex = 0; segmentIndex < _visibleLabelSegments.Count; segmentIndex++)
                {
                    _occupiedLabelRects.Add(_visibleLabelSegments[segmentIndex]);
                }

                label.LayoutOffset = new Vector2(resolvedRect.x - baseRect.x, resolvedRect.y - baseRect.y);
                label.HasLayoutOffset = true;
                label.WasLayoutBlocked = IsGuiRectOccluded(resolvedRect);
                _labels[i] = label;
            }

            _occupiedLabelRects.Clear();
            _visibleLabelSegments.Clear();
            _layoutDirty = false;
        }

        private bool TryBuildCurrentLabelRect(GroundItemLabel label, GUIContent content, out Rect rect)
        {
            rect = default;
            if (!TryBuildBaseLabelRect(label, content, out var baseRect))
                return false;

            rect = label.HasLayoutOffset ? OffsetRect(baseRect, label.LayoutOffset) : baseRect;
            return true;
        }

        private bool TryBuildBaseLabelRect(GroundItemLabel label, GUIContent content, out Rect rect)
        {
            rect = default;

            Vector3 worldPos = label.Position + new Vector3(0f, _labelWorldY, 0f);
            Vector3 screenPos = _camera.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0f)
                return false;

            content.text = label.Name;
            Vector2 textSize = _labelStyle.CalcSize(content);
            float labelW = Mathf.Max(MinLabelWidth, textSize.x + _horizontalPadding * 2f);
            float labelH = Mathf.Max(MinLabelHeight, textSize.y + _verticalPadding * 2f);
            rect = BuildBaseLabelRect(screenPos, labelW, labelH, label.StackIndex);
            return true;
        }

        private Rect ResolveLabelRect(Rect baseRect, Rect preferredRect, bool preferExistingRect)
        {
            float stepX = baseRect.width + _labelLayoutHorizontalGap;
            float stepY = baseRect.height + _labelLayoutVerticalGap;
            int maxDistance = Mathf.Max(0, _maxLabelLayoutDistance);
            float baseCenterX = baseRect.center.x;
            float baseCenterY = baseRect.center.y;

            if (preferExistingRect && CanPlaceLabelRect(preferredRect, avoidOccluders: false))
                return preferredRect;

            for (int distance = 0; distance <= maxDistance; distance++)
            {
                for (int row = 0; row <= distance; row++)
                {
                    int absColumn = distance - row;
                    if (absColumn == 0)
                    {
                        Rect centeredRect = CreateCandidateRect(baseCenterX, baseCenterY, baseRect.width, baseRect.height, row, 0, stepX, stepY);
                        if (CanPlaceLabelRect(centeredRect, avoidOccluders: true))
                            return centeredRect;

                        continue;
                    }

                    Rect leftRect = CreateCandidateRect(baseCenterX, baseCenterY, baseRect.width, baseRect.height, row, -absColumn, stepX, stepY);
                    if (CanPlaceLabelRect(leftRect, avoidOccluders: true))
                        return leftRect;

                    Rect rightRect = CreateCandidateRect(baseCenterX, baseCenterY, baseRect.width, baseRect.height, row, absColumn, stepX, stepY);
                    if (CanPlaceLabelRect(rightRect, avoidOccluders: true))
                        return rightRect;
                }
            }

            if (CanPlaceLabelRect(preferredRect, avoidOccluders: false))
                return preferredRect;

            return baseRect;
        }

        private Rect CreateCandidateRect(float baseCenterX, float baseCenterY, float labelW, float labelH, int row, int column, float stepX, float stepY)
        {
            float centerX = baseCenterX + column * stepX;
            float centerY = baseCenterY - row * stepY;
            return BuildBaseLabelRect(centerX, centerY, labelW, labelH);
        }

        private bool CanPlaceLabelRect(Rect rect, bool avoidOccluders)
        {
            if (avoidOccluders && IsGuiRectOccluded(rect))
                return false;

            for (int i = 0; i < _occupiedLabelRects.Count; i++)
            {
                if (_occupiedLabelRects[i].Overlaps(rect))
                    return false;
            }

            return true;
        }

        private Rect BuildBaseLabelRect(Vector3 screenPos, float labelW, float labelH, int stackIndex)
        {
            float baseCenterX = screenPos.x;
            float baseCenterY = Screen.height - screenPos.y - stackIndex * _stackOffsetY;
            return BuildBaseLabelRect(baseCenterX, baseCenterY, labelW, labelH);
        }

        private static Rect BuildBaseLabelRect(float centerX, float centerY, float width, float height)
        {
            return new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
        }

        private static Rect OffsetRect(Rect rect, Vector2 offset)
        {
            return new Rect(rect.x + offset.x, rect.y + offset.y, rect.width, rect.height);
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

        public bool ContainsItem(ItemData item)
        {
            return FindLabelIndex(item) >= 0;
        }

        public bool TryPickupItem(ItemData item)
        {
            return TryPickupLabelAtIndex(FindLabelIndex(item));
        }

        private int FindLabelIndex(ItemData item)
        {
            if (item == null)
                return -1;

            for (int i = _labels.Count - 1; i >= 0; i--)
            {
                if (_labels[i].Item == item)
                    return i;
            }

            return -1;
        }

        private bool TryPickupLabelAtIndex(int labelIndex)
        {
            if (labelIndex < 0 || labelIndex >= _labels.Count)
                return false;

            var label = _labels[labelIndex];
            if (label.Item == null)
                return false;

            var uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                ShowPickupHint("背包不可用");
                return false;
            }

            var bagPanel = uiManager.GetOrCreateBagPanel(false);
            if (bagPanel == null)
            {
                ShowPickupHint("背包不可用");
                return false;
            }

            var bagItem = CreateBagItemData(label.Item);
            if (bagItem == null)
            {
                ShowPickupHint("掉落数据异常");
                return false;
            }

            if (!bagPanel.CanAddItemToBag(bagItem, out var capacityFailureReason))
            {
                ShowPickupHint(string.IsNullOrWhiteSpace(capacityFailureReason) ? "背包放不下了" : "背包放不下了");
                return false;
            }

            if (!bagPanel.TryAddItemToBag(bagItem, out var failureReason))
            {
                ShowPickupHint(string.IsNullOrWhiteSpace(failureReason) ? "拾取失败" : failureReason);
                return false;
            }

            _labels.RemoveAt(labelIndex);
            return true;
        }

        private static string BuildLabelDisplayName(ItemData item)
        {
            if (item == null)
                return string.Empty;

            int stackCount = Mathf.Max(1, item.StackCount);
            if (item.IsStackable && stackCount > 1)
                return $"{item.Name} x{stackCount}";

            return item.Name ?? string.Empty;
        }

        private static BagItemData CreateBagItemData(ItemData item)
        {
            if (item == null)
                return null;

            if (item.Type == ItemType.Currency)
                return CreateCurrencyBagItemData(item);

            var bagItem = new BagItemData(item.Id, item.Name, ResolveGridWidth(item.Type), ResolveGridHeight(item.Type))
            {
                ItemKind = item.Type == ItemType.Flask ? BagItemKind.Flask : BagItemKind.Equipment,
                ItemColor = ResolveRarityColor(item.Rarity),
                RuntimeItemData = item,
            };

            ApplyAcceptedEquipmentSlots(bagItem, item);

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

        private static BagItemData CreateCurrencyBagItemData(ItemData item)
        {
            if (item == null)
                return null;

            int stackCount = Mathf.Max(1, item.StackCount);
            BagItemData bagItem = null;

            if (!string.IsNullOrWhiteSpace(item.CurrencyBaseId))
                bagItem = CurrencyBagDataFactory.CreateById(item.CurrencyBaseId, stackCount, item.Id);

            if (bagItem == null && !string.IsNullOrWhiteSpace(item.CurrencyCode))
                bagItem = CurrencyBagDataFactory.CreateByCode(item.CurrencyCode, stackCount, item.Id);

            if (bagItem == null)
            {
                bagItem = new BagItemData(item.Id, item.Name, 1, 1)
                {
                    ItemKind = BagItemKind.Currency,
                    Description = item.Description,
                    IsStackable = item.IsStackable || item.MaxStackCount > 1 || stackCount > 1,
                    StackCount = stackCount,
                    MaxStackCount = Mathf.Max(1, item.MaxStackCount),
                    CurrencyBaseId = item.CurrencyBaseId,
                    CurrencyCode = item.CurrencyCode,
                    CurrencyCategoryId = item.CurrencyCategoryId,
                    CurrencyCategoryName = item.CurrencyCategoryName,
                    CurrencyDisplayColor = item.CurrencyDisplayColor,
                    CurrencyEffectTypeId = item.CurrencyEffectTypeId,
                    CurrencyEffectTypeName = item.CurrencyEffectTypeName,
                    CurrencyTargetDescription = item.CurrencyTargetDescription,
                    CurrencyEffectDescription = item.CurrencyEffectDescription,
                    CurrencyFlavorText = item.CurrencyFlavorText,
                    CurrencyDropLevel = item.CurrencyDropLevel,
                    CurrencySortOrder = item.CurrencySortOrder,
                    CurrencyConsumesOnUse = item.CurrencyConsumesOnUse,
                    CurrencyCanApplyNormal = item.CurrencyCanApplyNormal,
                    CurrencyCanApplyMagic = item.CurrencyCanApplyMagic,
                    CurrencyCanApplyRare = item.CurrencyCanApplyRare,
                    CurrencyCanApplyUnique = item.CurrencyCanApplyUnique,
                    CurrencyCanApplyCorrupted = item.CurrencyCanApplyCorrupted,
                    ItemColor = ResolveCurrencyDisplayColor(item.CurrencyDisplayColor),
                    RuntimeItemData = item,
                };

                for (int i = 0; i < item.CurrencyAllowedItemTypes.Count; i++)
                {
                    var allowedType = item.CurrencyAllowedItemTypes[i];
                    if (!bagItem.CurrencyAllowedItemTypes.Contains(allowedType))
                        bagItem.CurrencyAllowedItemTypes.Add(allowedType);
                }

                bagItem.NormalizeStackState(clampToMax: false);
                return bagItem;
            }

            bagItem.Name = string.IsNullOrWhiteSpace(item.Name) ? bagItem.Name : item.Name;
            bagItem.Description = string.IsNullOrWhiteSpace(item.Description) ? bagItem.Description : item.Description;
            bagItem.IsStackable = true;
            bagItem.StackCount = stackCount;
            bagItem.MaxStackCount = Mathf.Max(1, item.MaxStackCount > 0 ? item.MaxStackCount : bagItem.MaxStackCount);
            bagItem.CurrencyBaseId = string.IsNullOrWhiteSpace(item.CurrencyBaseId) ? bagItem.CurrencyBaseId : item.CurrencyBaseId;
            bagItem.CurrencyCode = string.IsNullOrWhiteSpace(item.CurrencyCode) ? bagItem.CurrencyCode : item.CurrencyCode;
            bagItem.CurrencyCategoryId = string.IsNullOrWhiteSpace(item.CurrencyCategoryId) ? bagItem.CurrencyCategoryId : item.CurrencyCategoryId;
            bagItem.CurrencyCategoryName = string.IsNullOrWhiteSpace(item.CurrencyCategoryName) ? bagItem.CurrencyCategoryName : item.CurrencyCategoryName;
            bagItem.CurrencyDisplayColor = string.IsNullOrWhiteSpace(item.CurrencyDisplayColor) ? bagItem.CurrencyDisplayColor : item.CurrencyDisplayColor;
            bagItem.CurrencyEffectTypeId = string.IsNullOrWhiteSpace(item.CurrencyEffectTypeId) ? bagItem.CurrencyEffectTypeId : item.CurrencyEffectTypeId;
            bagItem.CurrencyEffectTypeName = string.IsNullOrWhiteSpace(item.CurrencyEffectTypeName) ? bagItem.CurrencyEffectTypeName : item.CurrencyEffectTypeName;
            bagItem.CurrencyTargetDescription = string.IsNullOrWhiteSpace(item.CurrencyTargetDescription) ? bagItem.CurrencyTargetDescription : item.CurrencyTargetDescription;
            bagItem.CurrencyEffectDescription = string.IsNullOrWhiteSpace(item.CurrencyEffectDescription) ? bagItem.CurrencyEffectDescription : item.CurrencyEffectDescription;
            bagItem.CurrencyFlavorText = string.IsNullOrWhiteSpace(item.CurrencyFlavorText) ? bagItem.CurrencyFlavorText : item.CurrencyFlavorText;
            bagItem.CurrencyDropLevel = item.CurrencyDropLevel > 0 ? item.CurrencyDropLevel : bagItem.CurrencyDropLevel;
            bagItem.CurrencySortOrder = item.CurrencySortOrder != 0 ? item.CurrencySortOrder : bagItem.CurrencySortOrder;
            bagItem.CurrencyConsumesOnUse = item.CurrencyConsumesOnUse;
            bagItem.CurrencyCanApplyNormal = item.CurrencyCanApplyNormal;
            bagItem.CurrencyCanApplyMagic = item.CurrencyCanApplyMagic;
            bagItem.CurrencyCanApplyRare = item.CurrencyCanApplyRare;
            bagItem.CurrencyCanApplyUnique = item.CurrencyCanApplyUnique;
            bagItem.CurrencyCanApplyCorrupted = item.CurrencyCanApplyCorrupted;
            bagItem.ItemColor = ResolveCurrencyDisplayColor(bagItem.CurrencyDisplayColor);
            bagItem.RuntimeItemData = item;

            if (item.CurrencyAllowedItemTypes.Count > 0)
            {
                bagItem.CurrencyAllowedItemTypes.Clear();
                for (int i = 0; i < item.CurrencyAllowedItemTypes.Count; i++)
                {
                    var allowedType = item.CurrencyAllowedItemTypes[i];
                    if (!bagItem.CurrencyAllowedItemTypes.Contains(allowedType))
                        bagItem.CurrencyAllowedItemTypes.Add(allowedType);
                }
            }

            bagItem.NormalizeStackState(clampToMax: false);
            return bagItem;
        }

        private static Color ResolveCurrencyDisplayColor(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && ColorUtility.TryParseHtmlString(value.Trim(), out var parsed)
                ? parsed
                : new Color(0.82f, 0.72f, 0.34f);
        }

        private static void ApplyAcceptedEquipmentSlots(BagItemData bagItem, ItemData item)
        {
            if (bagItem == null || item == null)
                return;

            var slots = ResolveAcceptedEquipmentSlots(item);
            if (slots.Count == 0)
                return;

            bagItem.AcceptedEquipmentSlot = slots[0];
            bagItem.SetAcceptedEquipmentSlots(slots);
        }

        private static List<EquipmentSlot> ResolveAcceptedEquipmentSlots(ItemData item)
        {
            var result = new List<EquipmentSlot>();
            if (item == null)
                return result;

            AddAcceptedEquipmentSlots(result, item.AllowedEquipmentSlots);
            if (result.Count == 0 && item.PrimaryEquipmentSlot.HasValue)
                AddAcceptedEquipmentSlot(result, item.PrimaryEquipmentSlot.Value);

            if (result.Count == 0)
            {
                TryAppendAcceptedSlotsFromText(result, item.BaseType);
                if (result.Count == 0)
                    TryAppendAcceptedSlotsFromText(result, item.Name);
            }

            if (result.Count == 0)
            {
                switch (item.Type)
                {
                    case ItemType.Weapon:
                        AddAcceptedEquipmentSlot(result, EquipmentSlot.MainHand);
                        AddAcceptedEquipmentSlot(result, EquipmentSlot.OffHand);
                        break;
                    case ItemType.Armour:
                        AddAcceptedEquipmentSlot(result, EquipmentSlot.BodyArmour);
                        break;
                    case ItemType.Accessory:
                        AddAcceptedEquipmentSlot(result, EquipmentSlot.Amulet);
                        break;
                }
            }

            if (item.PrimaryEquipmentSlot.HasValue)
                MovePrimarySlotToFront(result, item.PrimaryEquipmentSlot.Value);

            return result;
        }

        private static void AddAcceptedEquipmentSlots(List<EquipmentSlot> result, IEnumerable<EquipmentSlot> slots)
        {
            if (result == null || slots == null)
                return;

            foreach (var slot in slots)
                AddAcceptedEquipmentSlot(result, slot);
        }

        private static void AddAcceptedEquipmentSlot(List<EquipmentSlot> result, EquipmentSlot slot)
        {
            if (result == null || result.Contains(slot))
                return;

            result.Add(slot);
        }

        private static void MovePrimarySlotToFront(List<EquipmentSlot> result, EquipmentSlot primarySlot)
        {
            if (result == null)
                return;

            int index = result.IndexOf(primarySlot);
            if (index <= 0)
                return;

            result.RemoveAt(index);
            result.Insert(0, primarySlot);
        }

        private static bool TryAppendAcceptedSlotsFromText(List<EquipmentSlot> result, string text)
        {
            if (result == null || string.IsNullOrWhiteSpace(text))
                return false;

            if (ContainsKeyword(text, "戒指", "ring"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.RingLeft);
                AddAcceptedEquipmentSlot(result, EquipmentSlot.RingRight);
                return true;
            }

            if (ContainsKeyword(text, "项链", "护身符", "amulet"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.Amulet);
                return true;
            }

            if (ContainsKeyword(text, "腰带", "belt"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.Belt);
                return true;
            }

            if (ContainsKeyword(text, "头盔", "helmet", "helm"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.Helmet);
                return true;
            }

            if (ContainsKeyword(text, "手套", "glove"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.Gloves);
                return true;
            }

            if (ContainsKeyword(text, "鞋", "靴", "boots", "boot"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.Boots);
                return true;
            }

            if (ContainsKeyword(text, "盾", "箭袋", "法器", "副手", "shield", "quiver", "focus", "offhand"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.OffHand);
                return true;
            }

            if (ContainsKeyword(text, "胸甲", "板甲", "铠甲", "铁甲", "armor", "armour", "chest", "plate"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.BodyArmour);
                return true;
            }

            if (ContainsKeyword(text, "武器", "剑", "斧", "锤", "弓", "匕首", "爪", "杖", "长矛", "权杖", "sword", "axe", "mace", "bow", "dagger", "claw", "wand", "staff", "spear", "weapon"))
            {
                AddAcceptedEquipmentSlot(result, EquipmentSlot.MainHand);
                AddAcceptedEquipmentSlot(result, EquipmentSlot.OffHand);
                return true;
            }

            return false;
        }

        private static bool ContainsKeyword(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                var keyword = keywords[i];
                if (!string.IsNullOrWhiteSpace(keyword) &&
                    text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
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

        private void BuildVisibleLabelSegments(Rect guiRect, List<Rect> results)
        {
            results.Clear();
            _visibleLabelSegmentBuffer.Clear();
            results.Add(guiRect);

            Rect screenBounds = new Rect(0f, 0f, Screen.width, Screen.height);
            Rect screenBoundsGuiRect = UIGamePanelManager.ScreenRectToGuiRect(screenBounds);
            ClipSegmentsAgainstCutout(screenBoundsGuiRect, keepIntersection: true, results);
            if (results.Count == 0)
                return;

            for (int i = 0; i < _screenOccluderRects.Count; i++)
            {
                Rect occluderGuiRect = UIGamePanelManager.ScreenRectToGuiRect(_screenOccluderRects[i]);
                if (!occluderGuiRect.Overlaps(guiRect, true))
                    continue;

                ClipSegmentsAgainstCutout(occluderGuiRect, keepIntersection: false, results);
                if (results.Count == 0)
                    return;
            }
        }

        private void ClipSegmentsAgainstCutout(Rect clipRect, bool keepIntersection, List<Rect> results)
        {
            _visibleLabelSegmentBuffer.Clear();
            for (int segmentIndex = 0; segmentIndex < results.Count; segmentIndex++)
            {
                Rect segment = results[segmentIndex];
                if (keepIntersection)
                {
                    Rect visiblePart = IntersectRects(segment, clipRect);
                    AddRectIfVisible(_visibleLabelSegmentBuffer, visiblePart);
                }
                else
                {
                    SubtractRect(segment, clipRect, _visibleLabelSegmentBuffer);
                }
            }

            results.Clear();
            for (int segmentIndex = 0; segmentIndex < _visibleLabelSegmentBuffer.Count; segmentIndex++)
            {
                Rect segment = _visibleLabelSegmentBuffer[segmentIndex];
                if (segment.width >= MinVisibleSegmentWidth && segment.height > 0f)
                    results.Add(segment);
            }
        }

        private static void SubtractRect(Rect source, Rect cutout, List<Rect> results)
        {
            Rect overlap = IntersectRects(source, cutout);
            if (overlap.width <= 0f || overlap.height <= 0f)
            {
                results.Add(source);
                return;
            }

            AddRectIfVisible(results, Rect.MinMaxRect(source.xMin, source.yMin, source.xMax, overlap.yMin));
            AddRectIfVisible(results, Rect.MinMaxRect(source.xMin, overlap.yMax, source.xMax, source.yMax));
            AddRectIfVisible(results, Rect.MinMaxRect(source.xMin, overlap.yMin, overlap.xMin, overlap.yMax));
            AddRectIfVisible(results, Rect.MinMaxRect(overlap.xMax, overlap.yMin, source.xMax, overlap.yMax));
        }

        private static void AddRectIfVisible(List<Rect> results, Rect rect)
        {
            if (rect.width > 0f && rect.height > 0f)
                results.Add(rect);
        }

        private static Rect IntersectRects(Rect a, Rect b)

        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);

            if (xMax <= xMin || yMax <= yMin)
                return Rect.zero;

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool ContainsPoint(List<Rect> rects, Vector2 point)
        {
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Contains(point))
                    return true;
            }

            return false;
        }

        private void DrawLabelBackgroundClipped(Rect fullRect, List<Rect> visibleSegments, bool hovered)
        {
            Color backgroundColor = hovered ? _backgroundHighlightColor : _backgroundColor;
            Color borderColor = hovered ? _borderHighlightColor : _borderColor;

            DrawClippedFill(fullRect, visibleSegments, backgroundColor);
            DrawClippedFill(new Rect(fullRect.x, fullRect.y, fullRect.width, BorderThickness), visibleSegments, borderColor);
            DrawClippedFill(new Rect(fullRect.x, fullRect.yMax - BorderThickness, fullRect.width, BorderThickness), visibleSegments, borderColor);
            DrawClippedFill(new Rect(fullRect.x, fullRect.y, BorderThickness, fullRect.height), visibleSegments, borderColor);
            DrawClippedFill(new Rect(fullRect.xMax - BorderThickness, fullRect.y, BorderThickness, fullRect.height), visibleSegments, borderColor);
        }

        private static void DrawClippedFill(Rect sourceRect, List<Rect> visibleSegments, Color color)
        {
            for (int i = 0; i < visibleSegments.Count; i++)
            {
                Rect clippedRect = IntersectRects(sourceRect, visibleSegments[i]);
                if (clippedRect.width <= 0f || clippedRect.height <= 0f)
                    continue;

                Color previousColor = GUI.color;
                GUI.color = color;
                GUI.DrawTexture(clippedRect, Texture2D.whiteTexture);
                GUI.color = previousColor;
            }
        }

        private void DrawLabelTextClipped(Rect fullRect, List<Rect> visibleSegments, string name, ItemRarity rarity, bool hovered)
        {
            _labelStyle.fontSize = _labelFontSize;
            _shadowStyle.fontSize = _labelFontSize;
            _labelStyle.normal.textColor = hovered ? Color.white : ResolveRarityColor(rarity);

            for (int i = 0; i < visibleSegments.Count; i++)
            {
                Rect visibleRect = visibleSegments[i];
                Rect groupRect = new Rect(visibleRect.x, visibleRect.y, visibleRect.width, visibleRect.height);
                GUI.BeginGroup(groupRect);

                Rect localFullRect = new Rect(fullRect.x - visibleRect.x, fullRect.y - visibleRect.y, fullRect.width, fullRect.height);
                GUI.Label(new Rect(localFullRect.x + 1f, localFullRect.y + 1f, localFullRect.width, localFullRect.height), name, _shadowStyle);
                GUI.Label(localFullRect, name, _labelStyle);

                GUI.EndGroup();
            }
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