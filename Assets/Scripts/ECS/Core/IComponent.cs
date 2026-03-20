namespace POELike.ECS.Core
{
    /// <summary>
    /// ECS组件基础接口
    /// 所有游戏组件必须实现此接口
    /// </summary>
    public interface IComponent
    {
        /// <summary>
        /// 组件是否启用
        /// </summary>
        bool IsEnabled { get; set; }
        
        /// <summary>
        /// 重置组件到初始状态
        /// </summary>
        void Reset();
    }
}
