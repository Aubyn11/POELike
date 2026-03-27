using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 商店页签条目，挂载在 Tog 预制体上。
    /// 点击时通知 <see cref="ShopPanel"/> 切换到对应页签。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ShopTabItem : ListBoxItem
    {
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Image           _bg;

        // 选中/未选中颜色
        private static readonly Color ColorSelected   = new Color(0.9f, 0.7f, 0.2f, 1f);
        private static readonly Color ColorNormal      = new Color(0.3f, 0.3f, 0.3f, 0.8f);

        private Button     _btn;
        private ShopPanel  _shopPanel;
        private int        _tabIndex;

        // ── 初始化 ────────────────────────────────────────────────────

        private void Awake()
        {
            _btn = GetComponent<Button>();
            if (_label == null) _label = GetComponentInChildren<TextMeshProUGUI>();
            if (_bg    == null) _bg    = GetComponent<Image>();
        }

        /// <summary>
        /// 由 ShopPanel 调用，设置页签索引、名称和所属面板
        /// </summary>
        public void SetupTab(int tabIndex, string tabName, ShopPanel shopPanel)
        {
            _tabIndex  = tabIndex;
            _shopPanel = shopPanel;

            if (_label != null) _label.text = tabName;

            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(OnTabClicked);

            SetSelected(false);
        }

        /// <summary>设置选中状态（更新背景色）</summary>
        public void SetSelected(bool selected)
        {
            if (_bg != null)
                _bg.color = selected ? ColorSelected : ColorNormal;
        }

        // ── 事件 ──────────────────────────────────────────────────────

        private void OnTabClicked()
        {
            _shopPanel?.OnTabSelected(_tabIndex);
        }

        // ── ListBoxItem 生命周期 ──────────────────────────────────────

        public override void OnItemInit()  { }
        public override void OnItemShow()  { }
        public override void OnItemHide()  { }
    }
}
