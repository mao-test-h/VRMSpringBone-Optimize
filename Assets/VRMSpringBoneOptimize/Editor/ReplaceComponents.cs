namespace VRM.Optimize.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using VRMSpringBone = VRM.VRMSpringBone;
#if ENABLE_JOB_SPRING_BONE
    using JobVRMSpringBone = VRM.Optimize.Jobs.VRMSpringBoneJobs;
    using JobVRMSpringBoneColliderGroup = VRM.Optimize.Jobs.VRMSpringBoneColliderGroupJobs;
#endif
#if ENABLE_ECS_SPRING_BONE
    using EntitiesVRMSpringBone = VRM.Optimize.Entities.VRMSpringBoneEntity;
    using EntitiesVRMSpringBoneColliderGroup = VRM.Optimize.Entities.VRMSpringBoneColliderGroupEntity;
#endif

    // MEMO: Assembly-CSharp-Editor.dllの配下に置いてある理由について。
    // こちらはVRM.SpringBone等を参照する都合上、Assembly-CSharp-Editorの配下に置く必要があった。
    // UniVRM v0.50からはasmdef対応されたので、VRM.asmdefの参照を紐付けてやれば
    // こちらの拡張スクリプト自体にもasmdefは設定可能となるが、
    // 非対応の環境(旧バージョンのUniVRMを使用している等)も考慮して敢えて設定しないままとしている。
    // 特にasmdef化出来ない理由はないので、必要な方は必要に応じて設定してしまっても問題はない。

    public static class ReplaceComponents
    {
        static void SetReplaceMethods(Action<IReadOnlyList<VRMSpringBone>> replaceMethod)
        {
            var models = GameObject.FindObjectsOfType<VRMMeta>();
            EditorApplication.delayCall += () =>
            {
                foreach (var obj in models)
                {
                    var springBones = obj.GetComponentsInChildren<VRMSpringBone>();
                    replaceMethod(springBones);
                }
            };
        }

#if ENABLE_JOB_SPRING_BONE
        [MenuItem("VRMSpringBoneOptimize/Replace SpringBone Components - Jobs")]
        static void ReplaceJobComponents()
        {
            SetReplaceMethods((springBones) =>
            {
                foreach (var oldComponent in springBones)
                {
                    var newComponent = oldComponent.gameObject.AddComponent<JobVRMSpringBone>();
                    newComponent.m_stiffnessForce = oldComponent.m_stiffnessForce;
                    newComponent.m_gravityPower = oldComponent.m_gravityPower;
                    newComponent.m_gravityDir = oldComponent.m_gravityDir;
                    newComponent.m_dragForce = oldComponent.m_dragForce;
                    newComponent.m_center = oldComponent.m_center;
                    newComponent.RootBones = oldComponent.RootBones;
                    newComponent.m_hitRadius = oldComponent.m_hitRadius;

                    // VRMSpringBoneColliderGroupの情報をコピー
                    var oldColliders = oldComponent.ColliderGroups;
                    var newColliders = new JobVRMSpringBoneColliderGroup[oldColliders.Length];
                    for (var j = 0; j < oldColliders.Length; j++)
                    {
                        var oldCollider = oldColliders[j];
                        var newCollider = oldCollider.gameObject.GetComponent<JobVRMSpringBoneColliderGroup>();
                        if (newCollider == null)
                        {
                            newCollider = oldCollider.gameObject.AddComponent<JobVRMSpringBoneColliderGroup>();
                            var oldSphereColliders = oldCollider.Colliders;
                            var newSphereColliders =
                                new JobVRMSpringBoneColliderGroup.SphereCollider[oldSphereColliders.Length];
                            for (var k = 0; k < oldSphereColliders.Length; k++)
                            {
                                newSphereColliders[k] = new JobVRMSpringBoneColliderGroup.SphereCollider
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

                foreach (var oldComponent in springBones)
                {
                    var oldColliders = oldComponent.ColliderGroups;
                    foreach (var t in oldColliders)
                    {
                        GameObject.DestroyImmediate(t);
                    }

                    GameObject.DestroyImmediate(oldComponent);
                }
            });
        }
#endif

#if ENABLE_ECS_SPRING_BONE
        [MenuItem("VRMSpringBoneOptimize/Replace SpringBone Components - Entities")]
        static void ReplaceEntitiesComponents()
        {
            SetReplaceMethods((springBones) =>
            {
                foreach (var oldComponent in springBones)
                {
                    var newComponent = oldComponent.gameObject.AddComponent<EntitiesVRMSpringBone>();
                    newComponent.m_stiffnessForce = oldComponent.m_stiffnessForce;
                    newComponent.m_gravityPower = oldComponent.m_gravityPower;
                    newComponent.m_gravityDir = oldComponent.m_gravityDir;
                    newComponent.m_dragForce = oldComponent.m_dragForce;
                    newComponent.m_center = oldComponent.m_center;
                    newComponent.RootBones = oldComponent.RootBones;
                    newComponent.m_hitRadius = oldComponent.m_hitRadius;

                    // VRMSpringBoneColliderGroupの情報をコピー
                    var oldColliders = oldComponent.ColliderGroups;
                    var newColliders = new EntitiesVRMSpringBoneColliderGroup[oldColliders.Length];
                    for (var j = 0; j < oldColliders.Length; j++)
                    {
                        var oldCollider = oldColliders[j];
                        var newCollider = oldCollider.gameObject.GetComponent<EntitiesVRMSpringBoneColliderGroup>();
                        if (newCollider == null)
                        {
                            newCollider = oldCollider.gameObject.AddComponent<EntitiesVRMSpringBoneColliderGroup>();
                            var oldSphereColliders = oldCollider.Colliders;
                            var newSphereColliders =
                                new EntitiesVRMSpringBoneColliderGroup.SphereCollider[oldSphereColliders.Length];
                            for (var k = 0; k < oldSphereColliders.Length; k++)
                            {
                                newSphereColliders[k] = new EntitiesVRMSpringBoneColliderGroup.SphereCollider
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

                foreach (var oldComponent in springBones)
                {
                    var oldColliders = oldComponent.ColliderGroups;
                    foreach (var t in oldColliders)
                    {
                        GameObject.DestroyImmediate(t);
                    }

                    GameObject.DestroyImmediate(oldComponent);
                }
            });
        }
#endif
    }
}
