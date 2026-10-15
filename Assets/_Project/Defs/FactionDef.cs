using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    public enum FactionRank
    {
        Major = 0,
        Minor = 1
    }

    [CreateAssetMenu(menuName = "星际商船/势力", fileName = "势力")]
    public class FactionDef : ScriptableObject
    {
        [Tooltip("稳定编号。星图快照里的占领用它。")]
        public string factionId = "势力";

        [Tooltip("面板和星图上的名字。")]
        public string displayName = "势力";

        [Tooltip("主要势力最多占领 5 颗行星。次要势力固定 1 颗。")]
        public FactionRank rank = FactionRank.Major;

        [Tooltip("星图上的颜色。")]
        public Color color = new Color(0.45f, 0.58f, 0.75f, 1f);

        [Tooltip("税乘数。成交价里的税默认用它。1 是不抽成。")]
        public float tax = 1.1f;

        [Tooltip("占领上限。主要势力会被钳在 1 到 5。次要势力固定为 1。")]
        public int ownedCap = 5;

        [Tooltip("手写的核心恒星和核心行星。外围行星不从这里长出来。")]
        public FactionCoreTemplate core;

        [Tooltip("这个势力出产、愿意往外卖的货。市场库存会偏高。")]
        public List<GoodDef> exports = new List<GoodDef>();

        [Tooltip("这个势力短缺、愿意买进的货。市场库存会偏低。")]
        public List<GoodDef> imports = new List<GoodDef>();

        public int Cap
        {
            get { return rank == FactionRank.Minor ? 1 : Mathf.Clamp(ownedCap, 1, 5); }
        }

        public static string RankLabel(FactionRank value)
        {
            return value == FactionRank.Minor ? "次要" : "主要";
        }

        void OnValidate()
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = factionId;
            tax = Mathf.Max(0.05f, tax);
            if (rank == FactionRank.Minor)
                ownedCap = 1;
            else
                ownedCap = Mathf.Clamp(ownedCap, 1, 5);
        }
    }
}
