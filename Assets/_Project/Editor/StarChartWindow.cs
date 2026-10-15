using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip.EditorTools
{
    public class StarChartWindow : EditorWindow
    {
        public const string SnapshotPath = "Assets/_Project/Content/Campaign/星图快照.asset";
        public const string ScenePath = "Assets/_Project/Scenes/CampaignChart.unity";

        WorldSnapshot snapshot;
        CampaignTravel travel;
        CampaignMapView view;
        double lastTime;

        [MenuItem("星际商船/星图")]
        public static void Open()
        {
            GetWindow<StarChartWindow>("星图");
        }

        [MenuItem("星际商船/打开星图场景")]
        public static void OpenScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        }

        void OnEnable()
        {
            EditorApplication.update += OnEditorTick;
            lastTime = EditorApplication.timeSinceStartup;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorTick;
        }

        void CreateGUI()
        {
            snapshot = AssetDatabase.LoadAssetAtPath<WorldSnapshot>(SnapshotPath);
            rootVisualElement.Clear();
            if (snapshot == null)
            {
                rootVisualElement.Add(new Label("找不到 " + SnapshotPath));
                return;
            }

            travel = new CampaignTravel();
            FlightTuning flight = null;
            ShipBuild ship = null;
            if (snapshot.rules == null)
            {
                rootVisualElement.Add(new Label("快照没有星图规则。"));
                return;
            }

            var arena = AssetDatabase.LoadAssetAtPath<CombatArenaDef>("Assets/_Project/Content/Slice/战场.asset");
            if (arena != null)
            {
                flight = arena.flight;
                ship = arena.playerBuild;
            }

            travel.Begin(snapshot, flight, ship);
            view = new CampaignMapView(rootVisualElement, travel, true, null, Generate, MarkDirty, null);
            view.Live();
        }

        void Generate()
        {
            if (snapshot == null || snapshot.rules == null)
                return;
            Undo.RecordObject(snapshot, "生成星图");
            string error = WorldGen.TryWrite(snapshot.rules, snapshot, false);
            if (error != null)
            {
                EditorUtility.DisplayDialog("星图", error, "好");
                return;
            }

            EditorUtility.SetDirty(snapshot);
            AssetDatabase.SaveAssets();
            CreateGUI();
        }

        void MarkDirty()
        {
            if (snapshot == null)
                return;
            EditorUtility.SetDirty(snapshot);
        }

        void OnEditorTick()
        {
            if (travel == null || view == null || travel.inCombat)
                return;
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastTime);
            lastTime = now;
            if (dt < 0f || dt > 0.1f)
                dt = 0.016f;
            travel.Tick(dt);
            view.Live();
            rootVisualElement.MarkDirtyRepaint();
        }
    }
}
