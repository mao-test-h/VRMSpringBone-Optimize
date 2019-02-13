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
                // SphereCollider
                var archetype = this._entityManager.CreateArchetype(
                    ComponentType.Create<Prefab>(),
                    ComponentType.Create<SphereColliderTag>());
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

            springBone.Initialize(this._entityManager);
            this.CreateSphereColliderEntities(springBone);
            this.CreateSpringBoneEntities(springBone);
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

                springBone.Initialize(this._entityManager);
                this.CreateSphereColliderEntities(springBone);
                this.CreateSpringBoneEntities(springBone);
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

            var createSphereColliderEntities = new List<Entity>();
            foreach (var group in groups)
            {
                if (!group.IsActive) continue;

                var groupEntity = group.CreateEntity();

                // SphereColliderEntityの生成
                // ※こちらのEntityはNativeMultiHashMap生成時の長さの算出に使用する
                for (var i = 0; i < group.Colliders.Length; i++)
                {
                    var entity = this._entityManager.Instantiate(this._sphereColliderPrefab);
                    this._entityManager.SetComponentData(entity, new SphereColliderTag());
                    createSphereColliderEntities.Add(entity);
                }
            }

            springBone.SphereColliderEntities = createSphereColliderEntities;
        }

        void CreateSpringBoneEntities(VRMSpringBone springBone)
        {
            foreach (var node in springBone.Nodes)
            {
                node.CreateEntity();
            }
        }

        void DestroySpringBoneEntities(VRMSpringBone springBone)
        {
            foreach (var colliderEntity in springBone.SphereColliderEntities)
            {
                this.DestroyEntity(colliderEntity);
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
                    var centerEntity = this._entityManager.GetComponentData<CenterEntity>(entity);
                    
                    var centerMatrix = Unity.Mathematics.float4x4.identity;
                    if (this._entityManager.Exists(centerEntity.Entity))
                    {
                        var center = this._entityManager.GetComponentData<Center>(centerEntity.Entity);
                        centerMatrix = center.Value;
                    }
                    
                    var currentTail = this._entityManager.GetComponentData<CurrentTail>(entity);
                    var prevTail = this._entityManager.GetComponentData<PrevTail>(entity);
                    var currentTailVal = Unity.Mathematics.math.transform(centerMatrix, currentTail.Value);
                    var prevTailVal = Unity.Mathematics.math.transform(centerMatrix, prevTail.Value);

                    var param = this._entityManager.GetComponentData<SpringBoneBlittableFieldsPtr>(entity).Value;
                    var radius = param->HitRadius;
                    Gizmos.color = Color.gray;
                    Gizmos.DrawLine(currentTailVal, prevTailVal);
                    Gizmos.DrawWireSphere(prevTailVal, radius);

                    var position = node.Transform.position;
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
                foreach (var colliderGroup in springBone.ColliderGroups)
                {
                    var trs = colliderGroup.transform;
                    var entity = colliderGroup.Entity;
                    var mat = new Unity.Mathematics.float4x4(trs.rotation, trs.position);
                    var blittableFields = this._entityManager.GetComponentData<ColliderGroupBlittableFieldsPtr>(entity);
                    for (var i = 0; i < blittableFields.Length; i++)
                    {
                        Gizmos.color = this._ecsColliderColor;
                        var field = blittableFields.GetBlittableFields(i);
                        Gizmos.DrawWireSphere(
                            Unity.Mathematics.math.transform(mat, field.Offset),
                            field.Radius);
                    }
                }
            }
        }

        #endregion // OnDrawGizmos

#endif
    }
}
