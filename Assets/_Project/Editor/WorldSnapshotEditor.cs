using UnityEditor;
using UnityEngine;

namespace SpaceShip.EditorTools
{
    [CustomEditor(typeof(WorldSnapshot))]
    public class WorldSnapshotEditor : UnityEditor.Editor
    {
        static bool showRaw;

        public override void OnInspectorGUI()
        {
            var world = (WorldSnapshot)target;
            EditorGUILayout.HelpBox("打开这份快照不会重新生成。要换种子，改规则后点下面的按钮，变更记录会留下。", MessageType.None);
            if (world.rules != null && world.generated && world.rulesVersion != world.rules.rulesVersion)
                EditorGUILayout.HelpBox("规则版本已经改过。这张快照仍是版本 " + world.rulesVersion + "。", MessageType.Warning);
            if (!string.IsNullOrEmpty(world.summary))
                EditorGUILayout.LabelField(world.summary, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(world.fingerprint))
                EditorGUILayout.LabelField("指纹", world.fingerprint);
            if (!world.generated)
            {
                if (GUILayout.Button("按种子生成并写入"))
                    Write(world, false);
            }
            else if (GUILayout.Button("强制重生成"))
            {
                bool go = EditorUtility.DisplayDialog("重新生成星图", "会换掉恒星、行星、航路和车站库存。变更记录保留，并追加一条。", "重生成", "取消");
                if (go)
                    Write(world, true);
            }

            if (world.audit != null)
            {
                for (int i = 0; i < world.audit.Count; i++)
                    EditorGUILayout.HelpBox(world.audit[i], MessageType.Warning);
            }

            if (world.changelog != null && world.changelog.Count > 0)
            {
                EditorGUILayout.LabelField("变更记录", EditorStyles.boldLabel);
                for (int i = 0; i < world.changelog.Count; i++)
                {
                    ChangeEntry entry = world.changelog[i];
                    if (entry != null)
                        EditorGUILayout.LabelField(entry.order + ". " + entry.summary, EditorStyles.wordWrappedLabel);
                }
            }

            showRaw = EditorGUILayout.Foldout(showRaw, "快照字段");
            if (showRaw)
                DrawDefaultInspector();
        }

        static void Write(WorldSnapshot world, bool force)
        {
            Undo.RecordObject(world, force ? "重生成星图" : "生成星图");
            string error = WorldGen.TryWrite(world.rules, world, force);
            if (error != null)
            {
                EditorUtility.DisplayDialog("星图", error, "好");
                return;
            }

            EditorUtility.SetDirty(world);
            AssetDatabase.SaveAssets();
        }
    }
}
