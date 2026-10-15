using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpaceShip.EditorTools
{
    [CustomEditor(typeof(GalaxyRules))]
    public class GalaxyRulesEditor : UnityEditor.Editor
    {
        string preview = "";

        public override void OnInspectorGUI()
        {
            var rules = (GalaxyRules)target;
            EditorGUILayout.HelpBox("核心恒星和核心行星用势力上的手写模板。外围按种子随机。预览不写快照。同一规则和种子，预览两次指纹应相同。读档不会重新生成。", MessageType.None);
            if (rules.majorCount < 4 || rules.majorCount > 5 || rules.minorCount < 4 || rules.minorCount > 5)
                EditorGUILayout.HelpBox("主要、次要势力现在不在 4 到 5 个。可以先这样调，默认是各 4 个，列表里准备了 5 个。", MessageType.Info);
            EditorGUILayout.LabelField("十二种货物", string.Join("、", GoodCatalog.Twelve));
            EditorGUILayout.LabelField("已做文档", AuthoredGoods(rules));
            if (GUILayout.Button("预览这颗种子"))
                preview = RunPreview(rules);
            if (!string.IsNullOrEmpty(preview))
                EditorGUILayout.HelpBox(preview, MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        static string AuthoredGoods(GalaxyRules rules)
        {
            if (rules.goods == null || rules.goods.Count == 0)
                return "还没有";
            var names = new string[rules.goods.Count];
            int count = 0;
            for (int i = 0; i < rules.goods.Count; i++)
            {
                if (rules.goods[i] == null)
                    continue;
                names[count] = rules.goods[i].displayName;
                count++;
            }

            if (count == 0)
                return "还没有";
            var text = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    text.Append("、");
                text.Append(names[i]);
            }

            return text.ToString();
        }

        static string RunPreview(GalaxyRules rules)
        {
            string error = WorldGen.Validate(rules);
            if (error != null)
                return error;
            WorldSnapshot first = WorldGen.Build(rules);
            WorldSnapshot second = WorldGen.Build(rules);
            if (first == null || second == null)
            {
                if (first != null)
                    Object.DestroyImmediate(first);
                if (second != null)
                    Object.DestroyImmediate(second);
                return "预览没有生成。";
            }

            var text = new StringBuilder();
            text.AppendLine(first.summary);
            text.AppendLine("指纹 " + first.fingerprint);
            if (first.fingerprint == second.fingerprint)
                text.AppendLine("同一规则和种子，两次预览指纹相同。");
            else
                text.AppendLine("两次预览指纹不同。");
            text.AppendLine(WorldGen.OwnershipReport(first));
            if (first.audit != null)
            {
                for (int i = 0; i < first.audit.Count; i++)
                    text.AppendLine(first.audit[i]);
            }

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            return text.ToString().TrimEnd();
        }
    }
}
