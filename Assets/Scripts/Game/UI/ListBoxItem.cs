using UnityEngine;

namespace POELike.Game.UI
{
    /// <summary>
    /// ListBox 子条目基类，挂载在子物体预制体根节点上。
    /// 继承此类并重写生命周期方法来实现具体逻辑。
    /// </summary>
    public class ListBoxItem : MonoBehaviour
    {
        // ── 内部数据 ──────────────────────────────────────────────────
        private int  _index;
        private int  _type;

        // ── 公开接口 ──────────────────────────────────────────────────

        /// <summary>返回该条目在 ListBox 中的创建索引（从 0 开始）</summary>
        public int GetIndex() => _index;

        /// <summary>返回该条目挂载的游戏物体</summary>
        public GameObject GetCtrl() => gameObject;

        // ── 内部初始化（由 ListBox 调用）─────────────────────────────

        internal void Setup(int index, int type)
        {
            _index = index;
            _type  = type;
        }

        // ── 生命周期回调（子类重写）───────────────────────────────────

        /// <summary>条目被创建并完成基础设置后调用（仅调用一次）</summary>
        public virtual void OnItemInit() { }

        /// <summary>条目被激活显示时调用</summary>
        public virtual void OnItemShow() { }

        /// <summary>条目被隐藏时调用</summary>
        public virtual void OnItemHide() { }

        /// <summary>条目被销毁前调用</summary>
        public virtual void OnItemDestroy() { }

        private void OnDestroy()
        {
            OnItemDestroy();
        }
    }
}
