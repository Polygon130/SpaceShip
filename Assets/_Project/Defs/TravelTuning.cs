using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/航程手感", fileName = "航程手感")]
    public class TravelTuning : ScriptableObject
    {
        [Tooltip("脉冲巡航相对常规引擎满出力速度的倍数。战斗里仍用飞行手感，不用这个倍数。")]
        public float pulseOverConventional = 40f;

        [Tooltip("曲速每光年的真实秒数。星图坐标的距离就是光年。")]
        public float secondsPerLightYear = 6f;

        [Tooltip("演示时把曲速和脉冲巡航的等待压到这么多秒以内。0 表示按真实航时走。面板仍写出真实航时。")]
        public float demoSeconds = 1.6f;

        [Tooltip("恒星轨道站离恒星中心的距离，千米。船停在这里，不落到恒星上。")]
        public float stellarStationKm = 500f;

        void OnValidate()
        {
            pulseOverConventional = Mathf.Max(1f, pulseOverConventional);
            secondsPerLightYear = Mathf.Max(0.1f, secondsPerLightYear);
            demoSeconds = Mathf.Max(0f, demoSeconds);
            stellarStationKm = Mathf.Max(50f, stellarStationKm);
        }
    }
}
