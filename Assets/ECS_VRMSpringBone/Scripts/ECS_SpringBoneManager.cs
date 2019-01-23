namespace VRM.ECS_SpringBone
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    using Unity.Entities;
    using Unity.Collections;
    using Unity.Mathematics;

    public unsafe sealed class ECS_SpringBoneManager : MonoBehaviour
    {
        // ------------------------------
        #region // Private Fields(Editable)

        // 初期登録するSpringBone
        [SerializeField] VRMSpringBone[] _initRegisterSpringBones = null;

#if UNITY_EDITOR && ENABLE_DEBUG
        [Header("【Gizmos】")]
        [SerializeField] bool _drawGizmo = false;
        [SerializeField] Color _ecsSpringBoneColor = Color.red;
        [SerializeField] Color _ecsColliderColor = Color.magenta;
        [SerializeField] Color _originalColliderColor = Color.green;
#endif

#if UNITY_EDITOR
        [Header("【Debug】")]
        public GameObject AddTestModel = null;

        public VRMSpringBone[] InitRegisterSpringBones { set { this._initRegisterSpringBones = value; } }
#endif

        #endregion // Private Fields(Editable)

        // ------------------------------
        #region // Private Fields

        List<VRMSpringBone> _springBones = new List<VRMSpringBone>();

        World _springBoneWorld = null;
        EntityManager _entityManager = null;
        SyncTransformSystem _syncTransformSystem = null;

        Entity _colliderIdentifyPrefab;
        Entity _colliderPrefab;
        Entity _springBonePrefab;

        #endregion // Private Fields


        // ----------------------------------------------------
        #region // Unity Events

        /// <summary>
        /// MonoBehaviour.Start
        /// </summary>
        void Start()
        {
            // Create World & Systems.
            this._springBoneWorld = new World("ECS-SpringBone World");
            this._entityManager = this._springBoneWorld.CreateManager<EntityManager>();
            this._springBoneWorld.CreateManager<SpringBoneSystem>();
            this._syncTransformSystem = this._springBoneWorld.CreateManager<SyncTransformSystem>();
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(this._springBoneWorld);

            // Create Entity Prefab
            {
                // SphereCollider
                var archetype = this._entityManager.CreateArchetype(
                    ComponentType.Create<ColliderIdentifyTag>(),
                    // Built-in ComponentData
                    ComponentType.Create<Prefab>());
                this._colliderIdentifyPrefab = this._entityManager.CreateEntity(archetype);
            }

            {
                // SphereCollider
                var archetype = this._entityManager.CreateArchetype(
                    ComponentType.Create<ColliderIdentify>(),
                    ComponentType.Create<ColliderGroupPositionPtr>(),
                    ComponentType.Create<SphereColliderParamPtr>(),
                    // Built-in ComponentData
                    ComponentType.Create<Prefab>());
                this._colliderPrefab = this._entityManager.CreateEntity(archetype);
            }

            {
                // SpringBone
                var archetype = this._entityManager.CreateArchetype(
                    ComponentType.Create<PositionPtr>(),
                    ComponentType.Create<RotationPtr>(),
                    ComponentType.Create<ParentRotationPtr>(),

                    ComponentType.Create<ColliderIdentify>(),

                    ComponentType.Create<CurrentTailPtr>(),
                    ComponentType.Create<PrevTail>(),

                    ComponentType.Create<SpringBoneParamPtr>(),
                    ComponentType.Create<Length>(),
                    ComponentType.Create<LocalRotation>(),
                    ComponentType.Create<BoneAxis>(),

                    // Built-in ComponentData
                    ComponentType.Create<Prefab>());
                this._springBonePrefab = this._entityManager.CreateEntity(archetype);
            }

            // 初期登録
            foreach (var springBone in this._initRegisterSpringBones)
            {
                this.AddSpringBone(springBone);
            }
        }

#if UNITY_EDITOR && ENABLE_DEBUG
        /// <summary>
        /// MonoBehaviour.OnDrawGizmos
        /// </summary>
        void OnDrawGizmos()
        {
            if (this._springBones.Count <= 0
                || !this._drawGizmo
                || this._springBoneWorld == null
                || !this._springBoneWorld.IsCreated)
            {
                return;
            }

            foreach (var springBone in this._springBones)
            {
                // SpringBone
                Gizmos.matrix = Matrix4x4.identity;
                foreach (var node in springBone.Nodes)
                {
                    var entity = node.Entity;
                    var currentTailPtr = this._entityManager.GetComponentData<CurrentTailPtr>(entity);
                    var prevTail = this._entityManager.GetComponentData<PrevTail>(entity);
                    float3 currentTailVal = currentTailPtr.GetValue;
                    float3 prevTailVal = prevTail.Value;

                    var param = this._entityManager.GetComponentData<SpringBoneParamPtr>(entity).Value;
                    var radius = param->HitRadius;
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(currentTailVal, prevTailVal);
                    Gizmos.DrawWireSphere(prevTailVal, radius);

                    var position = this._entityManager.GetComponentData<PositionPtr>(entity).GetValue;
                    Gizmos.color = this._ecsSpringBoneColor;
                    Gizmos.DrawLine(currentTailVal, position);
                    Gizmos.DrawWireSphere(currentTailVal, radius);
                }

                // Original CollisionGroup
                var colliderGroups = springBone.ColliderGroups;
                if (colliderGroups == null || colliderGroups.Length <= 0) { continue; }
                foreach (var group in colliderGroups)
                {
                    Gizmos.color = this._originalColliderColor;
                    Matrix4x4 mat = group.transform.localToWorldMatrix;
                    Gizmos.matrix = mat * Matrix4x4.Scale(new Vector3(
                        1.0f / group.transform.lossyScale.x,
                        1.0f / group.transform.lossyScale.y,
                        1.0f / group.transform.lossyScale.z));
                    foreach (var y in group.Colliders)
                    {
                        Gizmos.DrawWireSphere(y.Offset, y.Radius);
                    }
                }

                // ECS CollisionGroup
                Gizmos.matrix = Matrix4x4.identity;
                var colliderEntities = springBone.ColliderGroupEntities;
                if (colliderEntities == null || colliderEntities.Length <= 0) { continue; }
                foreach (var colliderEntity in colliderEntities)
                {
                    var colliderGroupPosition = this._entityManager.GetComponentData<ColliderGroupPositionPtr>(colliderEntity).GetValue;
                    var sphereColliderParam = this._entityManager.GetComponentData<SphereColliderParamPtr>(colliderEntity).GetValue;
                    Gizmos.color = this._ecsColliderColor;
                    Gizmos.DrawWireSphere(colliderGroupPosition + sphereColliderParam.Offset, sphereColliderParam.Radius);
                }
            }
        }
#endif

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            foreach (var springBone in this._springBones.ToArray())
            {
                this.RemoveSpringBone(springBone);
            }
            if (this._springBoneWorld != null)
            {
                this._springBoneWorld.Dispose();
            }
        }

        #endregion // Unity Events

        // ----------------------------------------------------
        #region // Public Methods

        public void AddSpringBone(VRMSpringBone springBone)
        {
            if (springBone.RootBones == null || springBone.RootBones.Count <= 0) { return; }
            springBone.Initialize();
            this.CreateSphereColliderEntities(springBone);
            this.CreateSpringBoneEntities(springBone);
            this.AddSyncTransform(springBone);
            this._springBones.Add(springBone);
        }

        public void RemoveSpringBone(VRMSpringBone springBone)
        {
            if (!this._springBones.Contains(springBone)) { return; }
            this.DestroySpringBoneEntities(springBone);
            this.RemoveSyncTransform(springBone);
            this._springBones.Remove(springBone);
            springBone.Dispose();
        }

        #endregion // Public Methods

        // ----------------------------------------------------
        #region // Private Methods

        void CreateSphereColliderEntities(VRMSpringBone springBone)
        {
            var groups = springBone.ColliderGroups;
            if (groups == null || groups.Length <= 0) { return; }

            var colliderIdentify = this._entityManager.Instantiate(this._colliderIdentifyPrefab);
            springBone.ColliderIdentifyEntity = colliderIdentify;

            var createEntities = new List<Entity>();
            foreach (var group in groups)
            {
                var colliders = group.Colliders;
                if (colliders == null || colliders.Length <= 0) { continue; }

                for (int i = 0; i < colliders.Length; i++)
                {
                    var entity = this._entityManager.Instantiate(this._colliderPrefab);
                    this._entityManager.SetComponentData(
                        entity, new ColliderIdentify { Entity = colliderIdentify });
                    this._entityManager.SetComponentData(
                        entity, new ColliderGroupPositionPtr { Value = group.PositionPtr });
                    this._entityManager.SetComponentData(
                        entity, new SphereColliderParamPtr { Value = group.GetSphereColliderParamPtr(i) });
                    createEntities.Add(entity);
                }
            }
            springBone.ColliderGroupEntities = createEntities.ToArray();
        }

        void CreateSpringBoneEntities(VRMSpringBone springBone)
        {
            var paramPtr = springBone.VRMSpringBoneParamPtr;
            foreach (var node in springBone.Nodes)
            {
                var entity = this._entityManager.Instantiate(this._springBonePrefab);
                node.Entity = entity;

                var nodeTrs = node.Transform;
                Vector3 localChildPosition;
                if (nodeTrs.childCount == 0)
                {
                    var delta = nodeTrs.position - nodeTrs.parent.position;
                    var childPosition = nodeTrs.position + delta.normalized * 0.07f;
                    localChildPosition = nodeTrs.worldToLocalMatrix.MultiplyPoint(childPosition);
                }
                else
                {
                    var firstChild = nodeTrs.childCount > 0 ? nodeTrs.GetChild(0) : null;
                    var localPosition = firstChild.localPosition;
                    var scale = firstChild.lossyScale;
                    localChildPosition = new Vector3(
                        localPosition.x * scale.x,
                        localPosition.y * scale.y,
                        localPosition.z * scale.z);
                }
                this._entityManager.SetComponentData(
                    entity, new SpringBoneParamPtr { Value = paramPtr });
                this._entityManager.SetComponentData(
                    entity, new LocalRotation { Value = nodeTrs.localRotation });
                this._entityManager.SetComponentData(
                    entity, new BoneAxis { Value = localChildPosition.normalized });
                this._entityManager.SetComponentData(
                    entity, new Length { Value = localChildPosition.magnitude });

                // ECS側で物理演算を行った後にTransformへの同期処理を行う都合上、物理演算中にTransformの値が更新されないという問題がある。
                // その為に位置取得は親ノードの算出後の値を用いることで都合を合わせるようにしている.
                bool isParent = (node.Parent != null);
                this._entityManager.SetComponentData(
                    entity, new PositionPtr { Value = isParent ? node.Parent.CurrentTailPtr : node.PositionPtr });
                this._entityManager.SetComponentData(
                    entity, new RotationPtr { Value = node.RotationPtr });
                this._entityManager.SetComponentData(
                    entity, new ParentRotationPtr { Value = isParent ? node.Parent.RotationPtr : null });
                this._entityManager.SetComponentData(
                    entity, new ColliderIdentify { Entity = springBone.ColliderIdentifyEntity });

                // TODO: 現状m_centerは非対応
                var currentTail = nodeTrs.TransformPoint(localChildPosition);
                *node.CurrentTailPtr = currentTail;
                this._entityManager.SetComponentData(
                    entity, new CurrentTailPtr { Value = node.CurrentTailPtr });
                this._entityManager.SetComponentData(
                    entity, new PrevTail { Value = currentTail });
            }
        }

        void AddSyncTransform(VRMSpringBone springBone)
        {
            this._syncTransformSystem.AddSyncTransformSOA(springBone);
        }

        void RemoveSyncTransform(VRMSpringBone springBone)
        {
            this._syncTransformSystem.RemoveSyncTransformSOA(springBone);
        }

        void DestroySpringBoneEntities(VRMSpringBone springBone)
        {
            foreach (var colliderEntity in springBone.ColliderGroupEntities)
            {
                this.DestroyEntity(colliderEntity);
            }
            this.DestroyEntity(springBone.ColliderIdentifyEntity);
            foreach (var node in springBone.Nodes)
            {
                this.DestroyEntity(node.Entity);
            }
        }

        void DestroyEntity(Entity entity)
        {
            if (!this._entityManager.Exists(entity)) { return; }
            this._entityManager.DestroyEntity(entity);
        }

        #endregion // Private Methods
    }
}
