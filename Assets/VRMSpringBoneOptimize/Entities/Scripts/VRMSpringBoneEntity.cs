namespace VRM.Optimize.Entities
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Assertions;
    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public sealed unsafe class VRMSpringBoneEntity : MonoBehaviour, System.IDisposable
    {
        // ------------------------------

        #region // Defines

        public unsafe struct BlittableFields
        {
            public float StiffnessForce;
            public float GravityPower;
            public float3 GravityDir;
            public float DragForce;
            public float HitRadius;

            public int* ColliderGroupInstanceIDs;
            public int ColliderGroupInstanceIDsLength;

            public int GetColliderGroupInstanceID(int index)
            {
                Assert.IsTrue((index >= 0) && (index < this.ColliderGroupInstanceIDsLength));
                return *(ColliderGroupInstanceIDs + index);
            }
        }

        public sealed class Node
        {
            public Transform Transform { get; }
            public Entity Entity { get; private set; }

            readonly Node _parent;
            readonly BlittableFields* _blittableFieldsPtr;

            readonly EntityManager _entityManager;
            readonly Entity _centerEntity;

            public Node(
                Transform trs,
                BlittableFields* blittableFieldsPtr,
                EntityManager entityManager,
                Entity centerEntity,
                Node parent = null)
            {
                this.Transform = trs;
                this._blittableFieldsPtr = blittableFieldsPtr;
                this._entityManager = entityManager;
                this._centerEntity = centerEntity;
                this._parent = parent;
            }

            public Entity CreateEntity()
            {
                var gameObjectEntity = GetOrAddComponent<GameObjectEntity>(this.Transform.gameObject);
                var entity = this.Entity = gameObjectEntity.Entity;
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

                this._entityManager.AddComponentData(
                    entity, new SpringBoneBlittableFieldsPtr {Value = this._blittableFieldsPtr});
                this._entityManager.AddComponentData(
                    entity, new Length {Value = localChildPosition.magnitude});
                this._entityManager.AddComponentData(
                    entity, new LocalRotation {Value = nodeTrs.localRotation});
                this._entityManager.AddComponentData(
                    entity, new BoneAxis {Value = localChildPosition.normalized});

                var parentEntity = Entity.Null;
                if (this._parent != null)
                {
                    parentEntity = this._parent.Entity;
                }
                else if (nodeTrs.parent != null)
                {
                    // ルートの親が存在する場合には回転値取得用のEntityとして設定する
                    var rootParentObjectEntity = GetOrAddComponent<GameObjectEntity>(nodeTrs.parent.gameObject);
                    var rootParentEntity = rootParentObjectEntity.Entity;
                    if (!this._entityManager.HasComponent<Rotation>(rootParentEntity))
                    {
                        this.SetDummyData(ref rootParentEntity, nodeTrs.parent.rotation);
                    }

                    parentEntity = rootParentEntity;
                }

                this._entityManager.AddComponentData(
                    entity, new ParentEntity {Entity = parentEntity});
                this._entityManager.AddComponentData(
                    entity, new Rotation {Value = nodeTrs.rotation});

                this._entityManager.AddComponentData(
                    entity, new CenterEntity() {Entity = this._centerEntity});
                var currentTail = nodeTrs.TransformPoint(localChildPosition);
                if (this._entityManager.Exists(this._centerEntity))
                {
                    var centerMatrix = this._entityManager.GetComponentData<Center>(this._centerEntity);
                    var centerInvertMatrix = math.inverse(centerMatrix.Value);
                    currentTail = math.transform(centerInvertMatrix, currentTail);
                }
                
                this._entityManager.AddComponentData(
                    entity, new CurrentTail {Value = currentTail});
                this._entityManager.AddComponentData(
                    entity, new PrevTail {Value = currentTail});

                return entity;
            }

            void SetDummyData(ref Entity entity, quaternion rotation)
            {
                // 回転値は全てで必要となるので設定
                this._entityManager.AddComponentData(entity, new Rotation {Value = rotation});
                // 残りはアーキタイプを合わせるために既定値を入れておく
                this._entityManager.AddComponentData(entity, new SpringBoneBlittableFieldsPtr {Value = null});
                this._entityManager.AddComponentData(entity, new Length());
                this._entityManager.AddComponentData(entity, new LocalRotation());
                this._entityManager.AddComponentData(entity, new BoneAxis());
                this._entityManager.AddComponentData(entity, new ParentEntity());
                this._entityManager.AddComponentData(entity, new CenterEntity());
                this._entityManager.AddComponentData(entity, new CurrentTail());
                this._entityManager.AddComponentData(entity, new PrevTail());
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
        public VRMSpringBoneColliderGroupEntity[] ColliderGroups;

        #endregion // Fields(Editable)

        // ------------------------------

        #region // Fields

        public List<Node> Nodes { get; private set; } = null;
        public List<Entity> SphereColliderEntities = null;

        BlittableFields* _blittableFieldsPtr = null;
        NativeArray<int> _colliderGroupInstanceIDs;

        EntityManager _entityManager;
        Entity _centerEntity;

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
            if (this._blittableFieldsPtr == null)
            {
                return;
            }

            this.CopyBlittableFields();
        }
#endif

        #endregion // Unity Events

        // ----------------------------------------------------

        #region // Public Methods

        public void Initialize(EntityManager entityManager)
        {
            Assert.IsFalse(this.RootBones == null || this.RootBones.Count <= 0);
            this._entityManager = entityManager;

            // コライダーの初期化
            if (this.ColliderGroups != null && this.ColliderGroups.Length > 0)
            {
                foreach (var collider in this.ColliderGroups)
                {
                    collider.Initialize(this._entityManager);
                }

                this._colliderGroupInstanceIDs = new NativeArray<int>(this.ColliderGroups.Length, Allocator.Persistent);
                for (var i = 0; i < this._colliderGroupInstanceIDs.Length; i++)
                {
                    this._colliderGroupInstanceIDs[i] = this.ColliderGroups[i].GetInstanceID();
                }
            }

            this._centerEntity = Entity.Null;
            if (this.m_center != null)
            {
                var centerEntity = GetOrAddComponent<GameObjectEntity>(this.m_center.gameObject);
                this._centerEntity = centerEntity.Entity;
                this._entityManager.AddComponentData(
                    this._centerEntity, new Center() {Value = this.m_center.localToWorldMatrix});
            }

            // 各種パラメータ用のメモリ確保 + 初期化
            {
                this._blittableFieldsPtr =
                    (BlittableFields*) UnsafeUtilityHelper.Malloc<BlittableFields>(Allocator.Persistent);
                this.CopyBlittableFields();
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

        public void Dispose()
        {
            if (this.ColliderGroups != null && this.ColliderGroups.Length > 0)
            {
                foreach (var collider in this.ColliderGroups)
                {
                    collider.Dispose();
                }

                if (this._colliderGroupInstanceIDs.IsCreated)
                {
                    this._colliderGroupInstanceIDs.Dispose();
                }
            }

            if (this._blittableFieldsPtr != null)
            {
                UnsafeUtility.Free(this._blittableFieldsPtr, Allocator.Persistent);
                this._blittableFieldsPtr = null;
            }

            this.Nodes?.Clear();
            this.Nodes = null;
        }

        #endregion // Public Events

        // ----------------------------------------------------

        #region // Private Methods

        void CopyBlittableFields()
        {
            *this._blittableFieldsPtr = new BlittableFields
            {
                StiffnessForce = this.m_stiffnessForce,
                GravityPower = this.m_gravityPower,
                GravityDir = this.m_gravityDir,
                DragForce = this.m_dragForce,
                HitRadius = this.m_hitRadius,

                ColliderGroupInstanceIDs = (this._colliderGroupInstanceIDs.IsCreated)
                    ? (int*) this._colliderGroupInstanceIDs.GetUnsafeReadOnlyPtr()
                    : null,
                ColliderGroupInstanceIDsLength = (this._colliderGroupInstanceIDs.IsCreated)
                    ? this._colliderGroupInstanceIDs.Length
                    : 0,
            };
        }

        void CreateNode(Transform trs, Node parent = null)
        {
            var node = new Node(trs, this._blittableFieldsPtr, this._entityManager, this._centerEntity, parent);
            this.Nodes.Add(node);
            foreach (Transform child in trs)
            {
                this.CreateNode(child, node);
            }
        }

        static T GetOrAddComponent<T>(GameObject obj)
            where T : MonoBehaviour
        {
            var ret = obj.GetComponent<T>();
            if (ret == null)
            {
                ret = obj.AddComponent<T>();
            }

            return ret;
        }

        #endregion // Private Methods
    }
}
