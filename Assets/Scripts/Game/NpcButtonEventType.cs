namespace POELike.Game
{
    /// <summary>
    /// NPC 对话按钮事件类型枚举
    /// 对应 ButtonEventDataConf.pb 中的 EventID
    /// </summary>
    public enum NpcButtonEventType
    {
        /// <summary>未知事件</summary>
        None = 0,

        /// <summary>再见 - 关闭对话框（EventID=1001）</summary>
        CloseDialog = 1001,

        /// <summary>强化装备（EventID=1002）</summary>
        EnhanceEquipment = 1002,

        /// <summary>打开商店（EventID=1003）</summary>
        OpenShop = 1003,
    }
}
