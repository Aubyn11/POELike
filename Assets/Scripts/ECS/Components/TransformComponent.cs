using UnityEngine;
using POELike.ECS.Core;

namespace POELike.ECS.Components
{
    /// <summary>
    /// 变换组件
    /// 存储实体的位置、旋转、缩放信息
    /// </summary>
    public class TransformComponent : IComponent
    {
        public bool IsEnabled { get; set; } = true;
        
        /// <summary>
        /// 关联的Unity Transform（可为null，用于纯逻辑实体）
        /// </summary>
        public Transform UnityTransform { get; set; }
        
        /// <summary>
        /// 世界坐标位置
        /// </summary>
        public Vector3 Position
        {
            get => UnityTransform != null ? UnityTransform.position : _position;
            set
            {
                _position = value;
                if (UnityTransform != null) UnityTransform.position = value;
            }
        }
        private Vector3 _position;
        
        /// <summary>
        /// 旋转
        /// </summary>
        public Quaternion Rotation
        {
            get => UnityTransform != null ? UnityTransform.rotation : _rotation;
            set
            {
                _rotation = value;
                if (UnityTransform != null) UnityTransform.rotation = value;
            }
        }
        private Quaternion _rotation = Quaternion.identity;
        
        /// <summary>
        /// 缩放
        /// </summary>
        public Vector3 Scale
        {
            get => UnityTransform != null ? UnityTransform.localScale : _scale;
            set
            {
                _scale = value;
                if (UnityTransform != null) UnityTransform.localScale = value;
            }
        }
        private Vector3 _scale = Vector3.one;
        
        /// <summary>
        /// 朝向（前方向量）
        /// </summary>
        public Vector3 Forward => UnityTransform != null ? UnityTransform.forward : Rotation * Vector3.forward;
        
        public void Reset()
        {
            _position = Vector3.zero;
            _rotation = Quaternion.identity;
            _scale = Vector3.one;
        }
    }
}
