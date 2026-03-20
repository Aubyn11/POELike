using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

namespace POELike.Game.UI
{
    /// <summary>
    /// 角色列表中的单个条目
    /// 挂载在 CharacterSlotItem Prefab 根节点上
    /// </summary>
    public class CharacterSlotItem : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI 引用")]
        [SerializeField] public Image           AvatarImage;
        [SerializeField] public TextMeshProUGUI NameText;
        [SerializeField] public TextMeshProUGUI LevelText;
        [SerializeField] public TextMeshProUGUI ServerText;
        [SerializeField] public TextMeshProUGUI LastPlayText;
        [SerializeField] public Image           HighlightBg;    // 选中高亮背景
        [SerializeField] public Image           NormalBg;       // 普通背景

        [Header("高亮颜色")]
        [SerializeField] public Color HighlightColor = new Color(0.98f, 0.78f, 0.25f, 0.25f); // 金色半透明
        [SerializeField] public Color NormalColor    = new Color(0f,    0f,    0f,    0.35f);

        // ── 数据 ──────────────────────────────────────────────────────
        public CharacterSaveData Data { get; private set; }

        // ── 事件 ──────────────────────────────────────────────────────
        /// <summary>点击时通知父面板</summary>
        public event Action<CharacterSlotItem> OnSelected;

        private bool _isSelected;

        // ── 初始化 ────────────────────────────────────────────────────
        public void Bind(CharacterSaveData data)
        {
            Data = data;

            if (NameText)     NameText.text     = data.CharacterName;
            if (LevelText)    LevelText.text     = $"Lv.{data.Level}";
            if (ServerText)   ServerText.text    = data.ServerName;
            if (LastPlayText) LastPlayText.text  = data.LastPlayTime;

            // 加载头像
            if (AvatarImage)
            {
                if (!string.IsNullOrEmpty(data.AvatarPath))
                {
                    var sprite = Resources.Load<Sprite>(data.AvatarPath);
                    if (sprite) AvatarImage.sprite = sprite;
                }
            }

            SetSelected(false);
        }

        // ── 选中状态 ──────────────────────────────────────────────────
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (HighlightBg) HighlightBg.color = selected ? HighlightColor : Color.clear;
            if (NormalBg)    NormalBg.color     = selected ? new Color(0.15f, 0.10f, 0.05f, 0.9f)
                                                           : NormalColor;
        }

        // ── 点击 ──────────────────────────────────────────────────────
        public void OnPointerClick(PointerEventData eventData)
        {
            OnSelected?.Invoke(this);
        }
    }
}
