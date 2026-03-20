using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace POELike.Game.UI
{
    /// <summary>
    /// 角色选择面板控制器
    /// 挂载在 CharacterSelectPanel Prefab 根节点上
    /// </summary>
    public class CharacterSelectPanel : MonoBehaviour
    {
        [Header("列表区域")]
        [SerializeField] private Transform          _scrollContent;     // ScrollView/Viewport/Content
        [SerializeField] private GameObject         _slotItemPrefab;    // CharacterSlotItem Prefab
        [SerializeField] private ScrollRect         _scrollRect;

        [Header("底部按钮")]
        [SerializeField] private Button             _btnEnterGame;
        [SerializeField] private Button             _btnCreateCharacter;
        [SerializeField] private Button             _btnDeleteCharacter;

        [Header("按钮文字（可选）")]
        [SerializeField] private TextMeshProUGUI    _btnEnterText;
        [SerializeField] private TextMeshProUGUI    _btnCreateText;
        [SerializeField] private TextMeshProUGUI    _btnDeleteText;

        [Header("提示文字")]
        [SerializeField] private TextMeshProUGUI    _hintText;          // 无存档时的提示

        // ── 运行时状态 ────────────────────────────────────────────────
        private readonly List<CharacterSlotItem> _slotItems = new();
        private CharacterSlotItem                _selectedItem;

        // ── 外部事件（供其他系统监听）────────────────────────────────
        public System.Action<CharacterSaveData> OnEnterGame;
        public System.Action                    OnCreateCharacter;
        public System.Action<CharacterSaveData> OnDeleteCharacter;

        // ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            _btnEnterGame        .onClick.AddListener(HandleEnterGame);
            _btnCreateCharacter  .onClick.AddListener(HandleCreateCharacter);
            _btnDeleteCharacter  .onClick.AddListener(HandleDeleteCharacter);
        }

        private void OnEnable()
        {
            RefreshList();
        }

        // ── 刷新列表 ──────────────────────────────────────────────────
        public void RefreshList()
        {
            // 清空旧条目
            foreach (var item in _slotItems)
                if (item) Destroy(item.gameObject);
            _slotItems.Clear();
            _selectedItem = null;

            var saves = CharacterSaveManager.LoadAllSaves();

            bool hasSaves = saves.Count > 0;
            if (_hintText) _hintText.gameObject.SetActive(!hasSaves);

            foreach (var save in saves)
            {
                var go   = Instantiate(_slotItemPrefab, _scrollContent);
                var slot = go.GetComponent<CharacterSlotItem>();
                slot.Bind(save);
                slot.OnSelected += OnSlotSelected;
                _slotItems.Add(slot);
            }

            // 默认选中第一个
            if (_slotItems.Count > 0)
                OnSlotSelected(_slotItems[0]);

            // 强制刷新 ContentSizeFitter，确保动态添加条目后 Content 高度立即更新
            if (_scrollContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent as RectTransform);

            UpdateButtonStates();
        }

        // ── 选中回调 ──────────────────────────────────────────────────
        private void OnSlotSelected(CharacterSlotItem item)
        {
            if (_selectedItem == item) return;

            _selectedItem?.SetSelected(false);
            _selectedItem = item;
            _selectedItem?.SetSelected(true);

            UpdateButtonStates();
        }

        // ── 按钮状态 ──────────────────────────────────────────────────
        private void UpdateButtonStates()
        {
            bool hasSelection = _selectedItem != null;
            _btnEnterGame      .interactable = hasSelection;
            _btnDeleteCharacter.interactable = hasSelection;
            _btnCreateCharacter.interactable = CharacterSaveManager.CanCreateNew();
        }

        // ── 按钮处理 ──────────────────────────────────────────────────
        private void HandleEnterGame()
        {
            if (_selectedItem == null) return;
            Debug.Log($"[CharacterSelectPanel] 进入游戏: {_selectedItem.Data.CharacterName}");
            OnEnterGame?.Invoke(_selectedItem.Data);
        }

        private void HandleCreateCharacter()
        {
            Debug.Log("[CharacterSelectPanel] 创建角色");
            OnCreateCharacter?.Invoke();
        }

        private void HandleDeleteCharacter()
        {
            if (_selectedItem == null) return;
            var data = _selectedItem.Data;
            Debug.Log($"[CharacterSelectPanel] 删除角色: {data.CharacterName}");

            CharacterSaveManager.DeleteCharacter(data.SaveId);
            OnDeleteCharacter?.Invoke(data);
            RefreshList();
        }

        // ── 公开接口 ──────────────────────────────────────────────────
        /// <summary>外部调用：添加一条新存档并刷新</summary>
        public void AddAndRefresh(CharacterSaveData data)
        {
            CharacterSaveManager.SaveCharacter(data);
            RefreshList();
        }

        /// <summary>获取当前选中的存档数据</summary>
        public CharacterSaveData GetSelectedData() => _selectedItem?.Data;
    }
}
