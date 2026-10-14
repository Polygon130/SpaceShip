using UnityEngine;

namespace SpaceShip
{
    public struct MissileState
    {
        public Vector2 position;
        public Vector2 velocity;
        public float age;
    }

    public static class MissileFlight
    {
        public static MissileState Launch(Vector2 muzzle, Vector2 shipVelocity, Vector2 aimDirection, WeaponDef weapon)
        {
            return new MissileState
            {
                position = muzzle,
                velocity = shipVelocity
            };
        }

        public static bool Step(ref MissileState missile, Vector2 target, WeaponDef weapon, float dt, float speedScale = 1f)
        {
            if (weapon == null)
                return false;
            missile.age += dt;
            if (weapon.lifetime > 0f && missile.age > weapon.lifetime)
                return false;

            float coast = CoastTime(weapon);
            if (missile.age > coast)
            {
                float speed = missile.velocity.magnitude;
                if (weapon.homing && speed > 0.01f)
                {
                    Vector2 desired = target - missile.position;
                    if (desired.sqrMagnitude > 0.0001f)
                    {
                        float maxRadians = weapon.homingTurnRate * Mathf.Deg2Rad * dt;
                        Vector3 turned = Vector3.RotateTowards(missile.velocity / speed, desired.normalized, maxRadians, 0f);
                        missile.velocity = (Vector2)turned * speed;
                    }
                }

                Boost(ref missile, target, weapon, dt, speedScale);
            }

            missile.position += missile.velocity * dt;
            return true;
        }

        static void Boost(ref MissileState missile, Vector2 target, WeaponDef weapon, float dt, float speedScale)
        {
            float full = Mathf.Max(0f, weapon.muzzleSpeed) * Mathf.Clamp01(speedScale);
            if (full <= 0.01f)
                return;
            float coast = CoastTime(weapon);
            float boost = Mathf.Max(0f, weapon.boostTime);
            float poweredPrev = Mathf.Max(0f, missile.age - dt - coast);
            float poweredNow = Mathf.Max(0f, missile.age - coast);
            float add;
            if (boost <= 0.05f)
                add = poweredPrev <= 0.0001f ? full : 0f;
            else
                add = full * (Mathf.Clamp01(poweredNow / boost) - Mathf.Clamp01(poweredPrev / boost));
            if (add <= 0.0001f)
                return;
            Vector2 facing = missile.velocity.sqrMagnitude > 0.0001f
                ? missile.velocity.normalized
                : (target - missile.position).sqrMagnitude > 0.0001f ? (target - missile.position).normalized : Vector2.up;
            missile.velocity += facing * add;
        }

        static float CoastTime(WeaponDef weapon)
        {
            if (weapon == null || weapon.coastTime <= 0.01f)
                return 0f;
            return weapon.coastTime;
        }
    }
}
