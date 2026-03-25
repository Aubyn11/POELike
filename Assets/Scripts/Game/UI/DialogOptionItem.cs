using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace POELike.Game.UI
{
    /// <summary>
    /// 对话选项按钮条目，挂载在 DialogOptionItem 预制体根节点上。
    /// 由 NpcDialogPanel 通过 ListBox.AddItem(0, count) 批量创建。
    /// </summary>
    public class DialogOptionItem : ListBoxItem
    {
        [Header("UI 引用")]
        [SerializeField] private Button           _button;
        [SerializeField] private TextMeshProUGUI  _label;

        // 由外部在 OnItemInit 之前通过 Bind 注入
        private NpcDialogOption _option;

        /// <summary>绑定选项数据，必须在 ListBox.AddItem 之前调用</summary>
        public void Bind(NpcDialogOption option)
        {
            _option = option;
        }

        public override void OnItemInit()
        {
            // _option 尚未 Bind 时跳过，等 Bind 后由外部再次调用
            if (_option == null) return;

            if (_label  != null) _label.text = _option.Label;
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => _option.OnClick?.Invoke());
            }
        }

        public override void OnItemShow()
        {
            gameObject.SetActive(true);
        }

        public override void OnItemHide()
        {
            gameObject.SetActive(false);
        }
    }
}
