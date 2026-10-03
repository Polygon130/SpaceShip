using UnityEditor;
using UnityEngine;

namespace SpaceShip.EditorTools
{
    [CustomEditor(typeof(HullDef))]
    public class HullDefEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var hull = (HullDef)target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
                hull.RemapBlocked(Mathf.Max(1, hull.width), Mathf.Max(1, hull.height));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("船壳", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("亮的是船壳，暗的是切掉的格。面板上排是 y 较大的一侧。", MessageType.None);

            for (int y = hull.height - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < hull.width; x++)
                {
                    int index = y * hull.width + x;
                    bool open = hull.blocked == null || index >= hull.blocked.Length || !hull.blocked[index];
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = open ? new Color(0.45f, 0.55f, 0.62f) : new Color(0.18f, 0.18f, 0.2f);
                    if (GUILayout.Button(open ? "壳" : "空", GUILayout.Width(28), GUILayout.Height(28)))
                    {
                        Undo.RecordObject(hull, "修改船壳");
                        if (hull.blocked == null || hull.blocked.Length != hull.width * hull.height)
                            hull.RemapBlocked(hull.width, hull.height);
                        hull.blocked[index] = open;
                        EditorUtility.SetDirty(hull);
                    }

                    GUI.backgroundColor = previous;
                }

                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
