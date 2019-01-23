namespace VRM.ECS_SpringBone
{
    using UnityEngine;

    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Burst;

    [UpdateInGroup(typeof(VRMSpringBoneGroup))]
    [UpdateAfter(typeof(SyncTransformSystem))]
    public unsafe sealed class SpringBoneSystem : JobComponentSystem
    {
        // ------------------------------
        #region // Defines

        // NativeMultiHashmap登録用
        public struct SphereCollider
        {
            public float3 Position;
            public float Radius;
        }

        const int DefaultInnerloopBatchCount = 16;

        #endregion // Defines

        // ------------------------------
        #region // Jobs

        [BurstCompile]
        struct SetColliderHashJob : IJobParallelFor
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ColliderIdentify> ColliderIdentifies;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ColliderGroupPositionPtr> ColliderGroupPositionPtrs;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SphereColliderParamPtr> SphereColliderParamPtrs;
            public NativeMultiHashMap<Entity, SphereCollider>.Concurrent ColliderHashmap;

            public void Execute(int index)
            {
                float3 pos = this.ColliderGroupPositionPtrs[index].GetValue;
                var param = this.SphereColliderParamPtrs[index].GetValue;
                var collider = new SphereCollider
                {
                    Position = pos + param.Offset,
                    Radius = param.Radius,
                };
                this.ColliderHashmap.Add(this.ColliderIdentifies[index].Entity, collider);
            }
        }

        /// <summary>
        /// The base algorithm is http://rocketjump.skr.jp/unity3d/109/ of @ricopin416
        /// </summary>
        [BurstCompile]
        public struct LogicJob : IJobProcessComponentDataWithEntity<PrevTail>
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<PositionPtr> PositionPtrs;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<RotationPtr> RotationPtrs;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ParentRotationPtr> ParentRotationPtrs;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ColliderIdentify> ColliderIdentifys;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<CurrentTailPtr> CurrentTailPtrs;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SpringBoneParamPtr> SpringBoneParamPtrs;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Length> Lengths;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<LocalRotation> LocalRotations;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<BoneAxis> BoneAxes;
            [ReadOnly] public NativeMultiHashMap<Entity, SphereCollider> ColliderHashmap;
            [ReadOnly] public float DeltaTime;

            public void Execute(Entity entity, int index, ref PrevTail prevTailRef)
            {
                VRMSpringBoneParam param = this.SpringBoneParamPtrs[index].GetValue;
                float vecLength = this.Lengths[index].Value;
                quaternion localRotation = this.LocalRotations[index].Value;
                float3 boneAxis = this.BoneAxes[index].Value;

                bool isParent = this.ParentRotationPtrs[index].Value != null;
                quaternion parentRotation = isParent ? this.ParentRotationPtrs[index].GetValue : quaternion.identity;
                float3 position = this.PositionPtrs[index].GetValue;

                // 物理演算で用いるパラメータの事前計算
                float radius = param.HitRadius;
                float stiffnessForce = param.StiffnessForce * this.DeltaTime;
                float dragForce = param.DragForce;
                float3 external = param.GraviryDir * (param.GravityPower * this.DeltaTime);
                float3 currentTail = this.CurrentTailPtrs[index].GetValue;
                float3 prevTail = prevTailRef.Value;

                // verlet積分で次の位置を計算
                var nextTail = currentTail
                    // 前フレームの移動を継続する(減衰もあるよ)
                    + (currentTail - prevTail) * (1.0f - dragForce)
                    // 親の回転による子ボーンの移動目標
                    + math.mul(math.mul(parentRotation, localRotation), boneAxis) * stiffnessForce
                    // 外力による移動量
                    + external;

                // 長さをboneLengthに強制
                nextTail = position + math.normalize(nextTail - position) * vecLength;

                // Collisionで移動
                var colliderIdentifyEntity = this.ColliderIdentifys[index].Entity;
                nextTail = this.Collision(nextTail, radius, position, vecLength, colliderIdentifyEntity);

                *this.CurrentTailPtrs[index].Value = nextTail;
                prevTailRef = new PrevTail { Value = currentTail };

                // 回転を適用
                *this.RotationPtrs[index].Value = this.ApplyRotation(nextTail, parentRotation, localRotation, position, boneAxis);
            }

            quaternion ApplyRotation(float3 nextTail, quaternion parentRotation, quaternion localRotation, float3 position, float3 boneAxis)
            {
                var rotation = math.mul(parentRotation, localRotation);
                return Quaternion.FromToRotation(math.mul(rotation, boneAxis), nextTail - position) * rotation;
            }

            float3 Collision(float3 nextTail, float radius, float3 position, float vecLength, Entity colliderIdentifyEntity)
            {
                SphereCollider collider;
                NativeMultiHashMapIterator<Entity> iterator;
                for (bool success = this.ColliderHashmap.TryGetFirstValue(colliderIdentifyEntity, out collider, out iterator);
                    success;
                    success = this.ColliderHashmap.TryGetNextValue(out collider, ref iterator))
                {
                    float r = radius + collider.Radius;
                    if (math.lengthsq(nextTail - collider.Position) <= (r * r))
                    {
                        // ヒット。Colliderの半径方向に押し出す
                        var normal = math.normalize(nextTail - collider.Position);
                        var posFromCollider = collider.Position + normal * (radius + collider.Radius);
                        // 長さをboneLengthに強制
                        nextTail = position + math.normalize(posFromCollider - position) * vecLength;
                    }
                }
                return nextTail;
            }
        }

        #endregion // Jobs

        // ------------------------------
        #region // Private Fields

        // ComponentGroup
        ComponentGroup _sphereColliderGroup;
        ComponentGroup _spriteBoneGroup;

        NativeMultiHashMap<Entity, SphereCollider> _colliderHashmap;

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Protected Methods

        protected override void OnCreateManager()
        {
            this._sphereColliderGroup = base.GetComponentGroup(
                ComponentType.ReadOnly<ColliderIdentify>(),
                ComponentType.ReadOnly<ColliderGroupPositionPtr>(),
                ComponentType.ReadOnly<SphereColliderParamPtr>());

            this._spriteBoneGroup = base.GetComponentGroup(
                ComponentType.ReadOnly<PositionPtr>(),
                ComponentType.ReadOnly<RotationPtr>(),
                ComponentType.ReadOnly<ParentRotationPtr>(),
                ComponentType.ReadOnly<ColliderIdentify>(),
                ComponentType.ReadOnly<CurrentTailPtr>(),
                ComponentType.ReadOnly<SpringBoneParamPtr>(),
                ComponentType.ReadOnly<Length>(),
                ComponentType.ReadOnly<LocalRotation>(),
                ComponentType.ReadOnly<BoneAxis>(),

                ComponentType.Create<PrevTail>());
        }

        protected override void OnDestroyManager() => this.DisposeBuffers();

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            this.DisposeBuffers();
            var handle = inputDeps;
            this.SetColliderHashJobHandle(ref handle);
            this.SetLogicJobHandle(ref handle);
            return handle;
        }

        #endregion // Protected Methods

        // ----------------------------------------------------
        #region // Private Methods

        void DisposeBuffers()
        {
            if (this._colliderHashmap.IsCreated) { this._colliderHashmap.Dispose(); }
        }

        NativeArray<T> GetCopyComponentDataArray<T>(
            JobHandle* handhePtr, JobHandle handle, int length, ComponentGroup group, int innerloopBatchCount = DefaultInnerloopBatchCount)
            where T : struct, IComponentData
        {
            var array = new NativeArray<T>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            *handhePtr = new CopyComponentData<T>
            {
                Source = group.GetComponentDataArray<T>(),
                Results = array,
            }.Schedule(length, innerloopBatchCount, handle);
            return array;
        }

        void SetColliderHashJobHandle(ref JobHandle handleRef)
        {
            var groupLength = this._sphereColliderGroup.CalculateLength();

            var handles = new NativeArray<JobHandle>(3, Allocator.Temp);
            JobHandle* handlesPtr = (JobHandle*)NativeArrayUnsafeUtility.GetUnsafePtr(handles);
            var colliderIdentifies = this.GetCopyComponentDataArray<ColliderIdentify>(
                handlesPtr + 0, handleRef, groupLength, this._sphereColliderGroup);
            var colliderGroupPositionPtrs = this.GetCopyComponentDataArray<ColliderGroupPositionPtr>(
                handlesPtr + 1, handleRef, groupLength, this._sphereColliderGroup);
            var sphereColliderParamPtrs = this.GetCopyComponentDataArray<SphereColliderParamPtr>(
                handlesPtr + 2, handleRef, groupLength, this._sphereColliderGroup);
            handleRef = JobHandle.CombineDependencies(handles);
            handles.Dispose();

            // Hashmapの設定
            this._colliderHashmap = new NativeMultiHashMap<Entity, SphereCollider>(groupLength, Allocator.TempJob);
            handleRef = new SetColliderHashJob
            {
                ColliderIdentifies = colliderIdentifies,
                ColliderGroupPositionPtrs = colliderGroupPositionPtrs,
                SphereColliderParamPtrs = sphereColliderParamPtrs,
                ColliderHashmap = this._colliderHashmap.ToConcurrent(),
            }.Schedule(groupLength, DefaultInnerloopBatchCount, handleRef);
        }

        void SetLogicJobHandle(ref JobHandle handleRef)
        {
            var groupLength = this._spriteBoneGroup.CalculateLength();

            var handles = new NativeArray<JobHandle>(9, Allocator.Temp);
            JobHandle* handlesPtr = (JobHandle*)NativeArrayUnsafeUtility.GetUnsafePtr(handles);
            var positionPtrs = this.GetCopyComponentDataArray<PositionPtr>(
                handlesPtr + 0, handleRef, groupLength, this._spriteBoneGroup);
            var rotationPtrs = this.GetCopyComponentDataArray<RotationPtr>(
                handlesPtr + 1, handleRef, groupLength, this._spriteBoneGroup);
            var parentRotationPtrs = this.GetCopyComponentDataArray<ParentRotationPtr>(
                handlesPtr + 2, handleRef, groupLength, this._spriteBoneGroup);
            var colliderIdentifys = this.GetCopyComponentDataArray<ColliderIdentify>(
                handlesPtr + 3, handleRef, groupLength, this._spriteBoneGroup);
            var currentTailPtrs = this.GetCopyComponentDataArray<CurrentTailPtr>(
                handlesPtr + 4, handleRef, groupLength, this._spriteBoneGroup);
            var springBoneParamPtrs = this.GetCopyComponentDataArray<SpringBoneParamPtr>(
                handlesPtr + 5, handleRef, groupLength, this._spriteBoneGroup);
            var lengths = this.GetCopyComponentDataArray<Length>(
                handlesPtr + 6, handleRef, groupLength, this._spriteBoneGroup);
            var localRotations = this.GetCopyComponentDataArray<LocalRotation>(
                handlesPtr + 7, handleRef, groupLength, this._spriteBoneGroup);
            var boneAxes = this.GetCopyComponentDataArray<BoneAxis>(
                handlesPtr + 8, handleRef, groupLength, this._spriteBoneGroup);
            handleRef = JobHandle.CombineDependencies(handles);
            handles.Dispose();

            // ロジックの実行
            handleRef = new LogicJob
            {
                PositionPtrs = positionPtrs,
                RotationPtrs = rotationPtrs,
                ParentRotationPtrs = parentRotationPtrs,
                ColliderIdentifys = colliderIdentifys,
                CurrentTailPtrs = currentTailPtrs,
                SpringBoneParamPtrs = springBoneParamPtrs,
                Lengths = lengths,
                LocalRotations = localRotations,
                BoneAxes = boneAxes,
                ColliderHashmap = this._colliderHashmap,
                DeltaTime = Time.deltaTime,
            }.Schedule(this, handleRef);
        }

        #endregion // Private Methods
    }
}
