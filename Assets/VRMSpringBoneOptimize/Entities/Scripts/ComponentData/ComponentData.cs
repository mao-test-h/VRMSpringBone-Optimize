namespace VRM.Optimize.Entities
{
    using Unity.Entities;
    using Unity.Mathematics;

    // ----------------------------------------------------

    #region // Transforms

    // Transform.position
    public struct Position : IComponentData
    {
        public float3 Value;
    }

    // Transform.rotation
    public struct Rotation : IComponentData
    {
        public quaternion Value;
    }

    #endregion // Transforms

    // ----------------------------------------------------

    #region // VRMSpringBoneColliderGroup

    // VRMSpringBoneに所属するVRMSpringBoneColliderGroupの管理タグ
    public struct ColliderIdentifyTag : IComponentData
    {
    }

    // VRMSpringBoneColliderGroup識別用
    public struct ColliderGroupTag : IComponentData
    {
    }
    
   // Transform.rotation(ColliderGroup用の回転値)
    public struct ColliderGroupRotation : IComponentData
    {
        public quaternion Value;
    } 
    
    // SphereColliderEntityが所属するVRMSpringBoneを把握するためのEntity
    // ※EntityにはColliderIdentifyTagを持ったEntityが入る
    public struct ColliderIdentify : IComponentData
    {
        public Entity Entity;
    }
    
    // SphereColliderEntityが所属するVRMSpringBoneColliderGroupを把握するためのEntity
    // ※EntityにはColliderGroupTagを持ったEntityが入る
    public struct ColliderGroup : IComponentData
    {
        public Entity Entity;
    }
    
    public unsafe struct ColliderGroupBlittableFieldsPtr : IComponentData
    {
        public VRMSpringBoneColliderGroup.BlittableFields* Value;
        public VRMSpringBoneColliderGroup.BlittableFields GetValue => *this.Value;
    }

    #endregion // VRMSpringBoneColliderGroup

    // ----------------------------------------------------

    #region // VRMSpringBoneLogic

    public unsafe struct SpringBoneBlittableFieldsPtr : IComponentData
    {
        public VRMSpringBone.BlittableFields* Value;
        public VRMSpringBone.BlittableFields GetValue => *this.Value;
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

    public struct Parent : IComponentData
    {
        public Entity Entity;
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

    #endregion // VRMSpringBoneLogic
}
