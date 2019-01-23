namespace VRM.ECS_SpringBone
{
    using Unity.Entities;
    using Unity.Mathematics;

    // ----------------------------------------------------
    #region // Transforms

    // Transform.position
    public unsafe struct PositionPtr : IComponentData
    {
        public float3* Value;
        public float3 GetValue { get { return *this.Value; } }
    }

    // Transform.rotation
    public unsafe struct RotationPtr : IComponentData
    {
        public quaternion* Value;
        public quaternion GetValue { get { return *this.Value; } }
    }

    #endregion // Transforms

    // ----------------------------------------------------
    #region // VRMSpringBoneColliderGroup

    // VRMSpringBoneに所属するVRMSpringBoneColliderGroupの管理タグ
    public unsafe struct ColliderIdentifyTag : IComponentData { }

    // ColliderEntityが所属するVRMSpringBoneを把握するためのEntity
    // ※EntityにはColliderIdentifyTagを持ったEntityが入る
    public unsafe struct ColliderIdentify : IComponentData
    {
        public Entity Entity;
    }

    // VRMSpringBoneColliderGroup.transform.position
    public unsafe struct ColliderGroupPositionPtr : IComponentData
    {
        public float3* Value;
        public float3 GetValue { get { return *this.Value; } }
    }

    public unsafe struct SphereColliderParamPtr : IComponentData
    {
        public SphereColliderParam* Value;
        public SphereColliderParam GetValue { get { return *this.Value; } }
    }

    #endregion // VRMSpringBoneColliderGroup

    // ----------------------------------------------------
    #region // VRMSpringBoneLogic

    public unsafe struct SpringBoneParamPtr : IComponentData
    {
        public VRMSpringBoneParam* Value;
        public VRMSpringBoneParam GetValue { get { return *this.Value; } }
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

    // Transform.parent.rotation
    public unsafe struct ParentRotationPtr : IComponentData
    {
        public quaternion* Value;
        public quaternion GetValue { get { return *this.Value; } }
    }

    // VRMSpringBoneLogic.m_currentTail
    // ※PositionPtrにm_currentTailに該当するポインタを持たせる場合もあるのでポインタとしている
    public unsafe struct CurrentTailPtr : IComponentData
    {
        public float3* Value;
        public float3 GetValue { get { return *this.Value; } }
    }

    // VRMSpringBoneLogic.m_prevTail
    public struct PrevTail : IComponentData
    {
        public float3 Value;
    }

    #endregion // VRMSpringBoneLogic
}
