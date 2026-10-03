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
            Vector2 direction = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.up;
            float speed = DepartureSpeed(weapon);
            return new MissileState
            {
                position = muzzle,
                velocity = shipVelocity + direction * speed
            };
        }

        public static bool Step(ref MissileState missile, Vector2 target, WeaponDef weapon, float dt)
        {
            if (weapon == null)
                return false;
            missile.age += dt;
            if (weapon.lifetime > 0f && missile.age > weapon.lifetime)
                return false;

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

            Boost(ref missile, weapon, dt);
            missile.position += missile.velocity * dt;
            return true;
        }

        static void Boost(ref MissileState missile, WeaponDef weapon, float dt)
        {
            float full = weapon.muzzleSpeed;
            float start = DepartureSpeed(weapon);
            float boost = weapon.boostTime;
            if (boost <= 0.05f || full <= start + 0.01f)
                return;
            float previous = Mathf.Clamp01((missile.age - dt) / boost);
            float now = Mathf.Clamp01(missile.age / boost);
            float add = (full - start) * (now - previous);
            if (add <= 0.0001f)
                return;
            Vector2 facing = missile.velocity.sqrMagnitude > 0.0001f ? missile.velocity.normalized : Vector2.up;
            missile.velocity += facing * add;
        }

        static float DepartureSpeed(WeaponDef weapon)
        {
            if (weapon == null)
                return 0f;
            float full = Mathf.Max(0f, weapon.muzzleSpeed);
            if (weapon.boostTime <= 0.05f)
                return full;
            return Mathf.Min(full, Mathf.Max(4f, full * 0.12f));
        }
    }
}
