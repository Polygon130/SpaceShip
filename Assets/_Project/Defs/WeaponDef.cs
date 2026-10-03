using UnityEngine;

namespace SpaceShip
{
    public enum WeaponFamily
    {
        Ballistic = 0,
        Energy = 1,
        Missile = 2
    }

    public enum WeaponEffect
    {
        Penetrate = 0,
        Blast = 1,
        Electromagnetic = 2,
        Combined = 3
    }

    [CreateAssetMenu(menuName = "星际商船/武器", fileName = "武器")]
    public class WeaponDef : ScriptableObject
    {
        [Tooltip("面板上显示的名字。")]
        public string displayName = "电磁炮";

        [Tooltip("相对船头的射出速度，米/秒。世界速度 = 舰速 + 这个速度。")]
        public float muzzleSpeed = 80f;

        [Tooltip("两次射击之间的间隔，秒。")]
        public float cooldown = 0.8f;

        [Tooltip("炮台基准命中，0 到 1。火炮最终命中 = 基准 × 操作者 × 横截 × 锁定。横截由距离和相对速度决定。操作者还没进战斗时按 1。")]
        [Range(0f, 1f)] public float baseHit = 0.75f;

        [Tooltip("锁定进度每秒增加多少。突然改向或开关超频时，战斗逻辑会把锁定打回去。")]
        public float lockPerSecond = 0.35f;

        [Tooltip("穿透值。每穿一格消耗一次，气阀关上的隔舱额外消耗。")]
        public float penetration = 3f;

        [Tooltip("打进舱室时的结构伤害。导弹在爆炸半径里也用这个伤害。")]
        public float damage = 12f;

        [Tooltip("勾上之后，弹体飞行中会转向目标。电磁炮不勾。")]
        public bool homing;

        [Tooltip("追踪时每秒最多转过的角度。越大越咬得住，越小越容易冲出弯。")]
        public float homingTurnRate;

        [Tooltip("导弹离舰后，从慢速加到最快速度要用的时间，秒。0 表示一出发就是最快。火炮不用这项。")]
        public float boostTime = 1.5f;

        [Tooltip("飞行超过这么多秒就自毁。0 表示不限时。")]
        public float lifetime;

        [Tooltip("射程，米。目标到炮口的距离超过这个数就不开火。")]
        public float range;

        [Tooltip("爆炸半径，格。动能弹填 0。")]
        public float blastRadius;

        [Tooltip("能量没有弹道时间。火炮有飞行速度。导弹不看舱口朝向，进射程就能发射。")]
        public WeaponFamily family;

        [Tooltip("穿透、爆炸、电磁、综合。具体差别以后再接入战斗。")]
        public WeaponEffect effect;
    }
}
