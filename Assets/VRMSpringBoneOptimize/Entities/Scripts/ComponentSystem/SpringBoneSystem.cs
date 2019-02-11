namespace VRM.Optimize.Entities
{
    using UnityEngine;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Burst;

    [UpdateInGroup(typeof(SpringBoneUpdateInGroup))]
    [UpdateAfter(typeof(SyncTransformSystem))]
    public sealed class SpringBoneSystem : JobComponentSystem
    {
        // ------------------------------

        #region // Defines

        // NativeMultiHashMap登録用
        struct SphereCollider
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
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ColliderGroup> ColliderGroups;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ColliderGroupBlittableFieldsPtr> ColliderGroupBlittableFieldsPtr;
            [ReadOnly] public ComponentDataFromEntity<Position> ColliderGroupPosition;
            [ReadOnly] public ComponentDataFromEntity<ColliderGroupRotation> ColliderGroupRotation;
            public NativeMultiHashMap<Entity, SphereCollider>.Concurrent ColliderHashMap;

            public void Execute(int index)
            {
                var mat = new float4x4(
                    this.ColliderGroupRotation[this.ColliderGroups[index].Entity].Value,
                    this.ColliderGroupPosition[this.ColliderGroups[index].Entity].Value);
                var fields = this.ColliderGroupBlittableFieldsPtr[index].GetValue;
                var collider = new SphereCollider
                {
                    Position = math.transform(mat, fields.Offset),
                    Radius = fields.Radius,
                };
                this.ColliderHashMap.Add(this.ColliderIdentifies[index].Entity, collider);
            }
        }

        [BurstCompile]
        struct CopyParentJob : IJobParallelFor
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Position> Positions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Parent> Parents;
            [ReadOnly] public ComponentDataFromEntity<CurrentTail> ParentCurrentTails;
            [ReadOnly] public ComponentDataFromEntity<Rotation> ParentRotations;
            [WriteOnly] public NativeArray<Position> CopyPositions;
            [WriteOnly] public NativeArray<Rotation> CopyParentRotations;

            public void Execute(int index)
            {
                var parentEntity = this.Parents[index].Entity;
                var position = this.Positions[index].Value;
                if (this.ParentCurrentTails.Exists(parentEntity))
                {
                    position = this.ParentCurrentTails[parentEntity].Value;
                }

                this.CopyPositions[index] = new Position {Value = position};

                var parentRotation = quaternion.identity;
                if (this.ParentRotations.Exists(parentEntity))
                {
                    parentRotation = this.ParentRotations[parentEntity].Value;
                }

                this.CopyParentRotations[index] = new Rotation {Value = parentRotation};
            }
        }

        /// <summary>
        /// The base algorithm is http://rocketjump.skr.jp/unity3d/109/ of @ricopin416
        /// </summary>
        [BurstCompile]
        struct LogicJob : IJobProcessComponentDataWithEntity<CurrentTail, PrevTail, Rotation>
        {
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Position> Positions;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Rotation> ParentRotations;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<ColliderIdentify> ColliderIdentifies;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<SpringBoneBlittableFieldsPtr> SpringBoneBlittableFieldsPtr;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<Length> Lengths;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<LocalRotation> LocalRotations;
            [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<BoneAxis> BoneAxes;
            [ReadOnly] public NativeMultiHashMap<Entity, SphereCollider> ColliderHashMap;
            [ReadOnly] public float DeltaTime;

            public void Execute(Entity entity, int index, ref CurrentTail currentTailRef, ref PrevTail prevTailRef,
                ref Rotation rotationRef)
            {
                var fields = this.SpringBoneBlittableFieldsPtr[index].GetValue;
                var vecLength = this.Lengths[index].Value;
                var localRotation = this.LocalRotations[index].Value;
                var boneAxis = this.BoneAxes[index].Value;

                var parentRotation = this.ParentRotations[index].Value;
                var position = this.Positions[index].Value;

                // 物理演算で用いるパラメータの事前計算
                var radius = fields.HitRadius;
                var stiffnessForce = fields.StiffnessForce * this.DeltaTime;
                var dragForce = fields.DragForce;
                var external = fields.GravityDir * (fields.GravityPower * this.DeltaTime);
                var currentTail = currentTailRef.Value;
                var prevTail = prevTailRef.Value;

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
                var colliderIdentifyEntity = this.ColliderIdentifies[index].Entity;
                nextTail = this.Collision(nextTail, radius, position, vecLength, colliderIdentifyEntity);

                currentTailRef.Value = nextTail;
                prevTailRef.Value = currentTail;

                // 回転を適用
                rotationRef.Value = this.ApplyRotation(nextTail, parentRotation, localRotation, position, boneAxis);
            }

            quaternion ApplyRotation(float3 nextTail, quaternion parentRotation, quaternion localRotation,
                float3 position, float3 boneAxis)
            {
                var rotation = math.mul(parentRotation, localRotation);
                return Quaternion.FromToRotation(math.mul(rotation, boneAxis), nextTail - position) * rotation;
            }

            float3 Collision(float3 nextTail, float radius, float3 position, float vecLength,
                Entity colliderIdentifyEntity)
            {
                for (var success =
                        this.ColliderHashMap.TryGetFirstValue(colliderIdentifyEntity, out var collider,
                            out var iterator);
                    success;
                    success = this.ColliderHashMap.TryGetNextValue(out collider, ref iterator))
                {
                    var r = radius + collider.Radius;
                    if (!(math.lengthsq(nextTail - collider.Position) <= (r * r))) continue;
                    // ヒット。Colliderの半径方向に押し出す
                    var normal = math.normalize(nextTail - collider.Position);
                    var posFromCollider = collider.Position + normal * (radius + collider.Radius);
                    // 長さをboneLengthに強制
                    nextTail = position + math.normalize(posFromCollider - position) * vecLength;
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

        NativeMultiHashMap<Entity, SphereCollider> _colliderHashMap;

        #endregion // Private Fields


        // ----------------------------------------------------

        #region // Protected Methods

        protected override void OnCreateManager()
        {
            this._sphereColliderGroup = base.GetComponentGroup(
                ComponentType.ReadOnly<ColliderIdentify>(),
                ComponentType.ReadOnly<ColliderGroup>(),
                ComponentType.ReadOnly<ColliderGroupBlittableFieldsPtr>());

            this._spriteBoneGroup = base.GetComponentGroup(
                ComponentType.ReadOnly<Position>(),
                ComponentType.ReadOnly<Parent>(),
                ComponentType.ReadOnly<ColliderIdentify>(),
                ComponentType.ReadOnly<SpringBoneBlittableFieldsPtr>(),
                ComponentType.ReadOnly<Length>(),
                ComponentType.ReadOnly<LocalRotation>(),
                ComponentType.ReadOnly<BoneAxis>(),
                ComponentType.Create<CurrentTail>(),
                ComponentType.Create<Rotation>(),
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
            if (this._colliderHashMap.IsCreated)
            {
                this._colliderHashMap.Dispose();
            }
        }

        unsafe NativeArray<T> GetCopyComponentDataArray<T>(
            JobHandle* handlePtr, JobHandle handle, int length, ComponentGroup group,
            int innerloopBatchCount = DefaultInnerloopBatchCount)
            where T : struct, IComponentData
        {
            var array = new NativeArray<T>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            *handlePtr = new CopyComponentData<T>
            {
                Source = group.GetComponentDataArray<T>(),
                Results = array,
            }.Schedule(length, innerloopBatchCount, handle);
            return array;
        }

        unsafe void SetColliderHashJobHandle(ref JobHandle handleRef)
        {
            var groupLength = this._sphereColliderGroup.CalculateLength();
            var handles = new NativeArray<JobHandle>(3, Allocator.Temp);
            var handlesPtr = (JobHandle*) handles.GetUnsafePtr();
            var colliderIdentifies = this.GetCopyComponentDataArray<ColliderIdentify>(
                handlesPtr + 0, handleRef, groupLength, this._sphereColliderGroup);
            var colliderGroups = this.GetCopyComponentDataArray<ColliderGroup>(
                handlesPtr + 1, handleRef, groupLength, this._sphereColliderGroup);
            var colliderGroupBlittableFieldsPtr = this.GetCopyComponentDataArray<ColliderGroupBlittableFieldsPtr>(
                handlesPtr + 2, handleRef, groupLength, this._sphereColliderGroup);
            handleRef = JobHandle.CombineDependencies(handles);
            handles.Dispose();

            // HashMapの設定
            this._colliderHashMap = new NativeMultiHashMap<Entity, SphereCollider>(groupLength, Allocator.TempJob);
            handleRef = new SetColliderHashJob
            {
                ColliderIdentifies = colliderIdentifies,
                ColliderGroups = colliderGroups,
                ColliderGroupBlittableFieldsPtr = colliderGroupBlittableFieldsPtr,
                ColliderGroupPosition = base.GetComponentDataFromEntity<Position>(true),
                ColliderGroupRotation = base.GetComponentDataFromEntity<ColliderGroupRotation>(true),
                ColliderHashMap = this._colliderHashMap.ToConcurrent(),
            }.Schedule(groupLength, DefaultInnerloopBatchCount, handleRef);
        }

        unsafe void SetLogicJobHandle(ref JobHandle handleRef)
        {
            var groupLength = this._spriteBoneGroup.CalculateLength();

            var handles = new NativeArray<JobHandle>(7, Allocator.Temp);
            var handlesPtr = (JobHandle*) handles.GetUnsafePtr();
            var positions = this.GetCopyComponentDataArray<Position>(
                handlesPtr + 0, handleRef, groupLength, this._spriteBoneGroup);
            var parents = this.GetCopyComponentDataArray<Parent>(
                handlesPtr + 1, handleRef, groupLength, this._spriteBoneGroup);
            var colliderIdentifies = this.GetCopyComponentDataArray<ColliderIdentify>(
                handlesPtr + 2, handleRef, groupLength, this._spriteBoneGroup);
            var springBoneBlittableFieldsPtr = this.GetCopyComponentDataArray<SpringBoneBlittableFieldsPtr>(
                handlesPtr + 3, handleRef, groupLength, this._spriteBoneGroup);
            var lengths = this.GetCopyComponentDataArray<Length>(
                handlesPtr + 4, handleRef, groupLength, this._spriteBoneGroup);
            var localRotations = this.GetCopyComponentDataArray<LocalRotation>(
                handlesPtr + 5, handleRef, groupLength, this._spriteBoneGroup);
            var boneAxes = this.GetCopyComponentDataArray<BoneAxis>(
                handlesPtr + 6, handleRef, groupLength, this._spriteBoneGroup);
            handleRef = JobHandle.CombineDependencies(handles);
            handles.Dispose();

            // データのコピー
            var copyPositions = new NativeArray<Position>(groupLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var copyParentRotations = new NativeArray<Rotation>(groupLength, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            handleRef = new CopyParentJob()
            {
                Positions = positions,
                Parents = parents,
                ParentCurrentTails = base.GetComponentDataFromEntity<CurrentTail>(true),
                ParentRotations = base.GetComponentDataFromEntity<Rotation>(true),
                CopyPositions =  copyPositions,
                CopyParentRotations = copyParentRotations,
            }.Schedule(groupLength, DefaultInnerloopBatchCount, handleRef);
            
            // ロジックの実行
            handleRef = new LogicJob
            {
                Positions = copyPositions,
                ParentRotations = copyParentRotations,
                ColliderIdentifies = colliderIdentifies,
                SpringBoneBlittableFieldsPtr = springBoneBlittableFieldsPtr,
                Lengths = lengths,
                LocalRotations = localRotations,
                BoneAxes = boneAxes,
                ColliderHashMap = this._colliderHashMap,
                DeltaTime = Time.deltaTime,
            }.Schedule(this, handleRef);
        }

        #endregion // Private Methods
    }
}
