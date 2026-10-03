using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/重力井", fileName = "重力井")]
    public class GravityWellDef : ScriptableObject
    {
        [Tooltip("面板上显示的名字。")]
        public string displayName = "行星";

        [Tooltip("行星质量。引力和重力井范围按它放大。侧着掠过时，弯是这条引力自己积出来的。")]
        public float mass = 8f;

        [Tooltip("引力常数。加速度 = 引力常数 × 质量 / 距离²，距离单位米。")]
        public float gravityConstant = 91340f;

        [Tooltip("井宽，米。影响半径 = 井宽 × 质量。此范围外不拉船。")]
        public float wellSpan = 183f;

        [Tooltip("行星半径，米。画面上的星球就是这个大小，引力计算也不会比这更近。")]
        public float floorRadius = 315f;

        [Tooltip("星球颜色。")]
        public Color bodyColor = new Color(0.376f, 0.306f, 0.227f, 1f);

        public float influenceRadius
        {
            get { return wellSpan * Mathf.Max(0f, mass); }
        }

        public float AccelerationAt(float distance)
        {
            if (mass <= 0f || distance > influenceRadius)
                return 0f;
            float radius = Mathf.Max(floorRadius, distance);
            return gravityConstant * mass / (radius * radius);
        }
    }
}
