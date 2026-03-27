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

        // 零 GC Query 缓冲
        private readonly System.Collections.Generic.List<Entity> _queryBuffer
            = new System.Collections.Generic.List<Entity>(4096);

        protected override void OnUpdate(float deltaTime)
        {
            World.Query<TransformComponent, MovementComponent>(_queryBuffer);

            foreach (var entity in _queryBuffer)
            {
                var transform = entity.GetComponent<TransformComponent>();
                var movement = entity.GetComponent<MovementComponent>();
                
                if (!movement.IsEnabled || movement.IsImmobilized) continue;
                
                // 点击寻路：根据目标点更新移动方向（对所有移动模式生效）
                if (movement.HasTarget)
                {
                    // 优先使用 UnityTransform 的实际位置，纯逻辑模式则用 TransformComponent.Position
                    var currentPos = transform.UnityTransform != null
                        ? transform.UnityTransform.position
                        : transform.Position;
                    var targetFlat = new Vector3(movement.TargetPosition.x, currentPos.y, movement.TargetPosition.z);
                    var toTarget   = targetFlat - currentPos;
                    float dist     = toTarget.magnitude;

                    if (dist <= movement.ArrivalDistance)
                    {
                        // 到达目标，停止
                        movement.HasTarget     = false;
                        movement.MoveDirection = Vector3.zero;
                    }
                    else
                    {
                        // 朝目标点方向移动（转向与移动同时进行）
                        movement.MoveDirection = toTarget.normalized;
                    }
                }
                
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
            // MoveDirection 已由外层统一处理（含HasTarget寻路），直接按方向移动
            if (movement.MoveDirection.sqrMagnitude > 0.01f)
            {
                float step = movement.CurrentSpeed * deltaTime;
                // 有目标点时限制步长，防止越过目标
                if (movement.HasTarget)
                {
                    var targetFlat = new Vector3(movement.TargetPosition.x, transform.Position.y, movement.TargetPosition.z);
                    float dist = (targetFlat - transform.Position).magnitude;
                    step = Mathf.Min(step, dist);
                }
                transform.Position += movement.MoveDirection * step;
            }
        }
    }
}
