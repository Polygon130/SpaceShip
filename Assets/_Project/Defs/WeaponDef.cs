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

        [Tooltip("炮台基准命中，0 到 1。火炮最终命中 = 基准 × 操作者 × 横截 × 锁定 × 距离衰减 × 近距加成。横截只看相对速度。操作者还没进战斗时按 1。")]
        [Range(0f, 1f)] public float baseHit = 0.75f;

        [Tooltip("距离衰减。贴脸为 1，打到射程尽头时命中再乘这个数。越小，远距离越难打中。中间按距离线性下降。")]
        [Range(0f, 1f)] public float rangeHit = 0.25f;

        [Tooltip("近距加成。贴脸再乘这个数，到近距范围尽头降回 1。大于 1 才能把贴脸命中抬过基准。")]
        [Range(1f, 3f)] public float closeHit = 1.5f;

        [Tooltip("近距加成的作用距离，米。贴脸用满加成，到这个距离加成降到 1。")]
        public float closeRange = 48f;

        [Tooltip("锁定进度每秒增加多少。突然改向或开关超频时，战斗逻辑会把锁定打回去。")]
        public float lockPerSecond = 0.35f;

        [Tooltip("穿甲。炮弹逐格飞过。有装甲的格子消耗 1 点，装甲为 0 的格子不消耗。用完就停在这一格。还有剩余就飞出另一侧，可以打中后面的船。空格子按默认舱室的装甲算。")]
        public float penetration = 3f;

        [Tooltip("打进舱室时的结构伤害。导弹在爆炸半径里也用这个伤害。")]
        public float damage = 12f;

        [Tooltip("勾上之后，弹体飞行中会转向目标。电磁炮不勾。")]
        public bool homing;

        [Tooltip("追踪时每秒最多转过的角度。越大越咬得住，越小越容易冲出弯。")]
        public float homingTurnRate;

        [Tooltip("导弹离舰后先无动力滑行的时间，秒。这段只继承战舰当时的速度，不转向、不加速。0 表示一离舰就点火。火炮不用这项。")]
        public float coastTime = 1f;

        [Tooltip("导弹点火之后，加到最快速度要用的时间，秒。0 表示一点火就到最快。火炮不用这项。")]
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
