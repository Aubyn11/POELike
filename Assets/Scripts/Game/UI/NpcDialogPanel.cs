using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace POELike.Game.UI
{
    /// <summary>
    /// NPC对话框中的按钮选项
    /// </summary>
    [Serializable]
    public class NpcDialogOption
    {
        public string Label;
        public Action OnClick;

        public NpcDialogOption(string label, Action onClick)
        {
            Label   = label;
            OnClick = onClick;
        }
    }

    /// <summary>
    /// NPC对话框面板（UGUI 实现），继承自 <see cref="UIGamePanel"/>。
    /// 左侧显示 NPC 名称与对话内容，右侧通过 <see cref="ListBox"/> 动态创建选项按钮。
    /// 预制体结构：
    ///   ChatPanel
    ///   ├── NpcNameText       (TextMeshProUGUI)
    ///   ├── DialogText        (TextMeshProUGUI)
    ///   └── OptionListBox     (ListBox，_itemPrefabs[0] = DialogOptionItem 预制体)
    /// </summary>
    public class NpcDialogPanel : UIGamePanel
    {
        // ── UI 引用 ───────────────────────────────────────────────────
        [Header("UI 引用")]
        [SerializeField] private TextMeshProUGUI _npcNameText;
        [SerializeField] private TextMeshProUGUI _dialogText;
        [SerializeField] private ListBox         _optionListBox;

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 打开对话框，刷新文本并通过 ListBox 创建选项按钮。
        /// </summary>
        public void Open(string npcName, string dialogText, List<NpcDialogOption> options)
        {
            if (_npcNameText != null) _npcNameText.text = npcName    ?? "";
            if (_dialogText  != null) _dialogText.text  = dialogText ?? "";

            // 清空旧按钮，重新创建
            if (_optionListBox == null)
            {
                Debug.LogError("[NpcDialogPanel] _optionListBox 未赋值，请在 ChatPanel 预制体 Inspector 中绑定 ListBox 组件。");
            }
            else
            {
                _optionListBox.Clear();

                if (options != null && options.Count > 0)
                {
                    for (int i = 0; i < options.Count; i++)
                    {
                        _optionListBox.AddItem(0, 1);

                        var item = _optionListBox.GetItemByIndex(i) as DialogOptionItem;
                        if (item != null)
                        {
                            item.Bind(options[i]);
                            item.OnItemInit();
                        }
                    }
                }
            }

            // 调用基类 Open，统一处理 IsOpen、注册到 UIGamePanelManager、激活 GameObject
            base.Open();
        }

        // ── 基类钩子 ──────────────────────────────────────────────────

        protected override void OnClose_Internal()
        {
            _optionListBox?.Clear();
        }
    }
}