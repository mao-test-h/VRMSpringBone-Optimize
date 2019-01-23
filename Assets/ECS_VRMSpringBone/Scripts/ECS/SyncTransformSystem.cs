namespace VRM.ECS_SpringBone
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Jobs;

    using Unity.Entities;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Burst;

    [UpdateInGroup(typeof(VRMSpringBoneGroup))]
    [UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate))]
    public unsafe sealed class SyncTransformSystem : JobComponentSystem
    {
        // ------------------------------
        #region // Jobs

        [BurstCompile]
        struct SyncTransformJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<PositionPtr> PositionPtrs;
            [ReadOnly] public NativeArray<RotationPtr> RotationPtrs;
            void IJobParallelForTransform.Execute(int index, TransformAccess trsAccess)
            {
                if (this.PositionPtrs[index].Value != null)
                {
                    *this.PositionPtrs[index].Value = trsAccess.position;
                }
                if (this.RotationPtrs[index].Value != null)
                {
                    trsAccess.rotation = this.RotationPtrs[index].GetValue;
                }
            }
        }

        #endregion // Jobs

        // ------------------------------
        #region // Private Fields

        List<SyncTransformSOA> _syncTransformSOAList = new List<SyncTransformSOA>();

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Public Methods

        public void AddSyncTransformSOA(VRMSpringBone springBone)
        {
            this._syncTransformSOAList.Add(springBone.SyncTransformSOA);
        }

        public void RemoveSyncTransformSOA(VRMSpringBone springBone)
        {
            this._syncTransformSOAList.Remove(springBone.SyncTransformSOA);
        }

        #endregion // Public Methods

        // ----------------------------------------------------
        #region // Protected Methods

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var handle = inputDeps;
            var handles = new NativeArray<JobHandle>(this._syncTransformSOAList.Count, Allocator.Temp);
            for (int i = 0; i < this._syncTransformSOAList.Count; i++)
            {
                var data = this._syncTransformSOAList[i];
                handles[i] = new SyncTransformJob
                {
                    PositionPtrs = data.PositionPtrs,
                    RotationPtrs = data.RotationPtrs,
                }.Schedule(data.TransformAccessArray, handle);
            }
            handle = JobHandle.CombineDependencies(handles);
            handles.Dispose();
            return handle;
        }

        #endregion // Protected Methods
    }
}
