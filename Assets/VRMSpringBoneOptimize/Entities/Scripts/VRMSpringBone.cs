namespace VRM.Optimize.Entities
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Assertions;
    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public sealed unsafe class VRMSpringBone : MonoBehaviour, System.IDisposable
    {
        // ------------------------------

        #region // Defines

        public struct BlittableFields
        {
            public float StiffnessForce;
            public float GravityPower;
            public float3 GravityDir;
            public float DragForce;
            public float HitRadius;
        }

        public sealed class Node
        {
            public Transform Transform { get; }
            public Entity Entity { get; private set; }

            GameObjectEntity _gameObjectEntity;
            readonly Node _parent;
            readonly VRMSpringBone _springBone;

            public Node(VRMSpringBone springBone, Transform trs, Node parent = null)
            {
                this.Transform = trs;
                this._parent = parent;
                this._springBone = springBone;
            }

            public Entity CreateEntity(EntityManager manager)
            {
                this._gameObjectEntity = this.Transform.gameObject.AddComponent<GameObjectEntity>();
                this.Entity = this._gameObjectEntity.Entity;

                var entity = this.Entity;
                var nodeTrs = this.Transform;
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
                manager.AddComponentData(
                    entity, new SpringBoneBlittableFieldsPtr{Value = this._springBone.BlittableFieldsPtr});
                manager.AddComponentData(
                    entity, new LocalRotation {Value = nodeTrs.localRotation});
                manager.AddComponentData(
                    entity, new BoneAxis {Value = localChildPosition.normalized});
                manager.AddComponentData(
                    entity, new Length {Value = localChildPosition.magnitude});

                var isParent = (this._parent != null);
                manager.AddComponentData(
                    entity, new Position {Value = nodeTrs.position});
                manager.AddComponentData(
                    entity, new Rotation {Value = nodeTrs.rotation});
                manager.AddComponentData(
                    entity, new Parent {Entity = isParent ? this._parent.Entity : Entity.Null});
                manager.AddComponentData(
                    entity, new ColliderIdentify {Entity = this._springBone.ColliderIdentifyEntity});

                // TODO: 現状m_centerは非対応
                var currentTail = nodeTrs.TransformPoint(localChildPosition);
                manager.AddComponentData(
                    entity, new CurrentTail {Value = currentTail});
                manager.AddComponentData(
                    entity, new PrevTail {Value = currentTail});

                return entity;
            }
        }

        #endregion // Defines

        // ------------------------------

        #region // Fields(Editable)

        public string m_comment;

        [Header("Settings")] [Range(0, 4)] public float m_stiffnessForce = 1.0f;
        [Range(0, 2)] public float m_gravityPower = 0;
        public Vector3 m_gravityDir = new Vector3(0, -1.0f, 0);
        [Range(0, 1)] public float m_dragForce = 0.4f;
        public Transform m_center;
        public List<Transform> RootBones = new List<Transform>();

        [Header("Collider")] [Range(0, 0.5f)] public float m_hitRadius = 0.02f;
        public VRMSpringBoneColliderGroup[] ColliderGroups;

        #endregion // Fields(Editable)

        // ------------------------------

        #region // Fields

        public BlittableFields* BlittableFieldsPtr { get; private set; } = null;
        public List<Node> Nodes { get; private set; } = null;
        public Entity ColliderIdentifyEntity;
        public List<Entity> SphereColliderEntities = null;

        public List<Transform> Transforms { get; private set; } = new List<Transform>();
        public List<Entity> Entities { get; private set; } = new List<Entity>();

        #endregion // Fields


        // ----------------------------------------------------

        #region // Unity Events

#if UNITY_EDITOR && ENABLE_DEBUG
        /// <summary>
        /// MonoBehaviour.Update
        /// </summary>
        void Update()
        {
            // Editor上でのみInspectorからの動的変更を考慮する
            if (this.BlittableFieldsPtr == null)
            {
                return;
            }

            this.CopySpringBoneParam();
        }
#endif

        #endregion // Unity Events

        // ----------------------------------------------------

        #region // Public Methods

        public void Initialize()
        {
            Assert.IsFalse(this.RootBones == null || this.RootBones.Count <= 0);

            // コライダーの初期化
            foreach (var collider in this.ColliderGroups)
            {
                collider.Initialize();
            }

            // 各種パラメータ用のメモリ確保 + 初期化
            {
                this.BlittableFieldsPtr =
                    (BlittableFields*) UnsafeUtilityHelper.Malloc<BlittableFields>(Allocator.Persistent);
                this.CopySpringBoneParam();
            }

            this.Nodes = new List<Node>();
            foreach (var go in RootBones)
            {
                if (go == null)
                {
                    continue;
                }

                this.CreateNode(go);
            }
        }

        public void UpdateSyncData()
        {
            // Transform同期用データ
            this.Transforms.Clear();
            this.Entities.Clear();
            foreach (var node in this.Nodes)
            {
                this.Transforms.Add(node.Transform);
                this.Entities.Add(node.Entity);
                foreach (var collider in this.ColliderGroups)
                {
                    this.Transforms.Add(collider.transform);
                    this.Entities.Add(collider.Entity);
                }
            }
        }

        public void Dispose()
        {
            // コライダーの初期化
            foreach (var collider in this.ColliderGroups)
            {
                collider.Dispose();
            }

            if (this.BlittableFieldsPtr != null)
            {
                UnsafeUtility.Free(this.BlittableFieldsPtr, Allocator.Persistent);
                this.BlittableFieldsPtr = null;
            }

            Nodes?.Clear();
            this.Nodes = null;
        }

        #endregion // Public Events

        // ----------------------------------------------------

        #region // Private Methods

        void CopySpringBoneParam()
        {
            *this.BlittableFieldsPtr = new BlittableFields
            {
                StiffnessForce = this.m_stiffnessForce,
                GravityPower = this.m_gravityPower,
                GravityDir = this.m_gravityDir,
                DragForce = this.m_dragForce,
                HitRadius = this.m_hitRadius,
            };
        }

        void CreateNode(Transform trs, Node parent = null)
        {
            var node = new Node(this, trs, parent);
            this.Nodes.Add(node);
            foreach (Transform child in trs)
            {
                this.CreateNode(child, node);
            }
        }

        #endregion // Private Methods
    }
}
