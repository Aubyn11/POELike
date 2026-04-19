using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 玩家输入组件
    /// 存储当前帧的输入状态
    /// </summary>
    public class PlayerInputComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 移动输入（WASD / 摇杆）
        /// </summary>
        public UnityEngine.Vector2 MoveInput { get; set; }
        
        /// <summary>
        /// 点击寻路目标点（世界坐标）
        /// </summary>
        public UnityEngine.Vector3 ClickTargetPosition { get; set; }
        
        /// <summary>
        /// 是否有新的点击寻路目标
        /// </summary>
        public bool HasClickTarget { get; set; } = false;
        
        /// <summary>
        /// 鼠标世界坐标（用于朝向和技能目标）
        /// </summary>
        public UnityEngine.Vector3 MouseWorldPosition { get; set; }
        
        /// <summary>
        /// 鼠标屏幕坐标
        /// </summary>
        public UnityEngine.Vector2 MouseScreenPosition { get; set; }
        
        /// <summary>
        /// 技能槽位按下输入（0-7 对应技能 1-8）
        /// </summary>
        public bool[] SkillInputs { get; } = new bool[8];

        /// <summary>
        /// 技能槽位按住状态（0-7 对应技能 1-8）
        /// </summary>
        public bool[] SkillHeldInputs { get; } = new bool[8];

        /// <summary>
        /// 技能槽位松开输入（0-7 对应技能 1-8）
        /// </summary>
        public bool[] SkillReleasedInputs { get; } = new bool[8];
        
        /// <summary>
        /// 药剂输入（0-4对应药剂1-5）
        /// </summary>
        public bool[] FlaskInputs { get; } = new bool[5];

        
        /// <summary>
        /// 交互键
        /// </summary>
        public bool InteractPressed { get; set; }
        
        /// <summary>
        /// 跑步键（Shift）
        /// </summary>
        public bool SprintHeld { get; set; }
        
        /// <summary>
        /// 鼠标左键（普通攻击/移动）
        /// </summary>
        public bool MouseLeftHeld { get; set; }
        
        /// <summary>
        /// 鼠标右键（技能）
        /// </summary>
        public bool MouseRightHeld { get; set; }
        
        public void Reset()
        {
            MoveInput = UnityEngine.Vector2.zero;
            InteractPressed = false;
            SprintHeld = false;
            MouseLeftHeld = false;
            MouseRightHeld = false;
            HasClickTarget = false;
            for (int i = 0; i < SkillInputs.Length; i++)
            {
                SkillInputs[i] = false;
                SkillHeldInputs[i] = false;
                SkillReleasedInputs[i] = false;
            }
            for (int i = 0; i < FlaskInputs.Length; i++) FlaskInputs[i] = false;
        }

    }
}
