using System.Collections.Generic;
using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>条目排列方向</summary>
    public enum ArrangeDirection
    {
        /// <summary>从上到下（垂直列表）</summary>
        TopToBottom,
        /// <summary>从下到上</summary>
        BottomToTop,
        /// <summary>从左到右（水平列表）</summary>
        LeftToRight,
        /// <summary>从右到左</summary>
        RightToLeft,
    }

    /// <summary>
    /// ListBox —— 动态列表容器，挂载在父物体上。
    /// 通过 <see cref="AddItem"/> 按类型批量创建子条目，
    /// 子条目预制体需挂载继承自 <see cref="ListBoxItem"/> 的组件。
    /// </summary>
    public class ListBox : MonoBehaviour
    {
        [Header("子条目预制体数组（下标即类型 type）")]
        [SerializeField] private GameObject[] _itemPrefabs;

        [Header("排列方向")]
        [SerializeField] private ArrangeDirection _direction = ArrangeDirection.TopToBottom;

        [Header("条目间隔 (Left / Right / Bottom / Top)")]
        [SerializeField] private float _spacingLeft   = 0f;
        [SerializeField] private float _spacingRight  = 0f;
        [SerializeField] private float _spacingBottom = 0f;
        [SerializeField] private float _spacingTop    = 0f;

        [Header("整体 Padding (Left / Right / Bottom / Top)")]
        [SerializeField] private float _paddingLeft   = 0f;
        [SerializeField] private float _paddingRight  = 0f;
        [SerializeField] private float _paddingBottom = 0f;
        [SerializeField] private float _paddingTop    = 0f;

        // ── 内部状态 ──────────────────────────────────────────────────
        private readonly List<ListBoxItem> _items = new List<ListBoxItem>();
        private int _nextIndex = 0;

        // ── 公开属性（运行时动态修改后调用 RefreshLayout）────────────

        public ArrangeDirection Direction
        {
            get => _direction;
            set { _direction = value; RefreshLayout(); }
        }

        /// <summary>条目间隔（Left/Right/Bottom/Top）</summary>
        public void SetSpacing(float left, float right, float bottom, float top)
        {
            _spacingLeft   = left;
            _spacingRight  = right;
            _spacingBottom = bottom;
            _spacingTop    = top;
            RefreshLayout();
        }

        /// <summary>整体 Padding（Left/Right/Bottom/Top）</summary>
        public void SetPadding(float left, float right, float bottom, float top)
        {
            _paddingLeft   = left;
            _paddingRight  = right;
            _paddingBottom = bottom;
            _paddingTop    = top;
            RefreshLayout();
        }

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>
        /// 按类型和数量批量创建子条目，挂载在本物体下。
        /// </summary>
        /// <param name="type">预制体类型，对应 _itemPrefabs 数组下标</param>
        /// <param name="count">创建数量</param>
        public void AddItem(int type, int count)
        {
            if (_itemPrefabs == null || type < 0 || type >= _itemPrefabs.Length)
            {
                Debug.LogWarning($"[ListBox] 类型 {type} 超出预制体数组范围，请检查 _itemPrefabs 配置。");
                return;
            }

            var prefab = _itemPrefabs[type];
            if (prefab == null)
            {
                Debug.LogWarning($"[ListBox] 类型 {type} 对应的预制体为空。");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var go   = Instantiate(prefab, transform);
                var item = go.GetComponent<ListBoxItem>();

                if (item == null)
                {
                    // 预制体上没有 ListBoxItem 组件时自动添加，保证能被 ListBox 管理
                    item = go.AddComponent<ListBoxItem>();
                }

                item.Setup(_nextIndex++, type);
                _items.Add(item);

                // 触发初始化回调
                item.OnItemInit();
                // 默认激活显示
                item.OnItemShow();
            }

            RefreshLayout();
        }

        /// <summary>隐藏指定索引的条目（不销毁）</summary>
        public void HideItem(int index)
        {
            var item = GetItemByIndex(index);
            if (item == null) return;
            item.gameObject.SetActive(false);
            item.OnItemHide();
            RefreshLayout();
        }

        /// <summary>显示指定索引的条目</summary>
        public void ShowItem(int index)
        {
            var item = GetItemByIndex(index);
            if (item == null) return;
            item.gameObject.SetActive(true);
            item.OnItemShow();
            RefreshLayout();
        }

        /// <summary>销毁指定索引的条目</summary>
        public void RemoveItem(int index)
        {
            var item = GetItemByIndex(index);
            if (item == null) return;
            _items.Remove(item);
            Destroy(item.gameObject);
            RefreshLayout();
        }

        /// <summary>清空所有条目</summary>
        public void Clear()
        {
            foreach (var item in _items)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _items.Clear();
            _nextIndex = 0;
        }

        /// <summary>获取当前条目总数</summary>
        public int Count => _items.Count;

        /// <summary>通过索引获取 ListBoxItem</summary>
        public ListBoxItem GetItemByIndex(int index)
        {
            return _items.Find(item => item != null && item.GetIndex() == index);
        }

        // ── 布局 ──────────────────────────────────────────────────────

        /// <summary>
        /// 根据排列方向、Spacing 和 Padding 重新计算所有可见条目的位置。
        /// 仅处理激活状态的条目，隐藏条目不参与布局计算。
        /// </summary>
        public void RefreshLayout()
        {
            bool isVertical   = _direction == ArrangeDirection.TopToBottom
                             || _direction == ArrangeDirection.BottomToTop;
            bool isReverse    = _direction == ArrangeDirection.BottomToTop
                             || _direction == ArrangeDirection.RightToLeft;

            // 起始偏移（Padding 起始边）
            float cursor = isVertical
                ? (isReverse ? _paddingBottom : _paddingTop)
                : (isReverse ? _paddingRight  : _paddingLeft);

            // 收集激活的条目（按 _items 顺序）
            var visibleItems = new List<ListBoxItem>();
            foreach (var item in _items)
            {
                if (item != null && item.gameObject.activeSelf)
                    visibleItems.Add(item);
            }

            if (isReverse)
                visibleItems.Reverse();

            for (int i = 0; i < visibleItems.Count; i++)
            {
                var rt   = visibleItems[i].GetCtrl().GetComponent<RectTransform>();
                if (rt == null) continue;

                // 间隔前缀（非首个条目）
                if (i > 0)
                    cursor += isVertical ? _spacingTop : _spacingLeft;

                // 设置锚点为左上角（垂直）或左上角（水平），便于绝对定位
                if (isVertical)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot     = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -cursor);
                }
                else
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot     = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(cursor, 0f);
                }

                // 推进 cursor：条目尺寸 + 间隔后缀
                float itemSize = isVertical ? rt.rect.height : rt.rect.width;
                cursor += itemSize + (isVertical ? _spacingBottom : _spacingRight);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Inspector 中修改参数时实时预览布局
            if (Application.isPlaying)
                RefreshLayout();
        }
#endif
    }
}
