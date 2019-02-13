namespace VRM.Optimize.Entities
{
    using UnityEngine.Assertions;
    using Unity.Entities;
    using Unity.Mathematics;

    // ----------------------------------------------------

    #region // VRMSpringBoneColliderGroup

    // SphereCollider計測用のタグ
    // ※NativeMultiHashMap生成時の長さの算出に使用する
    public struct SphereColliderTag : IComponentData
    {
        public byte Dummy;
    }

    public struct ColliderGroupInstanceID : IComponentData
    {
        public int Value;
    }

    public unsafe struct ColliderGroupBlittableFieldsPtr : IComponentData
    {
        public VRMSpringBoneColliderGroup.BlittableFields* Value;
        public int Length;

        public VRMSpringBoneColliderGroup.BlittableFields GetBlittableFields(int index)
        {
            Assert.IsTrue((index >= 0) && (index < this.Length));
            return *(this.Value + index);
        }
    }

    #endregion // VRMSpringBoneColliderGroup

    // ----------------------------------------------------

    #region // VRMSpringBoneLogic

    public unsafe struct SpringBoneBlittableFieldsPtr : IComponentData
    {
        public VRMSpringBone.BlittableFields* Value;
        public VRMSpringBone.BlittableFields GetValue => *this.Value;
    }

    // 親のEntity
    public struct ParentEntity : IComponentData
    {
        public Entity Entity;
    }

    // transform.rotationの保持用
    public struct Rotation : IComponentData
    {
        public quaternion Value;
    }

    // VRMSpringBoneLogic.m_length
    public struct Length : IComponentData
    {
        public float Value;
    }

    // VRMSpringBoneLogic.m_localRotation
    public struct LocalRotation : IComponentData
    {
        public quaternion Value;
    }

    // VRMSpringBoneLogic.m_boneAxis
    public struct BoneAxis : IComponentData
    {
        public float3 Value;
    }

    // VRMSpringBoneLogic.m_currentTail
    public struct CurrentTail : IComponentData
    {
        public float3 Value;
    }

    // VRMSpringBoneLogic.m_prevTail
    public struct PrevTail : IComponentData
    {
        public float3 Value;
    }

    // VRMSpringBoneLogic.m_centerとなるGameObjectEntity
    public struct CenterEntity : IComponentData
    {
        public Entity Entity;
    }
    
    // VRMSpringBoneLogic.m_center
    public struct Center : IComponentData
    {
        public float4x4 Value;
    }

    #endregion // VRMSpringBoneLogic
}
