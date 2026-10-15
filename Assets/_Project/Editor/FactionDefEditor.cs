using UnityEditor;
using UnityEngine;

namespace SpaceShip.EditorTools
{
    [CustomEditor(typeof(FactionDef))]
    public class FactionDefEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var faction = (FactionDef)target;
            EditorGUILayout.HelpBox(
                faction.rank == FactionRank.Minor
                    ? "次要势力固定占领 1 颗行星。核心模板里多勾的占领会在生成时改成无主。"
                    : "主要势力最多占领 5 颗行星。数量以后可以改，生成时不会超过这个上限。",
                MessageType.None);
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("生成时的占领上限", faction.Cap + " 颗");
            if (faction.core == null)
            {
                EditorGUILayout.HelpBox("还没有核心模板。没有模板的势力不会放恒星。", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("核心恒星", faction.core.starName + "  (" + faction.core.mapPosition.x.ToString("0.0") + ", " + faction.core.mapPosition.y.ToString("0.0") + ")");
            if (faction.core.planets == null)
                return;
            int owned = 0;
            for (int i = 0; i < faction.core.planets.Count; i++)
            {
                CorePlanetSlot slot = faction.core.planets[i];
                if (slot == null)
                    continue;
                bool claim = slot.owned && owned < faction.Cap;
                if (claim)
                    owned++;
                string mark = claim ? "占领" : "无主";
                string station = slot.station ? "有站" : "无站";
                EditorGUILayout.LabelField(slot.planetName, mark + "  " + station + "  " + FactionCoreTemplate.RoleLabel(slot.role));
            }
        }
    }
}
