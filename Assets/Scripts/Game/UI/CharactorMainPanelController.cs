using System;
using System.Collections.Generic;
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
            public Sprite DefaultSprite;
            public Color DefaultColor;
            public bool DefaultPreserveAspect;
            public string DefaultText;
            public Color DefaultTextColor;
        }

        private static readonly string[] DefaultSkillKeys = { "1", "2", "3", "4", "5", "6", "7", "8" };

        private static Texture2D s_whiteTexture;
        private static Sprite s_whiteSprite;

        private readonly BagItemData[] _equippedPotions = new BagItemData[5];
        private readonly List<BagItemData> _socketedActiveGems = new List<BagItemData>(8);
        private readonly List<SlotView> _potionSlots = new List<SlotView>(5);
        private readonly List<SlotView> _skillSlots = new List<SlotView>(8);
        private readonly List<BagItemData> _skillSlotAssignments = new List<BagItemData>(8);

        private RectTransform _potionArr;
        private RectTransform _skillSlotArr;
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

        public void RefreshFromCurrentState()
        {
            EnsureInitialized();

            var bagPanel = UIManager.Instance?.CurrentBagPanel;
            if (bagPanel == null || bagPanel.IsInitializing)
                return;

            bagPanel.FillEquippedPotions(_equippedPotions);
            bagPanel.GetSocketedActiveGems(_socketedActiveGems);
            SyncSkillSlotAssignments();

            ApplyPotions();
            ApplySkills();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _potionArr = FindChildRecursive(transform, "PotionArr") as RectTransform;
            _skillSlotArr = FindChildRecursive(transform, "SkillSlotArr") as RectTransform;

            BuildSlots(_potionArr, "Potion", _potionSlots, false);
            BuildSlots(_skillSlotArr, "Skill", _skillSlots, true);
            EnsureSkillSlotAssignmentCapacity();

            ResetPotionSlots();
            ResetSkillSlots();
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

                target.Add(new SlotView
                {
                    Index = index,
                    Image = image,
                    Label = label,
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

        private void ApplyPotions()
        {
            for (int i = 0; i < _potionSlots.Count; i++)
            {
                var data = i < _equippedPotions.Length ? _equippedPotions[i] : null;
                ApplyItemToSlot(_potionSlots[i], data, false);
            }
        }

        private void ApplySkills()
        {
            EnsureSkillSlotAssignmentCapacity();

            for (int i = 0; i < _skillSlots.Count; i++)
            {
                var data = i < _skillSlotAssignments.Count ? _skillSlotAssignments[i] : null;
                ApplyItemToSlot(_skillSlots[i], data, true);
            }
        }

        private void SyncSkillSlotAssignments()
        {
            EnsureSkillSlotAssignmentCapacity();

            for (int i = 0; i < _skillSlotAssignments.Count; i++)
            {
                var assignedGem = _skillSlotAssignments[i];
                if (assignedGem != null && !_socketedActiveGems.Contains(assignedGem))
                    _skillSlotAssignments[i] = null;
            }

            for (int i = 0; i < _socketedActiveGems.Count; i++)
            {
                var gemData = _socketedActiveGems[i];
                if (gemData == null || FindAssignedSkillSlotIndex(gemData) >= 0)
                    continue;

                int emptySlotIndex = FindFirstEmptySkillSlotIndex();
                if (emptySlotIndex < 0)
                    break;

                _skillSlotAssignments[emptySlotIndex] = gemData;
            }
        }

        private void EnsureSkillSlotAssignmentCapacity()
        {
            while (_skillSlotAssignments.Count < _skillSlots.Count)
                _skillSlotAssignments.Add(null);

            if (_skillSlotAssignments.Count > _skillSlots.Count)
                _skillSlotAssignments.RemoveRange(_skillSlots.Count, _skillSlotAssignments.Count - _skillSlots.Count);
        }

        private int FindAssignedSkillSlotIndex(BagItemData gemData)
        {
            if (gemData == null)
                return -1;

            for (int i = 0; i < _skillSlotAssignments.Count; i++)
            {
                if (_skillSlotAssignments[i] == gemData)
                    return i;
            }

            return -1;
        }

        private int FindFirstEmptySkillSlotIndex()
        {
            for (int i = 0; i < _skillSlotAssignments.Count; i++)
            {
                if (_skillSlotAssignments[i] == null)
                    return i;
            }

            return -1;
        }

        private void ApplyItemToSlot(SlotView slot, BagItemData data, bool isSkillSlot)
        {
            if (slot == null)
                return;

            if (data == null)
            {
                RestoreSlot(slot);
                return;
            }

            ApplyImage(slot, data);

            if (isSkillSlot && slot.Label != null)
            {
                slot.Label.text = ResolveSkillSlotText(slot.Index - 1, data);
                slot.Label.color = data.Icon != null
                    ? slot.DefaultTextColor
                    : ResolveContrastingTextColor(data.ItemColor);
            }
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
        }

        private static void PrepareSkillLabel(TextMeshProUGUI label)
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 12f;
            label.fontSizeMax = Mathf.Max(label.fontSizeMax, label.fontSize);
            label.alignment = TextAlignmentOptions.Center;
        }

        private static string ResolveSkillSlotText(int index, BagItemData data)
        {
            string defaultKey = ResolveDefaultSkillKey(index);
            if (data == null)
                return defaultKey;

            if (data.Icon != null || string.IsNullOrWhiteSpace(data.Name))
                return defaultKey;

            return $"{defaultKey}\n{BuildShortLabel(data.Name)}";
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
            if (index >= 0 && index < DefaultSkillKeys.Length)
                return DefaultSkillKeys[index];

            return (index + 1).ToString();
        }

        private static Color ResolveContrastingTextColor(Color background)
        {
            float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
            return luminance < 0.55f ? Color.white : Color.black;
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