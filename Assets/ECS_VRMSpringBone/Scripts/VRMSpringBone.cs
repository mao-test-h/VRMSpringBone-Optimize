namespace VRM.ECS_SpringBone
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Assertions;

    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public unsafe sealed class VRMSpringBone : MonoBehaviour, System.IDisposable
    {
        // ------------------------------
        #region // Defines

        public sealed unsafe class Node : System.IDisposable
        {
            public Transform Transform { get; private set; } = null;
            public Node Parent { get; private set; } = null;
            public float3* PositionPtr { get; private set; } = null;
            public quaternion* RotationPtr { get; private set; } = null;
            public float3* CurrentTailPtr { get; private set; } = null;

            public Entity Entity { get; set; }

            public Node(Transform trs, Node parent = null)
            {
                this.Transform = trs;
                this.Parent = parent;

                if (this.Parent == null)
                {
                    // 親が存在する場合には親のCurrentTailPtrのポインタがComponentDataに渡されるので割当は不要
                    this.PositionPtr = (float3*)UnsafeUtilityHelper.Malloc<float3>(Allocator.Persistent);
                    *this.PositionPtr = this.Transform.position;
                }
                this.RotationPtr = (quaternion*)UnsafeUtilityHelper.Malloc<quaternion>(Allocator.Persistent);
                *this.RotationPtr = this.Transform.rotation;

                this.CurrentTailPtr = (float3*)UnsafeUtilityHelper.Malloc<float3>(Allocator.Persistent);
                *this.CurrentTailPtr = new float3(0f);
            }

            public void Dispose()
            {
                if (this.PositionPtr != null)
                {
                    UnsafeUtility.Free(this.PositionPtr, Allocator.Persistent);
                    this.PositionPtr = null;
                }
                UnsafeUtility.Free(this.RotationPtr, Allocator.Persistent);
                this.RotationPtr = null;
                UnsafeUtility.Free(this.CurrentTailPtr, Allocator.Persistent);
                this.CurrentTailPtr = null;
            }
        }

        #endregion // Defines

        // ------------------------------
        #region // Fields(Editable)

        public string m_comment;

        [Header("Settings")]
        [Range(0, 4)] public float m_stiffnessForce = 1.0f;
        [Range(0, 2)] public float m_gravityPower = 0;
        public Vector3 m_gravityDir = new Vector3(0, -1.0f, 0);
        [Range(0, 1)] public float m_dragForce = 0.4f;
        public Transform m_center;
        public List<Transform> RootBones = new List<Transform>();

        [Header("Collider")]
        [Range(0, 0.5f)] public float m_hitRadius = 0.02f;
        public VRMSpringBoneColliderGroup[] ColliderGroups;

        #endregion // Fields(Editable)

        // ------------------------------
        #region // Fields

        public VRMSpringBoneParam* VRMSpringBoneParamPtr { get; private set; } = null;
        public List<Node> Nodes { get; private set; } = null;
        public Entity ColliderIdentifyEntity;
        public Entity[] ColliderGroupEntities = null;

        public SyncTransformSOA SyncTransformSOA { get; private set; } = null;

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
            if (this.VRMSpringBoneParamPtr == null) { return; }
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
                this.VRMSpringBoneParamPtr = (VRMSpringBoneParam*)UnsafeUtilityHelper.Malloc<VRMSpringBoneParam>(Allocator.Persistent);
                this.CopySpringBoneParam();
            }

            this.Nodes = new List<Node>();
            foreach (var go in RootBones)
            {
                if (go == null) { continue; }
                this.CreateNode(go);
            }

            // Transform同期用データ
            var transforms = new List<Transform>();
            var posPtrs = new List<PositionPtr>();
            var rotPtrs = new List<RotationPtr>();
            foreach (var node in this.Nodes)
            {
                transforms.Add(node.Transform);
                posPtrs.Add(new PositionPtr { Value = node.PositionPtr });
                rotPtrs.Add(new RotationPtr { Value = node.RotationPtr });
                foreach (var collider in this.ColliderGroups)
                {
                    transforms.Add(collider.transform);
                    posPtrs.Add(new PositionPtr { Value = collider.PositionPtr });
                    rotPtrs.Add(new RotationPtr { Value = null });
                }
            }
            this.SyncTransformSOA = new SyncTransformSOA(transforms.ToArray(), posPtrs.ToArray(), rotPtrs.ToArray());
        }

        public void Dispose()
        {
            // コライダーの初期化
            foreach (var collider in this.ColliderGroups)
            {
                collider.Dispose();
            }

            if (this.VRMSpringBoneParamPtr != null)
            {
                UnsafeUtility.Free(this.VRMSpringBoneParamPtr, Allocator.Persistent);
                this.VRMSpringBoneParamPtr = null;
            }
            if (this.Nodes != null)
            {
                foreach (var node in this.Nodes)
                {
                    node.Dispose();
                }
                this.Nodes.Clear();
            }
            this.Nodes = null;

            if (this.SyncTransformSOA != null)
            {
                this.SyncTransformSOA.Dispose();
                this.SyncTransformSOA = null;
            }
        }

        #endregion // Public Events

        // ----------------------------------------------------
        #region // Private Methods

        void CopySpringBoneParam()
        {
            *this.VRMSpringBoneParamPtr = new VRMSpringBoneParam
            {
                StiffnessForce = this.m_stiffnessForce,
                GravityPower = this.m_gravityPower,
                GraviryDir = this.m_gravityDir,
                DragForce = this.m_dragForce,
                HitRadius = this.m_hitRadius,
            };
        }

        void CreateNode(Transform trs, Node parent = null)
        {
            var node = new Node(trs, parent);
            this.Nodes.Add(node);
            foreach (Transform child in trs)
            {
                this.CreateNode(child, node);
            }
        }

        #endregion // Private Methods
    }
}
