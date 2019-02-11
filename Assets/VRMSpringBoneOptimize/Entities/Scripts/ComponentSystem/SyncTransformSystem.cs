namespace VRM.Optimize.Entities
{
    using UnityEngine;
    using UnityEngine.Jobs;
    using Unity.Entities;
    using Unity.Collections;
    using Unity.Jobs;
    using Unity.Burst;

    [UpdateInGroup(typeof(SpringBoneUpdateInGroup))]
    [UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate))]
    public sealed class SyncTransformSystem : JobComponentSystem
    {
        // ------------------------------

        #region // Jobs

        [BurstCompile]
        struct SyncTransformJob : IJobParallelForTransform
        {
            [ReadOnly] public EntityArray Entities;
            [ReadOnly] public ComponentDataFromEntity<Rotation> Rotations;
            [NativeDisableParallelForRestriction] public ComponentDataFromEntity<Position> Positions;
            [NativeDisableParallelForRestriction] public ComponentDataFromEntity<ColliderGroupRotation> ColliderGroupRotation;

            void IJobParallelForTransform.Execute(int index, TransformAccess trsAccess)
            {
                var entity = this.Entities[index];
                this.Positions[entity] = new Position {Value = trsAccess.position};
                if (this.Rotations.Exists(entity))
                {
                    trsAccess.rotation = this.Rotations[entity].Value;
                }
                else if (ColliderGroupRotation.Exists(entity))
                {
                    this.ColliderGroupRotation[entity] = new ColliderGroupRotation {Value = trsAccess.rotation};
                }
            }
        }

        #endregion // Jobs

        // ------------------------------

        #region // Private Fields

        ComponentGroup _group;

        #endregion // Private Fields


        // ----------------------------------------------------

        #region // Protected Methods

        protected override void OnCreateManager()
        {
            this._group = base.GetComponentGroup(
                typeof(Transform)
            );
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            var transforms = this._group.GetTransformAccessArray();
            var entities = this._group.GetEntityArray();
            var handle = inputDeps;
            handle = new SyncTransformJob
            {
                Entities = entities,
                Positions = base.GetComponentDataFromEntity<Position>(),
                Rotations = base.GetComponentDataFromEntity<Rotation>(true),
                ColliderGroupRotation = base.GetComponentDataFromEntity<ColliderGroupRotation>(),
            }.Schedule(transforms, handle);
            return handle;
        }

        #endregion // Protected Methods
    }
}
