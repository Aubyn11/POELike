using UnityEngine;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 移动组件
    /// 控制实体的移动行为
    /// </summary>
    public class MovementComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 当前速度向量
        /// </summary>
        public Vector3 Velocity { get; set; }
        
        /// <summary>
        /// 移动方向（归一化）
        /// </summary>
        public Vector3 MoveDirection { get; set; }
        
        /// <summary>
        /// 基础移动速度
        /// </summary>
        public float BaseSpeed { get; set; } = 5f;
        
        /// <summary>
        /// 当前实际移动速度（含加成）
        /// </summary>
        public float CurrentSpeed { get; set; } = 5f;
        
        /// <summary>
        /// 是否正在移动
        /// </summary>
        public bool IsMoving => MoveDirection.sqrMagnitude > 0.01f;
        
        /// <summary>
        /// 是否被禁止移动（眩晕、冰冻等）
        /// </summary>
        public bool IsImmobilized { get; set; } = false;
        
        /// <summary>
        /// 关联的CharacterController（可选）
        /// </summary>
        public CharacterController CharacterController { get; set; }
        
        /// <summary>
        /// 关联的Rigidbody（可选）
        /// </summary>
        public Rigidbody Rigidbody { get; set; }
        
        /// <summary>
        /// 重力是否启用
        /// </summary>
        public bool UseGravity { get; set; } = true;
        
        /// <summary>
        /// 垂直速度（用于跳跃/重力）
        /// </summary>
        public float VerticalVelocity { get; set; } = 0f;
        
        public void Reset()
        {
            Velocity = Vector3.zero;
            MoveDirection = Vector3.zero;
            CurrentSpeed = BaseSpeed;
            IsImmobilized = false;
            VerticalVelocity = 0f;
        }
    }
}
