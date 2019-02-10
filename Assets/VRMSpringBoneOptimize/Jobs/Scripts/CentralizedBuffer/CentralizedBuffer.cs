namespace VRM.Optimize.Jobs
{
    using System.Collections.Generic;
    using UnityEngine;
    using IDisposable = System.IDisposable;

    public sealed class CentralizedBuffer : MonoBehaviour, IDisposable
    {
        // ------------------------------

        #region // Properties

        // Components References
        public VRMSpringBone[] SpringBones { get; private set; }
        public List<VRMSpringBone> UpdateCenterBones { get; private set; }
        public List<VRMSpringBoneColliderGroup> ColliderGroups { get; } = new List<VRMSpringBoneColliderGroup>();

        public List<VRMSpringBone.Node> AllNodes { get; } = new List<VRMSpringBone.Node>();
        public int ColliderHashMapLength { get; private set; }

        #endregion // Properties


        // ----------------------------------------------------

        #region // Public Methods

        public void Initialize()
        {
            // VRMSpringBoneの初期化
            this.SpringBones = this.GetComponents<VRMSpringBone>();
            foreach (var springBone in this.SpringBones)
            {
                springBone.Initialize();
                this.AllNodes.AddRange(springBone.Nodes);

                // m_centerを持つ物を保持
                if (springBone.m_center != null)
                {
                    if (this.UpdateCenterBones == null)
                    {
                        this.UpdateCenterBones = new List<VRMSpringBone>();
                    }

                    this.UpdateCenterBones.Add(springBone);
                }

                // SpringBoneに登録されている全コライダーの取得
                // →同じコライダーが参照されている時があるので重複は取り除く
                foreach (var collider in springBone.ColliderGroups)
                {
                    if (collider.Colliders == null || collider.Colliders.Length <= 0)
                    {
                        continue;
                    }

                    if (this.ColliderGroups.Contains(collider))
                    {
                        continue;
                    }

                    this.ColliderGroups.Add(collider);
                }

                // VRMSpringBoneColliderGroupの初期化
                foreach (var collider in this.ColliderGroups)
                {
                    collider.Initialize();
                    this.ColliderHashMapLength += collider.BlittableFieldsArray.Length;
                }
            }
        }

        public void Dispose()
        {
            foreach (var springBone in this.SpringBones)
            {
                springBone.Dispose();
            }

            foreach (var collider in this.ColliderGroups)
            {
                collider.Dispose();
            }

            UpdateCenterBones?.Clear();
        }

        #endregion // Public Methods
    }
}
