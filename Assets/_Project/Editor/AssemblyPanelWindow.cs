using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip.EditorTools
{
    public class AssemblyPanelWindow : EditorWindow
    {
        const string PanelPath = "Assets/_Project/UI/AssemblyPanel.uxml";
        const string StylePath = "Assets/_Project/UI/AssemblyPanel.uss";
        const string ArenaPath = "Assets/_Project/Content/Slice/战场.asset";

        ShipBuild build;
        FlightTuning flight;
        List<RoomDef> catalog = new List<RoomDef>();
        ObjectField buildField;

        [MenuItem("星际商船/拼船面板")]
        public static void Open()
        {
            GetWindow<AssemblyPanelWindow>("拼船");
        }

        [MenuItem("星际商船/打开战场场景")]
        public static void OpenArenaScene()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/CombatArena.unity");
        }

        void OnEnable()
        {
            var arena = AssetDatabase.LoadAssetAtPath<CombatArenaDef>(ArenaPath);
            if (build == null && arena != null)
                build = arena.playerBuild;
            if (arena != null)
                flight = arena.flight;
            ReloadCatalog();
        }

        void CreateGUI()
        {
            buildField = new ObjectField("拼船方案")
            {
                objectType = typeof(ShipBuild),
                value = build
            };
            buildField.RegisterValueChangedCallback(evt =>
            {
                build = evt.newValue as ShipBuild;
                Rebuild();
            });
            rootVisualElement.Add(buildField);

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PanelPath);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label("找不到 " + PanelPath));
                return;
            }

            tree.CloneTree(rootVisualElement);
            if (sheet != null)
                rootVisualElement.styleSheets.Add(sheet);
            Rebuild();
        }

        void ReloadCatalog()
        {
            catalog.Clear();
            string[] guids = AssetDatabase.FindAssets("t:RoomDef");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var room = AssetDatabase.LoadAssetAtPath<RoomDef>(path);
                if (room != null)
                    catalog.Add(room);
            }

            catalog.Sort((a, b) => ((int)a.kind).CompareTo((int)b.kind));
        }

        void Rebuild()
        {
            if (buildField != null && buildField.value != build)
                buildField.SetValueWithoutNotify(build);
            AssemblyPanelView.Bind(rootVisualElement, build, flight, catalog, true, BeforeMutate);
        }

        void BeforeMutate()
        {
            if (build == null)
                return;
            Undo.RecordObject(build, "拼船");
            EditorUtility.SetDirty(build);
        }
    }
}
