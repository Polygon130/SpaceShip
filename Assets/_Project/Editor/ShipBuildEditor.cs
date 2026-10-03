using UnityEditor;
using UnityEngine;

namespace SpaceShip.EditorTools
{
    [CustomEditor(typeof(ShipBuild))]
    public class ShipBuildEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var build = (ShipBuild)target;
            ShipReport report = ShipEvaluator.Evaluate(build);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("装配结果", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("质量", report.mass.ToString("0.#") + " 吨");
            EditorGUILayout.LabelField("在线推力", report.thrust.ToString("0.#"));
            EditorGUILayout.LabelField("供电", report.powerDraw.ToString("0.#") + " / " + report.powerOutput.ToString("0.#"));
            EditorGUILayout.LabelField("产热 / 散热", report.heatOutput.ToString("0.#") + " / " + report.heatDissipation.ToString("0.#") + "，未覆盖 " + report.heatUncovered.ToString("0.#"));
            EditorGUILayout.LabelField("武器 / 导弹 / 通道", report.weapons + " / " + report.missiles + " / " + report.corridors);
            EditorGUILayout.LabelField("气阀 / 损管", report.valves + " / 覆盖 " + report.damageCovered + " 舱");
            for (int i = 0; i < report.errors.Count; i++)
                EditorGUILayout.HelpBox(report.errors[i], MessageType.Warning);
            if (GUILayout.Button("在拼船面板中打开"))
            {
                AssemblyPanelWindow.Open();
            }
        }
    }
}
