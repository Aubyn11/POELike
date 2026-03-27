using POELike.Game.Equipment;
using UnityEngine;
using UnityEngine.UI;

namespace POELike.Game.UI
{
    /// <summary>
    /// 插槽格子视图，挂载在 Socket.prefab 根节点上。
    /// 由 <see cref="EquipmentItem"/> 通过 ListBox 动态创建，
    /// 调用 <see cref="SetSocket"/> 设置颜色。
    /// </summary>
    public class SocketItem : ListBoxItem
    {
        [SerializeField] private Image _icon; // 插槽圆形图标

        // 各颜色对应的显示色
        private static readonly Color ColorRed   = new Color(0.90f, 0.25f, 0.20f);
        private static readonly Color ColorGreen = new Color(0.20f, 0.80f, 0.30f);
        private static readonly Color ColorBlue  = new Color(0.25f, 0.50f, 1.00f);
        private static readonly Color ColorWhite = new Color(0.90f, 0.90f, 0.90f);

        private void Awake()
        {
            _icon ??= GetComponent<Image>();
        }

        /// <summary>
        /// 设置插槽颜色
        /// </summary>
        public void SetSocket(SocketColor socketColor)
        {
            if (_icon == null) return;
            _icon.color = socketColor switch
            {
                SocketColor.Red   => ColorRed,
                SocketColor.Green => ColorGreen,
                SocketColor.Blue  => ColorBlue,
                _                 => ColorWhite,
            };
        }
    }
}
