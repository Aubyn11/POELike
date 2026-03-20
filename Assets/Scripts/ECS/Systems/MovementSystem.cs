using System.Collections.Generic;
using UnityEngine;
using POELike.ECS.Core;
using POELike.ECS.Components;

namespace POELike.ECS.Systems
{
    /// <summary>
    /// 移动系统
    /// 处理所有实体的移动逻辑
    /// 优先级：100（较早执行）
    /// </summary>
    public class MovementSystem : SystemBase
    {
        public override int Priority => 100;
        
        private const float Gravity = -9.81f;
        
        protected override void OnUpdate(float deltaTime)
        {
            var entities = World.Query<TransformComponent, MovementComponent>();
            
            foreach (var entity in entities)
            {
                var transform = entity.GetComponent<TransformComponent>();
                var movement = entity.GetComponent<MovementComponent>();
                
                if (!movement.IsEnabled || movement.IsImmobilized) continue;
                
                // 处理CharacterController移动
                if (movement.CharacterController != null)
                {
                    UpdateCharacterControllerMovement(movement, deltaTime);
                }
                // 处理Rigidbody移动
                else if (movement.Rigidbody != null)
                {
                    UpdateRigidbodyMovement(movement, deltaTime);
                }
                // 纯逻辑移动
                else
                {
                    UpdateLogicMovement(transform, movement, deltaTime);
                }
                
                // 更新朝向（面向移动方向）
                if (movement.MoveDirection.sqrMagnitude > 0.01f && transform.UnityTransform != null)
                {
                    var lookDir = new Vector3(movement.MoveDirection.x, 0, movement.MoveDirection.z);
                    if (lookDir.sqrMagnitude > 0.01f)
                    {
                        transform.UnityTransform.rotation = Quaternion.Slerp(
                            transform.UnityTransform.rotation,
                            Quaternion.LookRotation(lookDir),
                            deltaTime * 15f
                        );
                    }
                }
            }
        }
        
        private void UpdateCharacterControllerMovement(MovementComponent movement, float deltaTime)
        {
            var cc = movement.CharacterController;
            
            // 重力
            if (movement.UseGravity)
            {
                if (cc.isGrounded)
                    movement.VerticalVelocity = -2f; // 保持贴地
                else
                    movement.VerticalVelocity += Gravity * deltaTime;
            }
            
            // 计算移动向量
            var moveVelocity = movement.MoveDirection * movement.CurrentSpeed;
            moveVelocity.y = movement.VerticalVelocity;
            
            cc.Move(moveVelocity * deltaTime);
        }
        
        private void UpdateRigidbodyMovement(MovementComponent movement, float deltaTime)
        {
            var rb = movement.Rigidbody;
            var targetVelocity = movement.MoveDirection * movement.CurrentSpeed;
            targetVelocity.y = rb.linearVelocity.y; // 保留垂直速度
            rb.linearVelocity = targetVelocity;
        }
        
        private void UpdateLogicMovement(TransformComponent transform, MovementComponent movement, float deltaTime)
        {
            transform.Position += movement.MoveDirection * movement.CurrentSpeed * deltaTime;
        }
    }
}
