namespace VRM.Optimize.Entities
{
    using UnityEngine;
    using Unity.Entities;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public sealed unsafe class VRMSpringBoneColliderGroupEntity : MonoBehaviour, System.IDisposable
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

        EntityManager _entityManager;
        
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

            this.CopyBlittableFields();
        }
#endif

        #endregion // Unity Events

        // ----------------------------------------------------

        #region // Public Methods

        public void Initialize(EntityManager entityManager)
        {
            if (!this.IsActive) return;
            this._entityManager = entityManager;

            // コライダー用のNativeArrayを確保
            if (!this._blittableFieldsArray.IsCreated)
            {
                this._blittableFieldsArray =
                    new NativeArray<BlittableFields>(this.Colliders.Length, Allocator.Persistent);
                this.CopyBlittableFields();
            }
        }

        public Entity CreateEntity()
        {
            if (this._gameObjectEntity != null)
            {
                return this.Entity;
            }

            this._gameObjectEntity = this.gameObject.GetComponent<GameObjectEntity>();
            if (this._gameObjectEntity == null)
            {
                this._gameObjectEntity = this.gameObject.AddComponent<GameObjectEntity>();
            }
            this.Entity = this._gameObjectEntity.Entity;

            var entity = this.Entity;
            this._entityManager.AddComponentData(
                entity, new ColliderGroupInstanceID {Value = this.GetInstanceID()});
            this._entityManager.AddComponentData(
                entity, new ColliderGroupBlittableFieldsPtr()
                {
                    Value = (BlittableFields*) this._blittableFieldsArray.GetUnsafeReadOnlyPtr(),
                    Length = this._blittableFieldsArray.Length,
                });
            return entity;
        }

        public void Dispose()
        {
            if (this._blittableFieldsArray.IsCreated)
            {
                this._blittableFieldsArray.Dispose();
            }
        }

        #endregion // Public Methods

        // ----------------------------------------------------

        #region // Private Methods

        void CopyBlittableFields()
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
