using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/货物", fileName = "货物")]
    public class GoodDef : ScriptableObject
    {
        [Tooltip("稳定编号，和显示名可以相同。生成市场时用它认货。")]
        public string goodId = "水";

        [Tooltip("面板上的名字。")]
        public string displayName = "水";

        [Tooltip("基准价格。成交价 = 基准 × 库存压力 × 税 × 事件 × 相位。")]
        public float basePrice = 10f;

        [Tooltip("每单位质量，吨。转运还没进货舱质量。")]
        public float massPerUnit = 1f;

        [TextArea]
        [Tooltip("给设计看的备注。")]
        public string note;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(displayName))
                displayName = goodId;
            basePrice = Mathf.Max(0f, basePrice);
            massPerUnit = Mathf.Max(0f, massPerUnit);
        }
    }

    public static class GoodCatalog
    {
        public static readonly string[] Twelve =
        {
            "水", "粮食", "矿石", "燃料", "合金", "零件",
            "药品", "织物", "香料", "军火", "数据核", "稀有气体"
        };
    }
}
