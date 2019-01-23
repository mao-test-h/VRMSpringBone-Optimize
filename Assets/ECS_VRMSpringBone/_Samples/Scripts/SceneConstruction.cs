namespace VRM.ECS_SpringBone.Samples
{
    using UnityEngine;

    public sealed class SceneConstruction : MonoBehaviour
    {
        [Header("【Settings】")]
        public GameObject Prefab = null;
        public int Width = 18;
        public int Height = 18;
        public Vector2 Padding = Vector2.zero;
        public int GenerateNum = 5;

        [Header("【Gizmos】")]
        [SerializeField] Color _showAreaColor = Color.black;
        [SerializeField] bool _isDrawGizmos = true;

        /// <summary>
        /// MonoBehaviour.OnDrawGizmos
        /// </summary>
        void OnDrawGizmos()
        {
            if (!this._isDrawGizmos) { return; }
            Gizmos.color = this._showAreaColor;
            Gizmos.DrawCube(Vector3.zero, new Vector3(this.Width, 1f, this.Height));
        }
    }
}

#if UNITY_EDITOR
namespace VRM.ECS_SpringBone.Samples.Editor
{
    using UnityEngine;
    using UnityEditor;

    [CustomEditor(typeof(SceneConstruction))]
    public sealed class SceneConstructionInspector : Editor
    {
        const string VRMTag = "VRM";

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var target = base.target as SceneConstruction;

            if (GUILayout.Button("Generate"))
            {
                var generateNum = target.GenerateNum;

                float startX = -(target.Width / 2f);
                float startY = -(target.Height / 2f);
                float spaceX = ((float)target.Width / (float)generateNum) + target.Padding.x;
                float spaceY = ((float)target.Height / (float)generateNum) + target.Padding.y;

                for (int i = 0; i < generateNum * generateNum; i++)
                {
                    int x = i % generateNum;
                    int y = i / generateNum;

                    if (x <= 0 && y >= 1)
                    {
                        startX = -(target.Width / 2f);
                        startY += spaceY;
                    }

                    var instance = Instantiate<GameObject>(target.Prefab);
                    instance.tag = VRMTag;
                    var trs = instance.transform;
                    trs.localScale = Vector3.one;
                    trs.position = new Vector3(startX, 0, startY);
                    trs.SetAsLastSibling();

                    startX += spaceX;
                }
            }

            if(GUILayout.Button("Clear"))
            {
                var objs = GameObject.FindGameObjectsWithTag(VRMTag);
                for (int i = 0; i < objs.Length; i++)
                {
                    DestroyImmediate(objs[i]);
                }
            }
        }
    }
}
#endif
