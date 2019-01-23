namespace VRM.ECS_SpringBone
{
    using System.Collections.Generic;
    using System.Linq;
    using UnityEngine;
    using UnityEngine.Jobs;

    using Unity.Mathematics;
    using Unity.Collections;

    // ----------------------------------------------------

    public struct VRMSpringBoneParam
    {
        public float StiffnessForce;
        public float GravityPower;
        public float3 GraviryDir;
        public float DragForce;
        public float HitRadius;
    }

    public struct SphereColliderParam
    {
        public float3 Offset;
        public float Radius;
    }

    // ----------------------------------------------------
    // SyncTransform

    // UpdateInGroup
    public sealed class VRMSpringBoneGroup { }

    public unsafe class SyncTransformSOA : System.IDisposable
    {
        public TransformAccessArray TransformAccessArray;
        public NativeArray<PositionPtr> PositionPtrs;
        public NativeArray<RotationPtr> RotationPtrs;

        public SyncTransformSOA(Transform[] transforms, PositionPtr[] positionPtrs, RotationPtr[] rotationPtrs)
        {
            this.TransformAccessArray = new TransformAccessArray(transforms);
            this.PositionPtrs = new NativeArray<PositionPtr>(positionPtrs, Allocator.Persistent);
            this.RotationPtrs = new NativeArray<RotationPtr>(rotationPtrs, Allocator.Persistent);
        }

        public void Dispose()
        {
            this.TransformAccessArray.Dispose();
            this.PositionPtrs.Dispose();
            this.RotationPtrs.Dispose();
        }
    }
}
