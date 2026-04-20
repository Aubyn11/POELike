using System;
using System.Collections.Generic;
using POELike.ECS.Components;
using POELike.ECS.Core;
using POELike.ECS.Systems;
using POELike.Game.Equipment;
using POELike.Game.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using POELike.Managers;

namespace POELike.Game.UI
{
    [DisallowMultipleComponent]
    public class CharactorMainPanelController : MonoBehaviour
    {
        private sealed class SlotView
        {
            public int Index;
            public Image Image;
            public TextMeshProUGUI Label;
            public ListBox SupportSlotListBox;
            public SkillBarSlotButton SkillButton;
            public Image CooldownMask;
            public Sprite DefaultSprite;
            public Color DefaultColor;
            public bool DefaultPreserveAspect;
            public string DefaultText;
            public Color DefaultTextColor;
        }

        private static readonly string[] DefaultSkillKeys = { "LMB", "MMB", "RMB", "Q", "W", "E", "R", "T" };

        private static readonly EquipmentSlot[] FlaskSlots =
        {
            EquipmentSlot.Flask1,
            EquipmentSlot.Flask2,
            EquipmentSlot.Flask3,
            EquipmentSlot.Flask4,
            EquipmentSlot.Flask5,
        };

        private static Texture2D s_whiteTexture;
        private static Sprite s_whiteSprite;

        private readonly BagItemData[] _equippedPotions = new BagItemData[5];
        private readonly List<BagItemData> _socketedActiveGems = new List<BagItemData>(8);
        private readonly List<BagItemData> _linkedSupportGems = new List<BagItemData>(4);
        private readonly List<BagItemData> _linkedGemsScratch = new List<BagItemData>(4);
        private readonly List<SlotView> _potionSlots = new List<SlotView>(5);
        private readonly List<SlotView> _skillSlots = new List<SlotView>(8);
        private readonly List<BagItemData> _skillSlotAssignments = new List<BagItemData>(8);
        private readonly List<SupportGem> _supportGemScratch = new List<SupportGem>(4);

        private RectTransform _potionArr;
        private RectTransform _skillSlotArr;
        private Image _hpMaskImage;
        private Image _mpMaskImage;
        private HealthComponent _boundHealthComponent;
        private bool _initialized;

        private static Sprite SharedWhiteSprite
        {
            get
            {
                if (s_whiteSprite != null)
                    return s_whiteSprite;

                if (s_whiteTexture == null)
                {
                    s_whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    s_whiteTexture.SetPixel(0, 0, Color.white);
                    s_whiteTexture.Apply();
                    s_whiteTexture.hideFlags = HideFlags.HideAndDontSave;
                }

                s_whiteSprite = Sprite.Create(
                    s_whiteTexture,
                    new Rect(0, 0, 1, 1),
                    new Vector2(0.5f, 0.5f));
                s_whiteSprite.hideFlags = HideFlags.HideAndDontSave;
                return s_whiteSprite;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            RefreshFromCurrentState();
        }

        private void OnDisable()
        {
            UnbindHealthComponent();
        }

        private void Update()
        {
            if (!_initialized)
                return;

            UpdateSkillCooldownMasks();
            UpdatePotionChargeMasks();
        }

        public void RefreshFromCurrentState()
        {
            EnsureInitialized();
            BindHealthComponent();
            SyncPlayerLoadout();
            ApplyPotions();
            ApplySkills();
            ApplyHealthAndManaMasks();
        }

        public void OnSkillSlotClicked(int slotIndex)
        {
            var world = GameManager.Instance?.World;
            var player = world?.FindEntityByTag("Player");
            var skillComp = player?.GetComponent<SkillComponent>();
            if (world == null || player == null || skillComp == null)
                return;

            var slot = skillComp.GetSlot(slotIndex);
            if (slot == null || !slot.HasSkill)
                return;

            world.EventBus.Publish(new SkillActivateEvent
            {
                Caster = player,
                Slot = slot,
            });

            RefreshFromCurrentState();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _potionArr = FindChildRecursive(transform, "PotionArr") as RectTransform;
            _skillSlotArr = FindChildRecursive(transform, "SkillSlotArr") as RectTransform;
            _hpMaskImage = FindChildRecursive(transform, "Hp")?.Find("Mask")?.GetComponent<Image>();
            _mpMaskImage = FindChildRecursive(transform, "Mp")?.Find("Mask")?.GetComponent<Image>();

            BuildSlots(_potionArr, "Potion", _potionSlots, false);
            BuildSlots(_skillSlotArr, "Skill", _skillSlots, true);
            EnsureSkillSlotAssignmentCapacity();

            ResetPotionSlots();
            ResetSkillSlots();
            ApplyHealthAndManaMasks();
            _initialized = true;

        }

        private void BuildSlots(Transform root, string prefix, List<SlotView> target, bool includeLabel)
        {
            target.Clear();
            if (root == null)
                return;

            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!TryParseIndexedName(child.name, prefix, out int index))
                    continue;

                var image = child.GetComponent<Image>();
                var label = includeLabel ? child.GetComponentInChildren<TextMeshProUGUI>(true) : null;
                if (label != null)
                    PrepareSkillLabel(label);

                var supportSlotListBox = includeLabel
                    ? FindChildRecursive(child, "SkillSlot")?.GetComponent<ListBox>()
                    : null;
                if (supportSlotListBox != null)
                    supportSlotListBox.StretchItemsOnCrossAxis = false;

                var cooldownMask = (child.Find("Mask") ?? FindChildRecursive(child, "Mask"))?.GetComponent<Image>();
                if (cooldownMask == null && !includeLabel)
                    cooldownMask = CreateSlotOverlayMask(child);
                if (cooldownMask != null)
                    cooldownMask.raycastTarget = false;

                SkillBarSlotButton skillButton = null;
                if (includeLabel)
                {
                    skillButton = child.GetComponent<SkillBarSlotButton>();
                    if (skillButton == null)
                        skillButton = child.gameObject.AddComponent<SkillBarSlotButton>();
                    skillButton.Bind(this, index - 1);
                }

                target.Add(new SlotView
                {
                    Index = index,
                    Image = image,
                    Label = label,
                    SupportSlotListBox = supportSlotListBox,
                    SkillButton = skillButton,
                    CooldownMask = cooldownMask,
                    DefaultSprite = image != null ? image.sprite : null,
                    DefaultColor = image != null ? image.color : Color.white,
                    DefaultPreserveAspect = image != null && image.preserveAspect,
                    DefaultText = includeLabel ? ResolveDefaultSkillKey(index - 1) : string.Empty,
                    DefaultTextColor = label != null ? label.color : Color.white,
                });
            }

            target.Sort((left, right) => left.Index.CompareTo(right.Index));
        }

        private void ResetPotionSlots()
        {
            for (int i = 0; i < _potionSlots.Count; i++)
                RestoreSlot(_potionSlots[i]);
        }

        private void ResetSkillSlots()
        {
            for (int i = 0; i < _skillSlots.Count; i++)
                RestoreSlot(_skillSlots[i]);
        }

        private void SyncPlayerLoadout()
        {
            SyncPotionsFromPlayerEquipment();
            SyncSocketedGems();
            SyncPlayerSkillSlotsFromGems();
        }

        private void SyncPotionsFromPlayerEquipment()
        {
            Array.Clear(_equippedPotions, 0, _equippedPotions.Length);

            var bagPanel = UIManager.Instance?.CurrentBagPanel;
            if (bagPanel != null && !bagPanel.IsInitializing)
            {
                bagPanel.FillEquippedPotions(_equippedPotions);
                return;
            }

            var player = GameManager.Instance?.World?.FindEntityByTag("Player");
            var equipment = player?.GetComponent<EquipmentComponent>();
            if (equipment == null)
                return;

            for (int i = 0; i < FlaskSlots.Length && i < _equippedPotions.Length; i++)
            {
                var item = equipment.GetEquipped(FlaskSlots[i]);
                if (item == null)
                    continue;

                _equippedPotions[i] = new BagItemData(item.Id, item.Name, 1, 1)
                {
                    ItemKind = BagItemKind.Flask,
                    Rarity = item.Rarity,
                    FlaskType = item.FlaskType,
                    FlaskMaxCharges = item.FlaskMaxCharges,
                    FlaskCurrentCharges = item.FlaskCurrentCharges,
                    FlaskChargesPerUse = item.FlaskChargesPerUse,
                    FlaskQualityPercent = item.FlaskQualityPercent,
                    RuntimeItemData = item,
                    ItemColor = Color.white,
                };
            }
        }

        private void SyncSocketedGems()
        {
            var bagPanel = UIManager.Instance?.CurrentBagPanel;
            if (bagPanel == null || bagPanel.IsInitializing)
                return;

            _socketedActiveGems.Clear();
            bagPanel.GetSocketedActiveGems(_socketedActiveGems);
        }

        private void ApplyPotions()
        {
            for (int i = 0; i < _potionSlots.Count; i++)
            {
                var data = i < _equippedPotions.Length ? _equippedPotions[i] : null;
                ApplyItemToSlot(_potionSlots[i], data, false, null);
                ApplyFlaskChargeMask(_potionSlots[i], data);
            }
        }

        private void ApplySkills()
        {
            var skillComp = GameManager.Instance?.World?.FindEntityByTag("Player")?.GetComponent<SkillComponent>();
            if (skillComp == null)
            {
                ResetSkillSlots();
                return;
            }

            skillComp.EnsureSlotCapacity(_skillSlots.Count);
            for (int i = 0; i < _skillSlots.Count; i++)
            {
                var slot = skillComp.GetSlot(i);
                var data = i < _skillSlotAssignments.Count ? _skillSlotAssignments[i] : null;
                ApplyItemToSlot(_skillSlots[i], data, true, slot);
                ApplySupportGemsToSkillSlot(_skillSlots[i], slot, data);
            }

            UpdateSkillCooldownMasks(skillComp);
        }

        private void UpdateSkillCooldownMasks()
        {
            var skillComp = GameManager.Instance?.World?.FindEntityByTag("Player")?.GetComponent<SkillComponent>();
            UpdateSkillCooldownMasks(skillComp);
        }

        private void UpdateSkillCooldownMasks(SkillComponent skillComp)
        {
            if (_skillSlots.Count == 0)
                return;

            if (skillComp == null)
            {
                for (int i = 0; i < _skillSlots.Count; i++)
                    ApplySkillCooldownMask(_skillSlots[i], null);
                return;
            }

            skillComp.EnsureSlotCapacity(_skillSlots.Count);
            for (int i = 0; i < _skillSlots.Count; i++)
                ApplySkillCooldownMask(_skillSlots[i], skillComp.GetSlot(i));
        }

        private void UpdatePotionChargeMasks()
        {
            if (_potionSlots.Count == 0)
                return;

            for (int i = 0; i < _potionSlots.Count; i++)
            {
                var data = i < _equippedPotions.Length ? _equippedPotions[i] : null;
                ApplyFlaskChargeMask(_potionSlots[i], data);
            }
        }

        private static void ApplySkillCooldownMask(SlotView slot, SkillSlot runtimeSlot)
        {
            if (slot?.CooldownMask == null)
                return;

            float cooldownPercent = ResolveSkillCooldownPercent(runtimeSlot);
            bool showMask = cooldownPercent > 0f;
            if (slot.CooldownMask.gameObject.activeSelf != showMask)
                slot.CooldownMask.gameObject.SetActive(showMask);

            if (!showMask)
                return;

            slot.CooldownMask.type = Image.Type.Filled;
            slot.CooldownMask.fillMethod = Image.FillMethod.Radial360;
            slot.CooldownMask.fillOrigin = (int)Image.Origin360.Top;
            slot.CooldownMask.fillClockwise = true;
            slot.CooldownMask.fillAmount = cooldownPercent;

        }

        private static void ApplyFlaskChargeMask(SlotView slot, BagItemData flaskData)
        {
            if (slot?.CooldownMask == null)
                return;

            bool hasFlask = flaskData != null && flaskData.IsFlask;
            if (!hasFlask)
            {
                if (slot.CooldownMask.gameObject.activeSelf)
                    slot.CooldownMask.gameObject.SetActive(false);
                return;
            }

            float remainingPercent = flaskData.ResolveFlaskChargePercent();
            float consumedPercent = 1f - remainingPercent;
            bool showMask = consumedPercent > 0f;
            if (slot.CooldownMask.gameObject.activeSelf != showMask)
                slot.CooldownMask.gameObject.SetActive(showMask);

            if (!showMask)
                return;

            slot.CooldownMask.sprite = SharedWhiteSprite;
            slot.CooldownMask.color = new Color(0f, 0f, 0f, 0.72f);
            slot.CooldownMask.type = Image.Type.Filled;
            slot.CooldownMask.fillMethod = Image.FillMethod.Vertical;
            slot.CooldownMask.fillOrigin = (int)Image.OriginVertical.Top;
            slot.CooldownMask.fillClockwise = false;
            slot.CooldownMask.fillAmount = consumedPercent;
        }

        private static Image CreateSlotOverlayMask(Transform slotTransform)
        {
            if (slotTransform == null)
                return null;

            var maskObject = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            maskObject.transform.SetParent(slotTransform, false);
            maskObject.transform.SetAsLastSibling();

            var rectTransform = maskObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;

            var image = maskObject.GetComponent<Image>();
            image.sprite = SharedWhiteSprite;
            image.color = new Color(0f, 0f, 0f, 0.72f);
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        private static float ResolveSkillCooldownPercent(SkillSlot runtimeSlot)
        {
            if (runtimeSlot?.SkillData == null)
                return 0f;

            float totalCooldown = runtimeSlot.SkillData.Cooldown;
            if (totalCooldown <= 0f || runtimeSlot.CooldownTimer <= 0f)
                return 0f;

            return Mathf.Clamp01(runtimeSlot.CooldownTimer / totalCooldown);
        }

        private void SyncPlayerSkillSlotsFromGems()
        {
            EnsureSkillSlotAssignmentCapacity();

            var player = GameManager.Instance?.World?.FindEntityByTag("Player");
            var skillComp = player?.GetComponent<SkillComponent>();
            if (skillComp == null)
                return;

            skillComp.EnsureSlotCapacity(_skillSlots.Count);

            for (int i = 0; i < skillComp.SkillSlots.Count; i++)
            {
                var slot = skillComp.SkillSlots[i];
                if (slot != null && !string.IsNullOrWhiteSpace(slot.BoundActiveGemId))
                {
                    if (FindActiveGemByItemId(slot.BoundActiveGemId) == null)
                    {
                        slot.BoundActiveGemId = string.Empty;
                        slot.SkillData = null;
                    }
                }
            }

            for (int i = 0; i < _skillSlots.Count; i++)
            {
                var slot = skillComp.GetSlot(i);
                if (slot == null)
                    continue;

                BagItemData assignedGem = FindActiveGemByItemId(slot.BoundActiveGemId);
                if (assignedGem == null && i < _skillSlotAssignments.Count)
                    assignedGem = _skillSlotAssignments[i];
                if (assignedGem == null)
                    assignedGem = FindFirstUnassignedActiveGem(skillComp);

                if (i < _skillSlotAssignments.Count)
                    _skillSlotAssignments[i] = assignedGem;

                if (assignedGem != null)
                {
                    slot.BoundActiveGemId = assignedGem.ItemId ?? string.Empty;
                    slot.SkillData = BuildSkillFromGem(assignedGem);
                }
                else if (string.IsNullOrWhiteSpace(slot.BoundActiveGemId))
                {
                    PreserveDefaultSkillIfNeeded(slot);
                }
            }
        }

        private void PreserveDefaultSkillIfNeeded(SkillSlot slot)
        {
            if (slot == null || slot.SkillData != null)
                return;

            slot.SkillData = CreateDefaultSkillForSlot(slot.SlotIndex);
        }

        private BagItemData FindFirstUnassignedActiveGem(SkillComponent skillComp)
        {
            for (int i = 0; i < _socketedActiveGems.Count; i++)
            {
                var gem = _socketedActiveGems[i];
                if (gem == null)
                    continue;

                bool alreadyAssigned = false;
                for (int slotIndex = 0; slotIndex < skillComp.SkillSlots.Count; slotIndex++)
                {
                    var slot = skillComp.SkillSlots[slotIndex];
                    if (slot != null && string.Equals(slot.BoundActiveGemId, gem.ItemId, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyAssigned = true;
                        break;
                    }
                }

                if (!alreadyAssigned)
                    return gem;
            }

            return null;
        }

        private BagItemData FindActiveGemByItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            for (int i = 0; i < _socketedActiveGems.Count; i++)
            {
                var gem = _socketedActiveGems[i];
                if (gem != null && string.Equals(gem.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
                    return gem;
            }

            return null;
        }

        private void EnsureSkillSlotAssignmentCapacity()
        {
            while (_skillSlotAssignments.Count < _skillSlots.Count)
                _skillSlotAssignments.Add(null);

            if (_skillSlotAssignments.Count > _skillSlots.Count)
                _skillSlotAssignments.RemoveRange(_skillSlots.Count, _skillSlotAssignments.Count - _skillSlots.Count);
        }

        private void ApplySupportGemsToSkillSlot(SlotView slot, SkillSlot runtimeSlot, BagItemData activeGem)
        {
            if (slot?.SupportSlotListBox == null)
                return;

            slot.SupportSlotListBox.Clear();

            if (runtimeSlot?.SkillData == null)
                return;

            _supportGemScratch.Clear();
            CollectSupportGems(runtimeSlot, activeGem, _supportGemScratch);
            for (int i = 0; i < _supportGemScratch.Count; i++)
            {
                var supportGem = _supportGemScratch[i];
                if (supportGem == null)
                    continue;

                slot.SupportSlotListBox.AddItem(0, 1);
                var item = slot.SupportSlotListBox.GetItemByIndex(slot.SupportSlotListBox.Count - 1);
                ConfigureSupportGemItem(item, supportGem);
            }

            slot.SupportSlotListBox.RefreshLayout();
        }

        private void CollectSupportGems(SkillSlot runtimeSlot, BagItemData activeGem, List<SupportGem> results)
        {
            results.Clear();
            if (runtimeSlot?.SkillData == null)
                return;

            var seenSupportKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var supportGem in runtimeSlot.SkillData.SupportGems)
            {
                if (supportGem == null)
                    continue;

                string key = string.IsNullOrWhiteSpace(supportGem.Id) ? supportGem.Name : supportGem.Id;
                if (seenSupportKeys.Add(key ?? string.Empty))
                    results.Add(supportGem);
            }

            if (activeGem == null)
                return;

            _linkedSupportGems.Clear();
            CollectLinkedSupportGems(activeGem, _linkedSupportGems);
            for (int i = 0; i < _linkedSupportGems.Count; i++)
            {
                var supportData = _linkedSupportGems[i];
                var supportGem = SkillFactory.TryCreateSupportGemFromGem(supportData);
                if (supportGem == null)
                    continue;

                string key = string.IsNullOrWhiteSpace(supportGem.Id) ? supportGem.Name : supportGem.Id;
                if (seenSupportKeys.Add(key ?? string.Empty))
                    results.Add(supportGem);
            }
        }

        private void CollectLinkedSupportGems(BagItemData activeGem, List<BagItemData> results)
        {
            results.Clear();
            if (activeGem == null)
                return;

            var bagPanel = UIManager.Instance?.CurrentBagPanel;
            if (bagPanel == null || bagPanel.IsInitializing)
                return;

            var equippedSlots = Enum.GetValues(typeof(EquipmentSlot));
            for (int slotIndex = 0; slotIndex < equippedSlots.Length; slotIndex++)
            {
                var equipmentSlot = (EquipmentSlot)equippedSlots.GetValue(slotIndex);
                var equippedItem = bagPanel.GetEquippedItemData(equipmentSlot);
                if (equippedItem?.Sockets == null || equippedItem.Sockets.Count == 0)
                    continue;

                for (int socketIndex = 0; socketIndex < equippedItem.Sockets.Count; socketIndex++)
                {
                    bagPanel.GetLinkedGems(equipmentSlot, socketIndex, _linkedGemsScratch);
                    if (_linkedGemsScratch.Count == 0 || !_linkedGemsScratch.Contains(activeGem))
                        continue;

                    for (int linkedIndex = 0; linkedIndex < _linkedGemsScratch.Count; linkedIndex++)
                    {
                        var linkedGem = _linkedGemsScratch[linkedIndex];
                        if (linkedGem == null || linkedGem == activeGem || !linkedGem.IsSupportSkillGem || results.Contains(linkedGem))
                            continue;

                        results.Add(linkedGem);
                    }
                }
            }
        }

        private static void ConfigureSupportGemItem(ListBoxItem item, SupportGem supportGem)
        {
            if (item == null || supportGem == null)
                return;

            var root = item.GetCtrl().transform;
            var bgImage = FindChildRecursive(root, "Bg")?.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.sprite = SharedWhiteSprite;
                bgImage.color = ResolveSupportGemDisplayColor(supportGem);
            }

            var colorText = FindChildRecursive(root, "Color")?.GetComponent<Text>();
            if (colorText != null)
                colorText.text = BuildSupportGemShortLabel(supportGem);
        }

        private static Color ResolveSupportGemDisplayColor(SupportGem supportGem)
        {
            if (supportGem == null)
                return Color.white;

            return supportGem.Type switch
            {
                SupportGemType.MultiProjectile or SupportGemType.GMP or SupportGemType.LMP => new Color(0.25f, 0.86f, 0.38f),
                SupportGemType.AddedFireDamage => new Color(0.92f, 0.30f, 0.24f),
                SupportGemType.AddedColdDamage => new Color(0.35f, 0.58f, 1.00f),
                SupportGemType.AddedLightningDamage => new Color(1.00f, 0.90f, 0.30f),
                _ => new Color(0.85f, 0.85f, 0.85f),
            };
        }

        private static string BuildSupportGemShortLabel(SupportGem supportGem)
        {
            if (supportGem == null)
                return "?";

            return supportGem.Type switch
            {
                SupportGemType.MultiProjectile => "MP",
                SupportGemType.GMP => "G",
                SupportGemType.LMP => "L",
                SupportGemType.AddedFireDamage => "F",
                SupportGemType.AddedColdDamage => "C",
                SupportGemType.AddedLightningDamage => "L",
                _ => BuildShortLabel(supportGem.Name),
            };
        }

        private void ApplyItemToSlot(SlotView slot, BagItemData data, bool isSkillSlot, SkillSlot runtimeSlot)
        {
            if (slot == null)
                return;

            if (data == null)
            {
                if (isSkillSlot && runtimeSlot?.SkillData != null)
                {
                    ApplyVirtualSkillToSlot(slot, runtimeSlot);
                    return;
                }

                RestoreSlot(slot);
                return;
            }

            ApplyImage(slot, data);

            if (isSkillSlot && slot.Label != null)
            {
                slot.Label.text = ResolveSkillSlotText(slot.Index - 1, data, runtimeSlot);
                slot.Label.color = data.Icon != null
                    ? slot.DefaultTextColor
                    : ResolveContrastingTextColor(data.ItemColor);
            }
        }

        private void ApplyVirtualSkillToSlot(SlotView slot, SkillSlot runtimeSlot)
        {
            if (slot?.Image == null || runtimeSlot?.SkillData == null)
                return;

            var color = ResolveVirtualSkillColor(runtimeSlot.SkillData);
            slot.Image.sprite = SharedWhiteSprite;
            slot.Image.color = color;
            slot.Image.preserveAspect = false;

            if (slot.Label != null)
            {
                slot.Label.text = ResolveSkillSlotText(slot.Index - 1, null, runtimeSlot);
                slot.Label.color = ResolveContrastingTextColor(color);
            }
        }

        private static Color ResolveVirtualSkillColor(SkillData skill)
        {
            string key = $"{skill?.Id} {skill?.Name}".ToLowerInvariant();
            if (key.Contains("fire") || key.Contains("火"))
                return new Color(0.92f, 0.30f, 0.24f);
            if (key.Contains("frost") || key.Contains("ice") || key.Contains("cold") || key.Contains("冰"))
                return new Color(0.35f, 0.58f, 1.00f);
            if (key.Contains("blink") || key.Contains("闪"))
                return new Color(0.76f, 0.63f, 1.00f);
            if (key.Contains("cyclone") || key.Contains("旋"))
                return new Color(0.30f, 0.82f, 0.82f);
            return new Color(0.85f, 0.85f, 0.85f);
        }

        private void ApplyImage(SlotView slot, BagItemData data)
        {
            if (slot.Image == null)
                return;

            if (data.Icon != null)
            {
                slot.Image.sprite = data.Icon;
                slot.Image.color = Color.white;
                slot.Image.preserveAspect = true;
                return;
            }

            var color = data.ItemColor;
            if (color.a <= 0.01f)
                color.a = 1f;

            slot.Image.sprite = SharedWhiteSprite;
            slot.Image.color = color;
            slot.Image.preserveAspect = false;
        }

        private void RestoreSlot(SlotView slot)
        {
            if (slot == null)
                return;

            if (slot.Image != null)
            {
                slot.Image.sprite = slot.DefaultSprite;
                slot.Image.color = slot.DefaultColor;
                slot.Image.preserveAspect = slot.DefaultPreserveAspect;
            }

            if (slot.Label != null)
            {
                slot.Label.text = slot.DefaultText;
                slot.Label.color = slot.DefaultTextColor;
            }

            ApplySkillCooldownMask(slot, null);
        }

        private void BindHealthComponent()
        {
            var healthComponent = GameManager.Instance?.World?.FindEntityByTag("Player")?.GetComponent<HealthComponent>();
            if (ReferenceEquals(_boundHealthComponent, healthComponent))
                return;

            UnbindHealthComponent();
            _boundHealthComponent = healthComponent;
            if (_boundHealthComponent == null)
                return;

            _boundHealthComponent.OnHealthChanged += HandleHealthChanged;
            _boundHealthComponent.OnManaChanged += HandleManaChanged;
        }

        private void UnbindHealthComponent()
        {
            if (_boundHealthComponent == null)
                return;

            _boundHealthComponent.OnHealthChanged -= HandleHealthChanged;
            _boundHealthComponent.OnManaChanged -= HandleManaChanged;
            _boundHealthComponent = null;
        }

        private void HandleHealthChanged(float _, float __)
        {
            ApplyHealthAndManaMasks();
        }

        private void HandleManaChanged(float _, float __)
        {
            ApplyHealthAndManaMasks();
        }

        private void ApplyHealthAndManaMasks()
        {
            float hpPercent = _boundHealthComponent?.HealthPercent ?? 0f;
            float mpPercent = _boundHealthComponent?.ManaPercent ?? 0f;

            ApplyMaskFill(_hpMaskImage, hpPercent);
            ApplyMaskFill(_mpMaskImage, mpPercent);
        }

        private static void ApplyMaskFill(Image maskImage, float percent)
        {
            if (maskImage == null)
                return;

            percent = Mathf.Clamp01(percent);
            maskImage.type = Image.Type.Filled;
            maskImage.fillMethod = Image.FillMethod.Vertical;
            maskImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            maskImage.fillClockwise = false;
            maskImage.fillAmount = percent;
        }

        private static void PrepareSkillLabel(TextMeshProUGUI label)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Max(label.fontSizeMax, label.fontSize);
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
        }

        private static string ResolveSkillSlotText(int index, BagItemData data, SkillSlot runtimeSlot)
        {

            string defaultKey = ResolveDefaultSkillKey(index);
            if (runtimeSlot?.SkillData == null)
                return data == null ? defaultKey : ResolveFallbackSkillLabel(defaultKey, data.Name);

            string skillName = runtimeSlot.SkillData.Name;
            string shortName = BuildShortLabel(skillName);
            string cooldown = runtimeSlot.IsOnCooldown ? $"\n{Mathf.CeilToInt(runtimeSlot.CooldownTimer)}s" : string.Empty;
            return $"{defaultKey}\n{shortName}{cooldown}";
        }

        private static string ResolveFallbackSkillLabel(string defaultKey, string itemName)
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return defaultKey;

            return $"{defaultKey}\n{BuildShortLabel(itemName)}";
        }

        private static string BuildShortLabel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim();
            return text.Length <= 4 ? text : text.Substring(0, 4);
        }

        private static string ResolveDefaultSkillKey(int index)
        {
            return index >= 0 && index < DefaultSkillKeys.Length
                ? DefaultSkillKeys[index]
                : (index + 1).ToString();
        }

        private static Color ResolveContrastingTextColor(Color background)
        {
            float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
            return luminance < 0.55f ? Color.white : Color.black;
        }

        private SkillData BuildSkillFromGem(BagItemData activeGem)
        {
            if (activeGem == null || !activeGem.IsActiveSkillGem)
                return null;

            var skill = SkillFactory.TryCreateSkillFromGem(activeGem);
            if (skill == null)
                return null;

            _linkedSupportGems.Clear();
            CollectLinkedSupportGems(activeGem, _linkedSupportGems);
            for (int i = 0; i < _linkedSupportGems.Count; i++)
            {
                var supportGem = SkillFactory.TryCreateSupportGemFromGem(_linkedSupportGems[i]);
                if (supportGem != null)
                    skill.WithSupportGem(supportGem);
            }

            return skill;
        }

        private static SkillData CreateDefaultSkillForSlot(int slotIndex)
        {
            return slotIndex switch
            {
                0 => SkillFactory.CreateHeavyStrike(),
                1 => SkillFactory.CreateFireball(),
                2 => SkillFactory.CreateFrostNova(),
                3 => SkillFactory.CreateBlink(),
                4 => SkillFactory.CreateCyclone(),
                _ => null,
            };
        }

        private static bool TryParseIndexedName(string objectName, string prefix, out int index)
        {
            index = -1;
            if (string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(prefix))
                return false;

            if (!objectName.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string suffix = objectName.Substring(prefix.Length);
            if (string.IsNullOrEmpty(suffix))
                return false;

            for (int i = 0; i < suffix.Length; i++)
            {
                if (!char.IsDigit(suffix[i]))
                    return false;
            }

            return int.TryParse(suffix, out index);
        }

        private static Transform FindChildRecursive(Transform root, string nodeName)
        {
            if (root == null || string.IsNullOrWhiteSpace(nodeName))
                return null;

            if (root.name == nodeName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChildRecursive(root.GetChild(i), nodeName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}