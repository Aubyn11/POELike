using POELike.ECS.Core;

namespace POELike.ECS.Core
{
    /// <summary>
    /// 系统基类
    /// 提供默认实现，子类只需重写需要的方法
    /// </summary>
    public abstract class SystemBase : ISystem
    {
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 系统优先级，子类可重写
        /// </summary>
        public virtual int Priority => 0;
        
        /// <summary>
        /// 世界引用
        /// </summary>
        protected World World { get; private set; }
        
        public virtual void Initialize(World world)
        {
            World = world;
            OnInitialize();
        }
        
        public virtual void Update(float deltaTime)
        {
            OnUpdate(deltaTime);
        }
        
        public virtual void FixedUpdate(float fixedDeltaTime)
        {
            OnFixedUpdate(fixedDeltaTime);
        }
        
        public virtual void Dispose()
        {
            OnDispose();
        }
        
        // 子类重写的钩子方法
        protected virtual void OnInitialize() { }
        protected virtual void OnUpdate(float deltaTime) { }
        protected virtual void OnFixedUpdate(float fixedDeltaTime) { }
        protected virtual void OnDispose() { }
    }
}
