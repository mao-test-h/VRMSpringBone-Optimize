namespace VRM.Optimize.Entities
{
    using UnityEngine;
    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public sealed unsafe class VRMSpringBoneColliderGroup : MonoBehaviour, System.IDisposable
    {
        // ------------------------------

        #region // Defines

        public struct BlittableFields
        {
            public float3 Offset;
            public float Radius;
        }

        [System.Serializable]
        public class SphereCollider
        {
            public Vector3 Offset;
            [Range(0, 1.0f)] public float Radius;
        }

        #endregion // Defines

        // ------------------------------

        #region // Fields(Editable)

        public SphereCollider[] Colliders = new SphereCollider[] {new SphereCollider {Radius = 0.1f}};

        #endregion // Fields(Editable)

        // ------------------------------

        #region // Fields

        public Entity Entity { get; private set; }
        public bool IsActive => (this.Colliders != null && this.Colliders.Length > 0);

        NativeArray<BlittableFields> _blittableFieldsArray;
        GameObjectEntity _gameObjectEntity;

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
            if (!this._blittableFieldsArray.IsCreated)
            {
                return;
            }

            this.CopyColliders();
        }
#endif

        #endregion // Unity Events

        // ----------------------------------------------------

        #region // Public Methods

        public void Initialize()
        {
            if (!this.IsActive) return;

            // コライダー用のNativeArrayを確保
            if (!this._blittableFieldsArray.IsCreated)
            {
                this._blittableFieldsArray =
                    new NativeArray<BlittableFields>(this.Colliders.Length, Allocator.Persistent);
                this.CopyColliders();
            }
        }

        public Entity CreateEntity(EntityManager manager)
        {
            if (this._gameObjectEntity != null)
            {
                return this.Entity;
            }
            this._gameObjectEntity = this.gameObject.AddComponent<GameObjectEntity>();
            this.Entity = this._gameObjectEntity.Entity;

            var entity = this.Entity;
            manager.AddComponentData(
                entity, new ColliderGroupTag());
            manager.AddComponentData(
                entity, new Position() {Value = this.transform.position});
            return entity;
        }

        public void Dispose()
        {
            if (this._blittableFieldsArray.IsCreated)
            {
                this._blittableFieldsArray.Dispose();
            }
        }

        public BlittableFields* GetBlittableFieldsPtr(int index)
        {
            var ptr = (BlittableFields*) this._blittableFieldsArray.GetUnsafePtr();
            return ptr + index;
        }

        #endregion // Public Methods

        // ----------------------------------------------------

        #region // Private Methods

        void CopyColliders()
        {
            for (var i = 0; i < this.Colliders.Length; i++)
            {
                var collider = this.Colliders[i];
                this._blittableFieldsArray[i] = new BlittableFields
                {
                    Offset = collider.Offset,
                    Radius = collider.Radius,
                };
            }
        }

        #endregion // Private Methods
    }
}
