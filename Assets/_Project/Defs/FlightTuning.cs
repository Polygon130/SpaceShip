using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/飞行手感", fileName = "飞行手感")]
    public class FlightTuning : ScriptableObject
    {
        [Tooltip("加速度 = 在线推力 / 总质量 × 此系数。单位：米/秒²。玩家货船满档大约每秒 10 米。减速也是沿喷口推，同样按这个加速度。")]
        public float accelPerThrustOverMass = 32f;

        [Tooltip("有档位在推时，船头转过的角度里已有速度跟着转的比例。0 是完全不跟。1 是转到哪飞到哪。空档没有推力，转船头不改速度方向。推进器被打掉后也不跟。")]
        [Range(0f, 1f)]
        public float velocityFollowTurn = 0.5f;

        [Tooltip("推进有几档。0 档是空档。最高档不限速。下面每一档的速度上限 = 档位 / 档数 × 引擎满出力速度。")]
        public int gearCount = 4;

        [Tooltip("按住 W 或 S 时，每隔这么多秒再切一档。点一下只切一档。")]
        public float gearRepeat = 0.5f;

        [Tooltip("战斗时间流速。1 是原速。1/3 时船、转向、引力、炮弹都按真实时间放慢到三分之一，地图距离不变。")]
        public float timeScale = 1f / 3f;

        public float Acceleration(float thrust, float mass)
        {
            if (mass <= 0.01f)
                return 0f;
            return thrust / mass * accelPerThrustOverMass;
        }

    }
}
