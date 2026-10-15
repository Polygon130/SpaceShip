using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/星图规则", fileName = "星图规则")]
    public class GalaxyRules : ScriptableObject
    {
        [Tooltip("规则版本。快照生成时抄下来。之后改规则不会改已有快照。")]
        public int rulesVersion = 1;

        [Tooltip("种子。同一份规则和同一个种子，预览两次应得到同一指纹。")]
        public int seed = 13000;

        [Tooltip("行星总数，含核心和外围。默认大约 100。")]
        public int planetTarget = 100;

        [Tooltip("启用的主要势力数量。按势力列表里的顺序取。可以后再改。")]
        public int majorCount = 4;

        [Tooltip("启用的次要势力数量。按势力列表里的顺序取。")]
        public int minorCount = 4;

        [Tooltip("势力文档。主要在前、次要在后时最好认。")]
        public List<FactionDef> factions = new List<FactionDef>();

        [Tooltip("外围行星抽的重力井。沿用战斗里的小型、中型、大型。")]
        public List<GravityWellDef> wells = new List<GravityWellDef>();

        [Tooltip("和重力井一一对应的权重。缺了就平均抽。")]
        public List<float> wellWeights = new List<float>();

        [Tooltip("已经做成文档的货物。十二种里先做这一子集。")]
        public List<GoodDef> goods = new List<GoodDef>();

        [Tooltip("四种航路文档：官道、私航、走私、过期。")]
        public List<RouteDef> routeKinds = new List<RouteDef>();

        [Tooltip("开局所在势力的编号。它的核心恒星是已发现航路的起点。")]
        public string startFactionId = "航政署";

        [Tooltip("星图半径，光年。外围恒星撒在这个圆里。")]
        public float galaxyRadius = 90f;

        [Tooltip("恒星之间至少空开这么多光年。手写核心靠得更近时会记一条警告，但不挪位置。")]
        public float minStarSpacing = 7f;

        [Tooltip("一颗外围恒星至少几颗行星。")]
        public int peripheryMinPlanets = 2;

        [Tooltip("一颗外围恒星至多几颗行星。最后一颗恒星会把剩余行星收完。")]
        public int peripheryMaxPlanets = 6;

        [Tooltip("无主行星带轨道站的概率。")]
        public float wildStationChance = 0.35f;

        [Tooltip("无主星系之间的走私航路条数。")]
        public int smugglerRoutes = 6;

        [Tooltip("从走私航路里改成过期的条数。至少留一条走私。")]
        public int expiredRoutes = 4;

        [Tooltip("无主星域的税乘数。")]
        public float wildTax = 1f;

        [Tooltip("曲速、脉冲巡航和常规引擎的速度关系。")]
        public TravelTuning travel;

        void OnValidate()
        {
            rulesVersion = Mathf.Max(1, rulesVersion);
            planetTarget = Mathf.Clamp(planetTarget, 8, 400);
            majorCount = Mathf.Clamp(majorCount, 0, 12);
            minorCount = Mathf.Clamp(minorCount, 0, 12);
            peripheryMinPlanets = Mathf.Max(1, peripheryMinPlanets);
            peripheryMaxPlanets = Mathf.Max(peripheryMinPlanets, peripheryMaxPlanets);
            wildStationChance = Mathf.Clamp01(wildStationChance);
            smugglerRoutes = Mathf.Max(0, smugglerRoutes);
            expiredRoutes = Mathf.Max(0, expiredRoutes);
            galaxyRadius = Mathf.Max(10f, galaxyRadius);
            minStarSpacing = Mathf.Max(1f, minStarSpacing);
            wildTax = Mathf.Max(0.05f, wildTax);
        }
    }
}
