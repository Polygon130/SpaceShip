using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    public enum PlanetRole
    {
        Wild = 0,
        Farm = 1,
        Mine = 2,
        Industry = 3,
        Port = 4,
        Refinery = 5
    }

    [Serializable]
    public class CorePlanetSlot
    {
        [Tooltip("手写的行星名。")]
        public string planetName = "行星";

        [Tooltip("沿用战斗里的重力井。大型、中型、小型都可以。")]
        public GravityWellDef well;

        [Tooltip("轨道半径，千米。脉冲巡航按这个距离算航时。")]
        public float orbitRadius = 12000f;

        [Tooltip("绕恒星一圈的秒数。星图时钟走，行星就在动。")]
        public float orbitPeriod = 48f;

        [Tooltip("起始相位，0 到 1 是一整圈。")]
        public float orbitPhase;

        [Tooltip("勾上则计入该势力的占领，直到占领上限。")]
        public bool owned = true;

        [Tooltip("勾上则有轨道站。交易只在停靠之后。")]
        public bool station = true;

        [Tooltip("决定外围规则之外，这颗核心行星的产销偏向。")]
        public PlanetRole role = PlanetRole.Port;
    }

    [CreateAssetMenu(menuName = "星际商船/势力核心", fileName = "势力核心")]
    public class FactionCoreTemplate : ScriptableObject
    {
        [Tooltip("这颗核心恒星的名字。恒星不能降落。")]
        public string starName = "核心";

        [Tooltip("星图坐标，光年。手写，生成时不随机挪。")]
        public Vector2 mapPosition;

        [Tooltip("核心行星。外围行星另按种子撒。")]
        public List<CorePlanetSlot> planets = new List<CorePlanetSlot>();

        public static string RoleLabel(PlanetRole role)
        {
            if (role == PlanetRole.Farm)
                return "农";
            if (role == PlanetRole.Mine)
                return "矿";
            if (role == PlanetRole.Industry)
                return "工";
            if (role == PlanetRole.Port)
                return "港";
            if (role == PlanetRole.Refinery)
                return "炼";
            return "野";
        }
    }
}
