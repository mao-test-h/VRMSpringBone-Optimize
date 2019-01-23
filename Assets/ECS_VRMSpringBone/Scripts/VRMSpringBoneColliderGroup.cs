namespace VRM.ECS_SpringBone
{
    using UnityEngine;

    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public unsafe sealed class VRMSpringBoneColliderGroup : MonoBehaviour, System.IDisposable
    {
        // ------------------------------
        #region // Defines

        [System.Serializable]
        public class SphereCollider
        {
            public Vector3 Offset;
            [Range(0, 1.0f)] public float Radius;
        }

        #endregion // Defines

        // ------------------------------
        #region // Fields(Editable)

        public SphereCollider[] Colliders = new SphereCollider[] { new SphereCollider { Radius = 0.1f } };

        #endregion // Fields(Editable)

        // ------------------------------
        #region // Fields

        public float3* PositionPtr = null;
        NativeArray<SphereColliderParam> _sphereColliderParam;

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
            if (!this._sphereColliderParam.IsCreated) { return; }
            this.CopyColliders();
        }
#endif

        #endregion // Unity Events

        // ----------------------------------------------------
        #region // Public Methods

        public void Initialize()
        {
            if (this.Colliders == null || this.Colliders.Length <= 0) { return; }

            // コライダー用のNativeArrayを確保
            if (!this._sphereColliderParam.IsCreated)
            {
                this._sphereColliderParam = new NativeArray<SphereColliderParam>(this.Colliders.Length, Allocator.Persistent);
                this.CopyColliders();
            }

            // 位置同期用のメモリを確保 + 初期化
            if (this.PositionPtr == null)
            {
                this.PositionPtr = (float3*)UnsafeUtilityHelper.Malloc<float3>(Allocator.Persistent);
                *this.PositionPtr = this.transform.position;
            }
        }

        public void Dispose()
        {
            if (this._sphereColliderParam.IsCreated)
            {
                this._sphereColliderParam.Dispose();
            }
            if (this.PositionPtr != null)
            {
                UnsafeUtility.Free(this.PositionPtr, Allocator.Persistent);
                this.PositionPtr = null;
            }
        }

        public unsafe SphereColliderParam* GetSphereColliderParamPtr(int index)
        {
            SphereColliderParam* ptr = (SphereColliderParam*)NativeArrayUnsafeUtility.GetUnsafePtr(this._sphereColliderParam);
            return ptr + index;
        }

        #endregion // Public Methods

        // ----------------------------------------------------
        #region // Private Methods

        void CopyColliders()
        {
            for (int i = 0; i < this.Colliders.Length; i++)
            {
                var collider = this.Colliders[i];
                this._sphereColliderParam[i] = new SphereColliderParam
                {
                    Offset = collider.Offset,
                    Radius = collider.Radius,
                };
            }
        }

        #endregion // Private Methods
    }
}
