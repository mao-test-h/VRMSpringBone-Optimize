#if ENABLE_ECS_SPRING_BONE_V2
namespace VRM.ECS_SpringBone.Editor
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(ECS_SpringBoneManager))]
    public sealed class ECS_SpringBoneManagerEditor : Editor
    {
        GameObject _deleteTarget = null;
        Stack<Vector3> _deletePositions = new Stack<Vector3>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var target = base.target as ECS_SpringBoneManager;

            for (int i = 0; i < 3; ++i) { EditorGUILayout.Space(); }
            EditorGUILayout.LabelField("【Tools】", EditorStyles.boldLabel);
            if (GUILayout.Button("シーン中のVRMSpringBoneをInitRegisterSpringBoneに登録"))
            {
                var refs = GameObject.FindObjectsOfType<VRMSpringBone>();
                target.InitRegisterSpringBones = refs;
                Debug.Log("Complete Register.");
            }
            if (GUILayout.Button("「VRM.SpringBone/ColliderGroup」をECS用のコンポーネントに置き換え"))
            {
                this.ReplaceComponents(GameObject.FindObjectsOfType<VRM.VRMSpringBone>());
                Debug.Log("Complete Replace Component.");
            }


            for (int i = 0; i < 3; ++i) { EditorGUILayout.Space(); }
            EditorGUILayout.LabelField("【Test】", EditorStyles.boldLabel);

            if (GUILayout.Button("モデル追加テスト"))
            {
                Vector3 createPos = new Vector3(0, 3f, 0);
                if (this._deletePositions.Count > 0)
                {
                    createPos = this._deletePositions.Pop();
                }
                var instance = Instantiate<GameObject>(target.AddTestModel, createPos, Quaternion.identity);
                instance.tag = "VRM";
                instance.transform.SetAsLastSibling();

                this.ReplaceComponents(instance.GetComponentsInChildren<VRM.VRMSpringBone>());
                var springBones = instance.GetComponentsInChildren<VRM.ECS_SpringBone.VRMSpringBone>();
                foreach (var springBone in springBones)
                {
                    target.AddSpringBone(springBone);
                }
            }

            this._deleteTarget = EditorGUILayout.ObjectField("削除対象", this._deleteTarget, typeof(GameObject), allowSceneObjects: true) as GameObject;
            if (GUILayout.Button("モデル削除テスト"))
            {
                var springBones = this._deleteTarget.GetComponentsInChildren<VRMSpringBone>();
                foreach (var springBone in springBones)
                {
                    target.RemoveSpringBone(springBone);
                }
                this._deletePositions.Push(this._deleteTarget.transform.position);
                Destroy(this._deleteTarget);
            }
        }

        void ReplaceComponents(VRM.VRMSpringBone[] springBones)
        {

            for (int i = 0; i < springBones.Length; i++)
            {
                // VRMSpringBoneの情報をコピー
                var oldComponent = springBones[i];
                var newComponent = oldComponent.gameObject.AddComponent<VRM.ECS_SpringBone.VRMSpringBone>();
                newComponent.m_stiffnessForce = oldComponent.m_stiffnessForce;
                newComponent.m_gravityPower = oldComponent.m_gravityPower;
                newComponent.m_gravityDir = oldComponent.m_gravityDir;
                newComponent.m_dragForce = oldComponent.m_dragForce;
                newComponent.m_center = oldComponent.m_center;
                newComponent.RootBones = oldComponent.RootBones;
                newComponent.m_hitRadius = oldComponent.m_hitRadius;

                // VRMSpringBoneColliderGroupの情報をコピー
                var oldColliders = oldComponent.ColliderGroups;
                var newColliders = new VRM.ECS_SpringBone.VRMSpringBoneColliderGroup[oldColliders.Length];
                for (int j = 0; j < oldColliders.Length; j++)
                {
                    var oldCollider = oldColliders[j];
                    var newCollider = oldCollider.gameObject.GetComponent<VRM.ECS_SpringBone.VRMSpringBoneColliderGroup>();
                    if (newCollider == null)
                    {
                        newCollider = oldCollider.gameObject.AddComponent<VRM.ECS_SpringBone.VRMSpringBoneColliderGroup>();
                        var oldSphereColliders = oldCollider.Colliders;
                        var newSphereColliders = new VRM.ECS_SpringBone.VRMSpringBoneColliderGroup.SphereCollider[oldSphereColliders.Length]; ;
                        for (int k = 0; k < oldSphereColliders.Length; k++)
                        {
                            newSphereColliders[k] = new VRM.ECS_SpringBone.VRMSpringBoneColliderGroup.SphereCollider
                            {
                                Offset = oldSphereColliders[k].Offset,
                                Radius = oldSphereColliders[k].Radius,
                            };
                        }
                        newCollider.Colliders = newSphereColliders;
                    }
                    newColliders[j] = newCollider;
                }
                newComponent.ColliderGroups = newColliders;
            }

            for (int i = 0; i < springBones.Length; i++)
            {
                var oldComponent = springBones[i];
                var oldColliders = oldComponent.ColliderGroups;
                for (int j = 0; j < oldColliders.Length; j++)
                {
                    GameObject.DestroyImmediate(oldColliders[j]);
                }
                GameObject.DestroyImmediate(oldComponent);
            }
        }
    }
}
#endif
