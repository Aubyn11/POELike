#ifndef MONSTER_INDIRECT_COMMON_INCLUDED
#define MONSTER_INDIRECT_COMMON_INCLUDED

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
// 所有批次共享同一个大 buffer，_BatchOffset 指定本批次的起始索引
StructuredBuffer<float4x4> _MatrixBuffer;
int _BatchOffset;
#endif

// setup 回调：GPU 在每个实例 vert 之前调用，设置 ObjectToWorld 矩阵
// 对当前怪物模型链路而言，根矩阵只包含平移和欧拉旋转，缩放为统一标量；
// 因此直接使用 ObjectToWorld 的转置作为 WorldToObject 近似即可，
// 避免每个顶点都做一次昂贵的 4x4 矩阵求逆。
void setup()
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    float4x4 mat        = _MatrixBuffer[_BatchOffset + unity_InstanceID];
    unity_ObjectToWorld = mat;
    unity_WorldToObject = transpose(mat);
#endif
}

#endif // MONSTER_INDIRECT_COMMON_INCLUDED
