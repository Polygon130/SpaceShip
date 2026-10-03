using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    [Serializable]
    public class PlanetPlacement
    {
        [Tooltip("这颗行星的重力井。")]
        public GravityWellDef well;

        [Tooltip("行星中心，米。")]
        public Vector2 position;
    }

    [CreateAssetMenu(menuName = "星际商船/战场", fileName = "战场")]
    public class CombatArenaDef : ScriptableObject
    {
        [Tooltip("玩家开场使用的拼船方案。")]
        public ShipBuild playerBuild;

        [Tooltip("对战船的拼船方案。和玩家用同一套舱室文档。")]
        public ShipBuild enemyBuild;

        [Tooltip("两边共用的飞行手感。")]
        public FlightTuning flight;

        [Tooltip("场上的行星。每项是一份重力井和它的中心。")]
        public List<PlanetPlacement> planets = new List<PlanetPlacement>();

        [Tooltip("这场战斗使用的武器。舱室上的武器引用应指向同一份。")]
        public WeaponDef weapon;

        [Tooltip("拼船面板左侧可以拖动的舱室。")]
        public System.Collections.Generic.List<RoomDef> palette = new System.Collections.Generic.List<RoomDef>();
    }
}
