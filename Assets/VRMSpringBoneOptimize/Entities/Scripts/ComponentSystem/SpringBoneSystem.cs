namespace VRM.Optimize.Entities
{
    using UnityEngine;
    using UnityEngine.Jobs;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using Unity.Burst;

    [UpdateAfter(typeof(UnityEngine.Experimental.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate))]
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

        #endregion // Defines

        // ------------------------------

        #region // Jobs

        [BurstCompile]
        struct UpdateColliderHashJob : IJobParallelForTransform
        {
            [ReadOnly] public EntityArray Entities;
            [ReadOnly] public ComponentDataFromEntity<ColliderGroupInstanceID> ColliderGroupInstanceIDs;
            [ReadOnly] public ComponentDataFromEntity<ColliderGroupBlittableFieldsPtr> ColliderGroupBlittableFieldsPtr;
            public NativeMultiHashMap<int, SphereCollider>.Concurrent ColliderHashMap;

            void IJobParallelForTransform.Execute(int index, TransformAccess trsAccess)
            {
                var entity = this.Entities[index];
                var mat = new float4x4(trsAccess.rotation, trsAccess.position);
                var fields = this.ColliderGroupBlittableFieldsPtr[entity];
                for (var i = 0; i < fields.Length; i++)
                {
                    var blittableFields = fields.GetBlittableFields(i);
                    var collider = new SphereCollider
                    {
                        Position = math.transform(mat, blittableFields.Offset),
                        Radius = blittableFields.Radius,
                    };
                    this.ColliderHashMap.Add(this.ColliderGroupInstanceIDs[entity].Value, collider);
                }
            }
        }

        [BurstCompile]
        struct UpdateRotationJob : IJobParallelForTransform
        {
            [ReadOnly] public EntityArray Entities;
            [NativeDisableParallelForRestriction] public ComponentDataFromEntity<Rotation> Rotations;

            void IJobParallelForTransform.Execute(int index, TransformAccess trsAccess)
            {
                var entity = this.Entities[index];
                this.Rotations[entity] = new Rotation {Value = trsAccess.rotation};
            }
        }

        [BurstCompile]
        struct UpdateCenterJob : IJobParallelForTransform
        {
            [ReadOnly] public EntityArray Entities;
            [NativeDisableParallelForRestriction] public ComponentDataFromEntity<Center> Centers;

            void IJobParallelForTransform.Execute(int index, TransformAccess trsAccess)
            {
                var entity = this.Entities[index];
                this.Centers[entity] = new Center {Value = new float4x4(trsAccess.rotation, trsAccess.position)};
            }
        }

        /// <summary>
        /// The base algorithm is http://rocketjump.skr.jp/unity3d/109/ of @ricopin416
        /// </summary>
        [BurstCompile]
        struct LogicJob : IJobParallelForTransform
        {
            [ReadOnly] public EntityArray Entities;

            [ReadOnly] public ComponentDataFromEntity<SpringBoneBlittableFieldsPtr> SpringBoneBlittableFieldsPtr;
            [ReadOnly] public ComponentDataFromEntity<Length> Lengths;
            [ReadOnly] public ComponentDataFromEntity<LocalRotation> LocalRotations;
            [ReadOnly] public ComponentDataFromEntity<BoneAxis> BoneAxes;
            [ReadOnly] public ComponentDataFromEntity<ParentEntity> ParentEntities;
            [ReadOnly] public ComponentDataFromEntity<Rotation> Rotations;
            [ReadOnly] public ComponentDataFromEntity<Center> Centers;
            [ReadOnly] public ComponentDataFromEntity<CenterEntity> CenterEntities;

            [NativeDisableParallelForRestriction] public ComponentDataFromEntity<CurrentTail> CurrentTails;
            [NativeDisableParallelForRestriction] public ComponentDataFromEntity<PrevTail> PrevTails;

            [ReadOnly] public NativeMultiHashMap<int, SphereCollider> ColliderHashMap;
            [ReadOnly] public float DeltaTime;

            unsafe void IJobParallelForTransform.Execute(int index, TransformAccess trsAccess)
            {
                var entity = this.Entities[index];

                // フィールドが無いものについては回転値参照用のダミーなので無視
                var fieldsPtr = this.SpringBoneBlittableFieldsPtr[entity].Value;
                if (fieldsPtr == null) return;

                var fields = *fieldsPtr;
                var vecLength = this.Lengths[entity].Value;
                var localRotation = this.LocalRotations[entity].Value;
                var boneAxis = this.BoneAxes[entity].Value;

                float3 position = trsAccess.position;
                var parentRotation = quaternion.identity;
                if (this.ParentEntities.Exists(entity))
                {
                    var parentEntity = this.ParentEntities[entity].Entity;
                    parentRotation = this.Rotations[parentEntity].Value;
                }

                // 物理演算で用いるパラメータの事前計算
                var stiffnessForce = fields.StiffnessForce * this.DeltaTime;
                var dragForce = fields.DragForce;
                var external = fields.GravityDir * (fields.GravityPower * this.DeltaTime);

                var centerMatrix = float4x4.identity;
                var centerInvertMatrix = float4x4.identity;
                if (this.CenterEntities.Exists(entity))
                {
                    var centerEntity = this.CenterEntities[entity].Entity;
                    if (this.Centers.Exists(centerEntity))
                    {
                        centerMatrix = this.Centers[centerEntity].Value;
                        centerInvertMatrix = math.inverse(centerMatrix);
                    }
                }

                var currentTail = math.transform(centerMatrix, this.CurrentTails[entity].Value);
                var prevTail = math.transform(centerMatrix, this.PrevTails[entity].Value);

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
                this.Collision(ref nextTail, ref position, ref vecLength, ref fields);

                this.CurrentTails[entity] = new CurrentTail {Value = math.transform(centerInvertMatrix, nextTail)};
                this.PrevTails[entity] = new PrevTail {Value = math.transform(centerInvertMatrix, currentTail)};

                // 回転を適用
                trsAccess.rotation = this.ApplyRotation(ref nextTail, ref parentRotation, ref localRotation,
                    ref position, ref boneAxis);
            }

            quaternion ApplyRotation(ref float3 nextTail, ref quaternion parentRotation, ref quaternion localRotation,
                ref float3 position, ref float3 boneAxis)
            {
                var rotation = math.mul(parentRotation, localRotation);
                return Quaternion.FromToRotation(math.mul(rotation, boneAxis), nextTail - position) * rotation;
            }

            void Collision(ref float3 nextTail, ref float3 position, ref float vecLength,
                ref VRMSpringBone.BlittableFields blittableFields)
            {
                var hitRadius = blittableFields.HitRadius;
                for (var i = 0; i < blittableFields.ColliderGroupInstanceIDsLength; i++)
                {
                    var instanceID = blittableFields.GetColliderGroupInstanceID(i);
                    for (var success =
                            this.ColliderHashMap.TryGetFirstValue(instanceID, out var collider, out var iterator);
                        success;
                        success = this.ColliderHashMap.TryGetNextValue(out collider, ref iterator))
                    {
                        var r = hitRadius + collider.Radius;
                        if (!(math.lengthsq(nextTail - collider.Position) <= (r * r))) continue;
                        // ヒット。Colliderの半径方向に押し出す
                        var normal = math.normalize(nextTail - collider.Position);
                        var posFromCollider = collider.Position + normal * (hitRadius + collider.Radius);
                        // 長さをboneLengthに強制
                        nextTail = position + math.normalize(posFromCollider - position) * vecLength;
                    }
                }
            }
        }

        #endregion // Jobs

        // ------------------------------

        #region // Private Fields

        // ComponentGroup
        ComponentGroup _colliderGroup;
        ComponentGroup _sphereColliderGroup;

        ComponentGroup _updateCenterGroup;
        ComponentGroup _spriteBoneGroup;

        NativeMultiHashMap<int, SphereCollider> _colliderHashMap;

        #endregion // Private Fields


        // ----------------------------------------------------

        #region // Protected Methods

        protected override void OnCreateManager()
        {
            this._colliderGroup = base.GetComponentGroup(
                typeof(Transform),
                ComponentType.ReadOnly<ColliderGroupInstanceID>(),
                ComponentType.ReadOnly<ColliderGroupBlittableFieldsPtr>());

            this._sphereColliderGroup = base.GetComponentGroup(
                ComponentType.ReadOnly<SphereColliderTag>());

            this._updateCenterGroup = base.GetComponentGroup(
                typeof(Transform),
                ComponentType.Create<Center>());

            this._spriteBoneGroup = base.GetComponentGroup(
                typeof(Transform),
                ComponentType.ReadOnly<SpringBoneBlittableFieldsPtr>(),
                ComponentType.ReadOnly<Length>(),
                ComponentType.ReadOnly<LocalRotation>(),
                ComponentType.ReadOnly<BoneAxis>(),
                ComponentType.ReadOnly<ParentEntity>(),
                ComponentType.ReadOnly<CenterEntity>(),
                ComponentType.Create<Rotation>(),
                ComponentType.Create<CurrentTail>(),
                ComponentType.Create<PrevTail>());
        }

        protected override void OnDestroyManager() => this.DisposeBuffers();

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            this.DisposeBuffers();
            var handle = inputDeps;

            var isUpdateCenter = this._updateCenterGroup.CalculateLength() > 0;
            var handles = new NativeArray<JobHandle>(isUpdateCenter ? 3 : 2, Allocator.Temp);

            // コライダーの更新
            {
                this._colliderHashMap = new NativeMultiHashMap<int, SphereCollider>(
                    this._sphereColliderGroup.CalculateLength(), Allocator.TempJob);
                handles[0] = new UpdateColliderHashJob
                {
                    Entities = this._colliderGroup.GetEntityArray(),
                    ColliderGroupInstanceIDs = base.GetComponentDataFromEntity<ColliderGroupInstanceID>(true),
                    ColliderGroupBlittableFieldsPtr =
                        base.GetComponentDataFromEntity<ColliderGroupBlittableFieldsPtr>(true),
                    ColliderHashMap = this._colliderHashMap.ToConcurrent(),
                }.Schedule(this._colliderGroup.GetTransformAccessArray());
            }

            // 回転値の更新
            {
                handles[1] = new UpdateRotationJob
                {
                    Entities = this._spriteBoneGroup.GetEntityArray(),
                    Rotations = base.GetComponentDataFromEntity<Rotation>(),
                }.Schedule(this._spriteBoneGroup.GetTransformAccessArray());
            }

            // m_centerの更新
            if (isUpdateCenter)
            {
                handles[2] = new UpdateCenterJob
                {
                    Entities = this._updateCenterGroup.GetEntityArray(),
                    Centers = base.GetComponentDataFromEntity<Center>(),
                }.Schedule(this._updateCenterGroup.GetTransformAccessArray());
            }

            // 物理演算
            {
                var preHandle = JobHandle.CombineDependencies(handle, JobHandle.CombineDependencies(handles));
                handle = new LogicJob
                {
                    Entities = this._spriteBoneGroup.GetEntityArray(),

                    SpringBoneBlittableFieldsPtr = base.GetComponentDataFromEntity<SpringBoneBlittableFieldsPtr>(true),
                    Lengths = base.GetComponentDataFromEntity<Length>(true),
                    LocalRotations = base.GetComponentDataFromEntity<LocalRotation>(true),
                    BoneAxes = base.GetComponentDataFromEntity<BoneAxis>(true),
                    ParentEntities = base.GetComponentDataFromEntity<ParentEntity>(true),
                    Rotations = base.GetComponentDataFromEntity<Rotation>(true),

                    Centers = base.GetComponentDataFromEntity<Center>(true),
                    CenterEntities = base.GetComponentDataFromEntity<CenterEntity>(true),

                    CurrentTails = base.GetComponentDataFromEntity<CurrentTail>(),
                    PrevTails = base.GetComponentDataFromEntity<PrevTail>(),

                    DeltaTime = Time.deltaTime,
                    ColliderHashMap = this._colliderHashMap,
                }.Schedule(_spriteBoneGroup.GetTransformAccessArray(), preHandle);
            }
            handles.Dispose();
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

        #endregion // Private Methods
    }
}
