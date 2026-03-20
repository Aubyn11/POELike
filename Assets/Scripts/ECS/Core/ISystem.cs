namespace POELike.ECS.Core
{
    /// <summary>
    /// ECS系统基础接口
    /// 所有游戏系统必须实现此接口
    /// </summary>
    public interface ISystem
    {
        /// <summary>
        /// 系统是否启用
        /// </summary>
        bool IsEnabled { get; set; }
        
        /// <summary>
        /// 系统优先级（数值越小越先执行）
        /// </summary>
        int Priority { get; }
        
        /// <summary>
        /// 系统初始化
        /// </summary>
        void Initialize(World world);
        
        /// <summary>
        /// 每帧更新
        /// </summary>
        void Update(float deltaTime);
        
        /// <summary>
        /// 固定物理更新
        /// </summary>
        void FixedUpdate(float fixedDeltaTime);
        
        /// <summary>
        /// 系统销毁
        /// </summary>
        void Dispose();
    }
}
