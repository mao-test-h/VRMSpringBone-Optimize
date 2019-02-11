namespace VRM.Optimize.Entities
{
    using System.Collections.Generic;
    using UnityEngine;
    using Unity.Entities;

    public sealed class VRMSpringBoneECS : MonoBehaviour
    {
        // ------------------------------

        #region // Private Fields(Editable)

        [Header("【Settings】")] [SerializeField]
        bool _isAutoGetBuffers = false;

#if UNITY_EDITOR && ENABLE_DEBUG
        [Header("【Gizmos】")] [SerializeField] bool _drawGizmo = false;
        [SerializeField] Color _ecsSpringBoneColor = Color.red;
        [SerializeField] Color _ecsColliderColor = Color.magenta;
        [SerializeField] Color _originalColliderColor = Color.green;
#endif

        #endregion // Private Fields(Editable)

        // ------------------------------

        #region // Private Fields

        readonly List<VRMSpringBone> _springBones = new List<VRMSpringBone>();

        EntityManager _entityManager = null;
        Entity _colliderIdentifyPrefab;
        Entity _sphereColliderPrefab;

        #endregion // Private Fields


        // ----------------------------------------------------

        #region // Unity Events

        /// <summary>
        /// MonoBehaviour.Start
        /// </summary>
        void Start()
        {
            // Get System(use Default World.)
            this._entityManager = World.Active.GetOrCreateManager<EntityManager>();

            // Create Entity Prefab
            {
                var archetype = this._entityManager.CreateArchetype(
                    // Built-in ComponentData
                    ComponentType.Create<Prefab>(),
                    
                    // Original ComponentData
                    ComponentType.Create<ColliderIdentifyTag>());
                this._colliderIdentifyPrefab = this._entityManager.CreateEntity(archetype);
            }
            {
                // SphereCollider
                var archetype = this._entityManager.CreateArchetype(
                    // Built-in ComponentData
                    ComponentType.Create<Prefab>(),

                    // Original ComponentData
                    ComponentType.Create<ColliderIdentify>(),
                    ComponentType.Create<ColliderGroup>(),
                    ComponentType.Create<ColliderGroupBlittableFieldsPtr>());
                this._sphereColliderPrefab = this._entityManager.CreateEntity(archetype);
            }

            // 初期登録
            if (!this._isAutoGetBuffers) return;
            this.AddSpringBones(FindObjectsOfType<VRMSpringBone>());
        }

        /// <summary>
        /// MonoBehaviour.OnDestroy
        /// </summary>
        void OnDestroy()
        {
            foreach (var springBone in this._springBones.ToArray())
            {
                if (!this._springBones.Contains(springBone))
                {
                    continue;
                }

                this._springBones.Remove(springBone);
                springBone.Dispose();
            }
        }

        #endregion // Unity Events

        // ----------------------------------------------------

        #region // Public Methods

        public void AddSpringBone(VRMSpringBone springBone)
        {
            if (springBone.RootBones == null
                || springBone.RootBones.Count <= 0
                || this._springBones.Contains(springBone))
            {
                return;
            }

            springBone.Initialize();
            this.CreateSphereColliderEntities(springBone);
            this.CreateSpringBoneEntities(springBone);
            springBone.UpdateSyncData();
            this._springBones.Add(springBone);
        }

        public void AddSpringBones(VRMSpringBone[] springBones)
        {
            foreach (var springBone in springBones)
            {
                if (springBone.RootBones == null
                    || springBone.RootBones.Count <= 0
                    || this._springBones.Contains(springBone))
                {
                    continue;
                }

                springBone.Initialize();
                this.CreateSphereColliderEntities(springBone);
                this.CreateSpringBoneEntities(springBone);
                springBone.UpdateSyncData();
                this._springBones.Add(springBone);
            }
        }

        public void RemoveSpringBone(VRMSpringBone springBone)
        {
            if (!this._springBones.Contains(springBone))
            {
                return;
            }

            this.DestroySpringBoneEntities(springBone);
            this._springBones.Remove(springBone);
            springBone.Dispose();
        }

        #endregion // Public Methods

        // ----------------------------------------------------

        #region // Private Methods

        unsafe void CreateSphereColliderEntities(VRMSpringBone springBone)
        {
            var groups = springBone.ColliderGroups;
            if (groups == null || groups.Length <= 0)
            {
                return;
            }

            // SphereColliderEntityが所属するVRMSpringBoneを把握するためのEntity
            var colliderIdentify = this._entityManager.Instantiate(this._colliderIdentifyPrefab);
            springBone.ColliderIdentifyEntity = colliderIdentify;

            var createSphereColliderEntities = new List<Entity>();
            foreach (var group in groups)
            {
                if (!group.IsActive) continue;
                
                // SphereColliderEntityが所属するVRMSpringBoneColliderGroupを把握するためのEntity
                var groupEntity = group.CreateEntity(this._entityManager);

                // SphereColliderEntityの生成
                for (var i = 0; i < group.Colliders.Length; i++)
                {
                    var entity = this._entityManager.Instantiate(this._sphereColliderPrefab);
                    this._entityManager.SetComponentData(
                        entity, new ColliderIdentify {Entity = colliderIdentify});
                    this._entityManager.SetComponentData(
                        entity, new ColliderGroup {Entity = groupEntity});
                    this._entityManager.SetComponentData(
                        entity, new ColliderGroupBlittableFieldsPtr {Value = group.GetBlittableFieldsPtr(i)});
                    createSphereColliderEntities.Add(entity);
                }
            }

            springBone.SphereColliderEntities = createSphereColliderEntities;
        }

        void CreateSpringBoneEntities(VRMSpringBone springBone)
        {
            foreach (var node in springBone.Nodes)
            {
                node.CreateEntity(this._entityManager);
            }
        }

        void DestroySpringBoneEntities(VRMSpringBone springBone)
        {
            foreach (var colliderEntity in springBone.SphereColliderEntities)
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
            if (!this._entityManager.Exists(entity))
            {
                return;
            }

            this._entityManager.DestroyEntity(entity);
        }

        #endregion // Private Methods


#if UNITY_EDITOR && ENABLE_DEBUG
        // ----------------------------------------------------

        #region // OnDrawGizmos 

        /// <summary>
        /// MonoBehaviour.OnDrawGizmos
        /// </summary>
        unsafe void OnDrawGizmos()
        {
            if (this._springBones.Count <= 0 || !this._drawGizmo)
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
                    var currentTail = this._entityManager.GetComponentData<CurrentTail>(entity);
                    var prevTail = this._entityManager.GetComponentData<PrevTail>(entity);
                    var currentTailVal = currentTail.Value;
                    var prevTailVal = prevTail.Value;

                    var param = this._entityManager.GetComponentData<SpringBoneBlittableFieldsPtr>(entity).Value;
                    var radius = param->HitRadius;
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(currentTailVal, prevTailVal);
                    Gizmos.DrawWireSphere(prevTailVal, radius);

                    var position = this._entityManager.GetComponentData<Position>(entity).Value;
                    Gizmos.color = this._ecsSpringBoneColor;
                    Gizmos.DrawLine(currentTailVal, position);
                    Gizmos.DrawWireSphere(currentTailVal, radius);
                }

                // Original CollisionGroup
                var colliderGroups = springBone.ColliderGroups;
                if (colliderGroups == null || colliderGroups.Length <= 0)
                {
                    continue;
                }

                foreach (var group in colliderGroups)
                {
                    Gizmos.color = this._originalColliderColor;
                    var mat = group.transform.localToWorldMatrix;
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
                var colliderEntities = springBone.SphereColliderEntities;
                if (colliderEntities == null || colliderEntities.Count <= 0)
                {
                    continue;
                }

                foreach (var colliderEntity in colliderEntities)
                {
                    var groupEntity = this._entityManager
                        .GetComponentData<ColliderGroup>(colliderEntity).Entity;
                    var colliderGroupPosition = this._entityManager
                        .GetComponentData<Position>(groupEntity).Value;
                    var colliderGroupRotation = this._entityManager
                        .GetComponentData<ColliderGroupRotation>(groupEntity).Value;
                    var sphereColliderParam = this._entityManager
                        .GetComponentData<ColliderGroupBlittableFieldsPtr>(colliderEntity).GetValue;
                    var mat = new Unity.Mathematics.float4x4(colliderGroupRotation, colliderGroupPosition);
                    Gizmos.color = this._ecsColliderColor;
                    Gizmos.DrawWireSphere(
                        Unity.Mathematics.math.transform(mat, sphereColliderParam.Offset),
                        sphereColliderParam.Radius);
                }
            }
        }

        #endregion // OnDrawGizmos

#endif
    }
}
