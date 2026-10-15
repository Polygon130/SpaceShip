using UnityEngine;

namespace SpaceShip
{
    public enum RouteKind
    {
        Official = 0,
        Private = 1,
        Smuggler = 2,
        Expired = 3
    }

    [CreateAssetMenu(menuName = "星际商船/航路", fileName = "航路")]
    public class RouteDef : ScriptableObject
    {
        [Tooltip("面板上的名字。")]
        public string displayName = "官道";

        [Tooltip("官道、私航、走私、过期。生成星图时按这个种类写航路。")]
        public RouteKind kind = RouteKind.Official;

        [Tooltip("过期航路关掉。曲速只走勾了这项的已发现航路。")]
        public bool allowsWarp = true;

        [Tooltip("叠在势力税上。走私可以低于 1。成交价 = 基准 × 库存压力 × 税 × 事件 × 相位。")]
        public float taxMultiplier = 1f;

        [TextArea]
        [Tooltip("这条种类怎么进海图。")]
        public string summary;

        public static string Label(RouteKind value)
        {
            if (value == RouteKind.Private)
                return "私航";
            if (value == RouteKind.Smuggler)
                return "走私";
            if (value == RouteKind.Expired)
                return "过期";
            return "官道";
        }

        public static RouteDef Find(GalaxyRules rules, RouteKind kind)
        {
            if (rules == null || rules.routeKinds == null)
                return null;
            for (int i = 0; i < rules.routeKinds.Count; i++)
            {
                RouteDef route = rules.routeKinds[i];
                if (route != null && route.kind == kind)
                    return route;
            }

            return null;
        }

        public static bool AllowsWarp(GalaxyRules rules, RouteKind kind)
        {
            RouteDef route = Find(rules, kind);
            if (route != null)
                return route.allowsWarp;
            return kind != RouteKind.Expired;
        }

        public static float TaxMultiplier(GalaxyRules rules, RouteKind kind)
        {
            RouteDef route = Find(rules, kind);
            if (route == null || route.taxMultiplier <= 0f)
                return 1f;
            return route.taxMultiplier;
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = Label(kind);
            taxMultiplier = Mathf.Max(0.05f, taxMultiplier);
            if (kind == RouteKind.Expired)
                allowsWarp = false;
        }
    }
}
