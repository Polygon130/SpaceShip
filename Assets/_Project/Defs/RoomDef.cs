using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/舱室", fileName = "舱室")]
    public class RoomDef : ScriptableObject
    {
        [Tooltip("面板上显示的名字。")]
        public string displayName = "舱室";

        [Tooltip("功能类别。供电、推力和武器是否生效按这个类别读。")]
        public RoomKind kind = RoomKind.Bridge;

        [Tooltip("占用格子宽度。")]
        public int width = 1;

        [Tooltip("占用格子高度。")]
        public int height = 1;

        [Tooltip("只占一格的一半。可以和通道放在同一格，旋转决定占哪一半。")]
        public bool halfCell;

        [Tooltip("质量，吨。断电也计入。")]
        public float mass = 1f;

        [Tooltip("运转耗电。产出舱先上线，其余舱按拼船方案里的顺序分电。")]
        public float powerDraw;

        [Tooltip("供电产出。大于 0 的舱只要放得下就在线。")]
        public float powerOutput;

        [Tooltip("在线时贡献的推力。加速度 = 在线推力 / 总质量 × 飞行手感系数。")]
        public float thrust;

        [Tooltip("在线时贡献的热容量。超频和武器以后从这里扣。")]
        public float heatCapacity;

        [Tooltip("在线时每秒产生的热量。通道和装甲一般为 0。只有落在散热范围内的产热才被盖住。")]
        public float heatOutput;

        [Tooltip("在线时每秒散掉的热量。只覆盖效果范围内的舱。")]
        public float heatDissipation;

        [Tooltip("功能覆盖的格子距离。从本舱任意一格量到目标舱任意一格，斜向也算。0 表示只影响自己。")]
        public int effectRange;

        [Tooltip("这一舱每一格的装甲。动能先扣装甲，再决定穿不穿得进。")]
        public float armor;

        [Tooltip("在线时可以隔住漏气。")]
        public bool sealsAir;

        [Tooltip("在线时对效果范围内的舱做损管。")]
        public bool suppressesFire;

        [Tooltip("武器舱引用的武器文档。其他舱留空。")]
        public WeaponDef weapon;

        [Tooltip("拼船面板里的色块。只用于分辨舱室。")]
        public Color swatch = new Color(0.45f, 0.5f, 0.55f, 1f);

        void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (halfCell)
            {
                width = 1;
                height = 1;
            }
            effectRange = Mathf.Max(0, effectRange);
            heatOutput = Mathf.Max(0f, heatOutput);
        }
    }
}
