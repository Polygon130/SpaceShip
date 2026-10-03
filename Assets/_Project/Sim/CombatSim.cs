using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    public sealed class CombatShot
    {
        public Vector2 position;
        public Vector2 velocity;
        public float damage;
        public float age;
        public float lifetime;
        public int owner;
        public WeaponDef weapon;
        public bool guide;
        public float missSide;
    }

    public enum AttackFocus
    {
        None,
        Power,
        Weapon,
        Drive
    }

    public enum ShipOrder
    {
        Manual,
        Track,
        Parallel,
        Orbit,
        BreakOff
    }


    public sealed class WellPass
    {
        public bool inside;
        public bool closing;
        public bool flung;
    }

    public sealed class CombatShip
    {
        public ShipBuild build;
        public string name;
        public bool player;
        public Vector2 position;
        public Vector2 velocity;
        public float heading;
        public float angularVelocity;
        public float structure;
        public float structureMax;
        public float cellStructureMax;
        public float[] cellStructure;
        public float[] heat;
        public float[] cellArmor;
        public float[] weaponCooldown;
        public float[] weaponLock;
        public float thrustNow;
        public bool destroyed;
        public bool hitPlanet;
        public WellPass[] wells;

        public float Speed
        {
            get { return velocity.magnitude; }
        }
    }

    public sealed class CombatSim
    {
        public const float CellMeters = 2f;

        public CombatShip player;
        public CombatShip enemy;
        public readonly List<CombatShot> shots = new List<CombatShot>();
        public FlightTuning flight;
        public readonly List<PlanetPlacement> planets = new List<PlanetPlacement>();
        public bool finished;
        public string outcome = "";
        public int gear;
        public int gearSpan = 4;
        public float thrustLevel;
        public float brakeLevel;
        public float turnCommand;
        public ShipOrder order;
        public bool fireEnergy = true;
        public bool fireBallistic = true;
        public bool fireMissile = true;
        public AttackFocus attackFocus;
        public float orderRange = 1000f;
        public static readonly float[] OrderRanges = { 200f, 500f, 1000f, 2000f, 5000f, 10000f };
        float formationSign = 1f;
        public int enemyMode;
        public bool combat;
        public bool enemyIntent;
        public bool playerEngaging;
        public bool enemyEngaging;
        public float enemyThrust;
        public float enemyBrake;
        float enemyThink;
        float enemyJink = 1f;
        float enemyJinkFlip = 2.4f;

        public static CombatSim Start(CombatArenaDef arena)
        {
            var sim = new CombatSim();
            sim.flight = arena.flight;
            if (arena.planets != null)
            {
                for (int i = 0; i < arena.planets.Count; i++)
                {
                    PlanetPlacement placement = arena.planets[i];
                    if (placement != null && placement.well != null)
                        sim.planets.Add(placement);
                }
            }
            sim.player = CreateShip(arena.playerBuild, "玩家", true, new Vector2(0f, 300f), 180f);
            sim.enemy = CreateShip(arena.enemyBuild, "敌方", false, new Vector2(0f, -300f), 0f);
            BindWells(sim.player, sim.planets.Count);
            BindWells(sim.enemy, sim.planets.Count);
            return sim;
        }

        static void BindWells(CombatShip ship, int count)
        {
            if (ship == null)
                return;
            ship.wells = new WellPass[count];
            for (int i = 0; i < count; i++)
                ship.wells[i] = new WellPass();
        }

        public static string OrderLabel(ShipOrder kind)
        {
            if (kind == ShipOrder.Track)
                return "跟踪";
            if (kind == ShipOrder.Parallel)
                return "平行";
            if (kind == ShipOrder.Orbit)
                return "环绕";
            if (kind == ShipOrder.BreakOff)
                return "脱离";
            return "手动";
        }

        public string FireStatus()
        {
            string text = "能量" + (fireEnergy ? "开" : "停")
                + "  火炮" + (fireBallistic ? "开" : "停")
                + "  导弹" + (fireMissile ? "开" : "停");
            if (attackFocus == AttackFocus.Power)
                text += "  优先能源";
            else if (attackFocus == AttackFocus.Weapon)
                text += "  优先武器";
            else if (attackFocus == AttackFocus.Drive)
                text += "  优先动力";
            return text;
        }

        public void SetAttackFocus(AttackFocus focus)
        {
            attackFocus = attackFocus == focus ? AttackFocus.None : focus;
        }

        public void SetAllFire(bool on)
        {
            fireEnergy = on;
            fireBallistic = on;
            fireMissile = on;
        }

        public void SetFamilyFire(WeaponFamily family, bool on)
        {
            if (family == WeaponFamily.Energy)
                fireEnergy = on;
            else if (family == WeaponFamily.Missile)
                fireMissile = on;
            else
                fireBallistic = on;
        }

        public bool FamilyFiring(WeaponFamily family)
        {
            if (family == WeaponFamily.Energy)
                return fireEnergy;
            if (family == WeaponFamily.Missile)
                return fireMissile;
            return fireBallistic;
        }

        public static string RangeLabel(float meters)
        {
            if (meters >= 1000f)
            {
                float km = meters / 1000f;
                if (Mathf.Abs(km - Mathf.Round(km)) < 0.05f)
                    return Mathf.RoundToInt(km) + "公里";
                return km.ToString("0.#") + "公里";
            }

            return Mathf.RoundToInt(meters) + "米";
        }

        public void SetMoveOrder(ShipOrder next, float range)
        {
            bool fresh = next != order;
            order = next;
            if (range > 1f)
                orderRange = range;
            if (fresh)
                formationSign = PickFormationSign();
        }

        public void ReleaseMoveOrder()
        {
            order = ShipOrder.Manual;
        }

        public void Step(float dt, float thrustLevel, float brakeLevel, float turn)
        {
            this.thrustLevel = thrustLevel;
            this.brakeLevel = brakeLevel;
            turnCommand = turn;
            Advance(dt);
        }

        public void StepOrdered(float dt)
        {
            float step = Mathf.Clamp(dt, 0f, 0.05f);
            if (!finished && step > 0f)
                DriveOrder(step);
            Advance(dt);
        }

        void Advance(float dt)
        {
            if (finished)
                return;
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            if (flight != null)
                dt *= Mathf.Clamp(flight.timeScale, 0.02f, 1f);
            if (dt <= 0f)
                return;

            StepShip(player, dt, thrustLevel, brakeLevel, turnCommand, true);
            StepEnemy(dt);
            StepShots(dt);
            if (player.destroyed || enemy.destroyed)
            {
                finished = true;
                if (player.hitPlanet)
                    outcome = "撞上了行星。";
                else if (enemy.hitPlanet && !player.destroyed)
                    outcome = "敌船撞上了行星。";
                else if (player.destroyed && enemy.destroyed)
                    outcome = "两船都解体了。";
                else if (player.destroyed)
                    outcome = "你的船解体了。";
                else
                    outcome = "敌船解体了。";
            }
        }

        void DriveOrder(float dt)
        {
            thrustLevel = 0f;
            brakeLevel = 0f;
            turnCommand = 0f;
            if (player == null || player.destroyed || enemy == null || enemy.destroyed || flight == null || order == ShipOrder.Manual)
                return;
            ShipReport report = ShipEvaluator.Evaluate(player.build);
            ApplyCombatPower(player, report);
            bool reactorUp = ReactorUp(player, report);
            float thrustOffset = ThrustOffset(player, report, reactorUp);
            float thrustAngle = player.heading + thrustOffset;
            float accel = ShipDriveAccel(player, report, reactorUp);
            float want;
            float thrust;
            float brake;
            Vector2 escape;
            if (PlanetThreat(player, out escape))
            {
                want = Mathf.Atan2(escape.x, escape.y) * Mathf.Rad2Deg;
                thrust = AlignThrust(thrustAngle, want);
                brake = 0f;
            }
            else
            {
                SteerOrder(thrustAngle, accel, out want, out thrust, out brake);
            }

            float headingWant = want - thrustOffset;
            float orderRate = OrderTurnRate(player);
            float delta = Mathf.DeltaAngle(player.heading, headingWant);
            float turn = 0f;
            if (orderRate > 0.01f && Mathf.Abs(delta) > 0.8f)
                turn = Mathf.Clamp(delta / Mathf.Max(dt, 0.01f), -orderRate, orderRate) / Mathf.Max(1f, TurnRate(player));
            if (brake > 0.01f)
                thrust = 0f;
            thrustLevel = thrust;
            brakeLevel = brake;
            turnCommand = turn;
        }

        void SteerOrder(float thrustAngle, float accel, out float want, out float thrust, out float brake)
        {
            Vector2 travel = enemy.velocity.sqrMagnitude > 9f ? enemy.velocity.normalized : Forward(enemy.heading);
            if (order == ShipOrder.Orbit)
            {
                SteerOrbit(thrustAngle, travel, accel, out want, out thrust, out brake);
                return;
            }

            Vector2 slot;
            Vector2 face;
            if (order == ShipOrder.Parallel)
            {
                Vector2 side = new Vector2(travel.y, -travel.x) * formationSign;
                slot = enemy.position + side * orderRange;
                face = travel;
            }
            else if (order == ShipOrder.BreakOff)
            {
                Vector2 away = player.position - enemy.position;
                float gap = away.magnitude;
                if (gap < 1f)
                    away = -travel;
                else
                    away /= gap;
                face = away;
                if (gap >= orderRange - 20f)
                {
                    Vector2 hold = enemy.velocity;
                    float closingIn = Vector2.Dot(enemy.velocity - player.velocity, away);
                    if (closingIn > 1f)
                        hold += away * Mathf.Min(100f, closingIn + 16f);
                    BurnToward(hold, face, thrustAngle, out want, out thrust, out brake);
                    return;
                }

                slot = enemy.position + away * orderRange;
            }
            else
            {
                slot = enemy.position - travel * orderRange;
                face = travel;
            }

            HoldSlot(slot, enemy.velocity, face, thrustAngle, accel, out want, out thrust, out brake);
        }

        void SteerOrbit(float thrustAngle, Vector2 travel, float accel, out float want, out float thrust, out float brake)
        {
            Vector2 radial = player.position - enemy.position;
            float dist = radial.magnitude;
            Vector2 outDir = dist > 1f ? radial / dist : new Vector2(-travel.y, travel.x);
            Vector2 tangent = new Vector2(outDir.y, -outDir.x) * formationSign;
            float radialSpeed = Vector2.Dot(player.velocity - enemy.velocity, outDir);
            float radialError = dist - orderRange;
            float cruise = Mathf.Clamp(orderRange * 0.08f, 18f, 80f);
            float toward = radialError >= 0f ? -radialSpeed : radialSpeed;
            float allowed = SpeedRoomAllows(player, Mathf.Abs(radialError), accel);
            float radialCmd = Mathf.Clamp(-radialError * 0.55f - radialSpeed * 1.1f, -70f, 70f);
            if (toward > allowed)
                radialCmd = radialError >= 0f ? -allowed : allowed;
            Vector2 desired = enemy.velocity + tangent * cruise + outDir * radialCmd;
            BurnToward(desired, tangent, thrustAngle, out want, out thrust, out brake);
        }

        void HoldSlot(Vector2 slot, Vector2 match, Vector2 face, float thrustAngle, float accel, out float want, out float thrust, out float brake)
        {
            Vector2 error = slot - player.position;
            float dist = error.magnitude;
            float closeCap = Mathf.Max(220f, match.magnitude + 90f);
            float close = Mathf.Min(closeCap, Mathf.Max(dist * 0.7f, 3.2f * Mathf.Sqrt(Mathf.Max(dist, 0f))));
            close = Mathf.Min(close, SpeedRoomAllows(player, dist, accel));
            Vector2 desired = match;
            if (dist > 1f)
                desired += error / dist * close;
            Vector2 nose = face.sqrMagnitude > 0.01f ? face : error;
            BurnToward(desired, nose, thrustAngle, out want, out thrust, out brake);
        }

        void BurnToward(Vector2 desiredVel, Vector2 face, float thrustAngle, out float want, out float thrust, out float brake)
        {
            brake = 0f;
            Vector2 burn = desiredVel - player.velocity;
            if (burn.sqrMagnitude < 1.44f)
            {
                Vector2 nose = face.sqrMagnitude > 0.01f ? face : Forward(player.heading);
                want = Mathf.Atan2(nose.x, nose.y) * Mathf.Rad2Deg;
                thrust = 0f;
                return;
            }

            want = Mathf.Atan2(burn.x, burn.y) * Mathf.Rad2Deg;
            float dot = Vector2.Dot(Forward(thrustAngle), burn.normalized);
            if (dot <= 0.08f)
                thrust = 0f;
            else if (dot >= 0.5f)
                thrust = 1f;
            else
                thrust = 0.75f;
        }

        float PickFormationSign()
        {
            if (player == null || enemy == null)
                return 1f;
            Vector2 radial = player.position - enemy.position;
            Vector2 fwd = enemy.velocity.sqrMagnitude > 9f ? enemy.velocity.normalized : Forward(enemy.heading);
            Vector2 side = new Vector2(fwd.y, -fwd.x);
            if (order == ShipOrder.Orbit)
            {
                Vector2 outDir = radial.sqrMagnitude > 1f ? radial.normalized : Vector2.up;
                Vector2 tangent = new Vector2(outDir.y, -outDir.x);
                float tangential = Vector2.Dot(player.velocity - enemy.velocity, tangent);
                return tangential >= 0f ? 1f : -1f;
            }

            if (radial.sqrMagnitude < 1f)
                return 1f;
            return Vector2.Dot(radial, side) >= 0f ? 1f : -1f;
        }

        static CombatShip CreateShip(ShipBuild build, string name, bool player, Vector2 position, float heading)
        {
            var ship = new CombatShip();
            ship.build = build;
            ship.name = name;
            ship.player = player;
            ship.position = position;
            ship.heading = heading;
            int rooms = build != null ? build.rooms.Count : 0;
            ship.heat = new float[rooms];
            ship.weaponCooldown = new float[rooms];
            ship.weaponLock = new float[rooms];
            HullDef hull = build != null ? build.hull : null;
            int cells = hull != null ? hull.width * hull.height : 0;
            ship.cellArmor = new float[cells];
            ship.cellStructure = new float[cells];
            ShipReport report = ShipEvaluator.Evaluate(build);
            ship.structureMax = hull != null ? hull.baseStructure : 0f;
            if (hull == null)
                return ship;
            int open = 0;
            for (int y = 0; y < hull.height; y++)
            {
                for (int x = 0; x < hull.width; x++)
                {
                    if (hull.IsOpen(x, y))
                        open++;
                }
            }

            ship.cellStructureMax = open > 0 ? ship.structureMax / open : 0f;
            for (int y = 0; y < hull.height; y++)
            {
                for (int x = 0; x < hull.width; x++)
                {
                    if (!hull.IsOpen(x, y))
                        continue;
                    ship.cellStructure[y * hull.width + x] = ship.cellStructureMax;
                }
            }

            ship.structure = ship.cellStructureMax * open;
            for (int i = 0; i < rooms; i++)
            {
                if (i >= report.valid.Count || !report.valid[i])
                    continue;
                RoomPlacement placement = build.rooms[i];
                if (placement == null || placement.room == null || placement.room.armor <= 0f)
                    continue;
                for (int y = 0; y < placement.SpanY; y++)
                {
                    for (int x = 0; x < placement.SpanX; x++)
                    {
                        int cellX = placement.x + x;
                        int cellY = placement.y + y;
                        if (!hull.IsOpen(cellX, cellY))
                            continue;
                        ship.cellArmor[cellY * hull.width + cellX] = placement.room.armor;
                    }
                }
            }

            return ship;
        }

        void StepShip(CombatShip ship, float dt, float thrustLevel, float brakeLevel, float turn, bool allowFire)
        {
            if (ship == null || ship.build == null || ship.destroyed || flight == null)
                return;
            ShipReport report = ShipEvaluator.Evaluate(ship.build);
            ApplyCombatPower(ship, report);
            bool reactorUp = ReactorUp(ship, report);
            float turnRate = TurnRate(ship);
            float inertia = TurnInertia(ship);
            float commanded = turn * turnRate;
            float rate = Mathf.Abs(turnRate);
            float spin = Mathf.Max(rate, Mathf.Abs(commanded));
            if (inertia <= 0.001f || spin <= 0.001f)
                ship.angularVelocity = commanded;
            else
                ship.angularVelocity = Mathf.MoveTowards(ship.angularVelocity, commanded, spin / inertia * dt);
            float yaw = ship.angularVelocity * dt;
            ship.heading += yaw;
            CarryVelocity(ref ship.velocity, yaw * flight.velocityFollowTurn);
            Vector2 thrustVector = Vector2.zero;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                RoomPlacement placement = ship.build.rooms[i];
                if (placement.room.thrust <= 0f)
                    continue;
                thrustVector += Direction(ship.heading, placement.rotation) * placement.room.thrust * RoomEfficiency(ship, i);
            }

            ship.thrustNow = thrustVector.magnitude;
            float driveAccel = flight.Acceleration(ship.thrustNow, report.mass);
            PushVelocity(ref ship.velocity, thrustVector, driveAccel, thrustLevel, dt);
            ApplyGravity(ship, dt);
            Vector2 before = ship.position;
            ship.position += ship.velocity * dt;
            Vector2 impact;
            if (HitPlanet(ship, before, ship.position, out impact))
            {
                ship.position = impact;
                ship.velocity = Vector2.zero;
                if (ship.cellStructure != null)
                {
                    for (int n = 0; n < ship.cellStructure.Length; n++)
                        ship.cellStructure[n] = 0f;
                }

                ship.structure = 0f;
                ship.destroyed = true;
                ship.hitPlanet = true;
            }

            for (int i = 0; i < ship.weaponCooldown.Length; i++)
                ship.weaponCooldown[i] = Mathf.Max(0f, ship.weaponCooldown[i] - dt);

            AdvanceWeaponLock(ship, report, reactorUp, dt);
            if (allowFire)
                FireReady(ship, report, reactorUp);

            ApplyHeat(ship, report, reactorUp, dt);
        }

        void StepEnemy(float dt)
        {
            if (enemy == null || enemy.destroyed || player == null || player.destroyed)
                return;
            ShipReport report = ShipEvaluator.Evaluate(enemy.build);
            ApplyCombatPower(enemy, report);
            bool reactorUp = ReactorUp(enemy, report);
            ShipReport playerReport = ShipEvaluator.Evaluate(player.build);
            ApplyCombatPower(player, playerReport);
            bool playerReactor = ReactorUp(player, playerReport);
            GunScan mine = ScanGun(enemy, report, reactorUp);
            GunScan theirs = ScanGun(player, playerReport, playerReactor);
            Vector2 to = player.position - enemy.position;
            float distance = to.magnitude;
            Vector2 toDir = distance > 0.01f ? to / distance : Forward(enemy.heading);
            enemyJinkFlip -= dt;
            if (enemyJinkFlip <= 0f)
            {
                enemyJink = -enemyJink;
                enemyJinkFlip = 2.4f;
            }

            enemyThink -= dt;
            if (enemyThink <= 0f)
            {
                enemyThink = 0.4f;
                float myHp = enemy.structureMax > 0.01f ? enemy.structure / enemy.structureMax : 0f;
                float theirHp = player.structureMax > 0.01f ? player.structure / player.structureMax : 0f;
                float score = (myHp - theirHp) * 1.4f;
                if (mine.ready && !mine.hot && distance <= mine.reach && GunAngle(enemy, player.position, mine.rotation) < 18f)
                    score += 0.45f;
                if (theirs.found && distance <= theirs.reach && GunAngle(player, enemy.position, theirs.rotation) < 22f)
                    score -= 0.55f;
                if (!mine.ready || mine.hot)
                    score -= 0.35f;
                if (myHp < 0.3f)
                    score -= 0.4f;
                if (enemyMode > 0 && score > 0f)
                    enemyMode = 1;
                else if (enemyMode < 0 && score < 0.05f)
                    enemyMode = -1;
                else if (score >= 0.2f)
                    enemyMode = 1;
                else if (score <= -0.15f)
                    enemyMode = -1;
                else
                    enemyMode = 0;
            }

            float thrustOffset = ThrustOffset(enemy, report, reactorUp);
            float standoff = Mathf.Clamp(mine.reach * 0.55f, 70f, mine.reach * 0.8f);
            float accel = ShipDriveAccel(enemy, report, reactorUp);
            float relClosing = Vector2.Dot(enemy.velocity - player.velocity, toDir);
            Vector2 escape;
            bool planet = PlanetThreat(enemy, out escape);
            float want;
            float thrust = 0f;
            if (planet)
            {
                want = Mathf.Atan2(escape.x, escape.y) * Mathf.Rad2Deg - thrustOffset;
                thrust = AlignThrust(enemy.heading + thrustOffset, Mathf.Atan2(escape.x, escape.y) * Mathf.Rad2Deg);
            }
            else if (enemyMode < 0)
            {
                Vector2 away = -toDir;
                if (theirs.found)
                {
                    Vector2 barrel = Direction(player.heading, theirs.rotation);
                    Vector2 beam = new Vector2(-barrel.y, barrel.x) * enemyJink;
                    away = (away + beam * 0.85f).normalized;
                }

                float awayAngle = Mathf.Atan2(away.x, away.y) * Mathf.Rad2Deg;
                want = awayAngle - thrustOffset;
                thrust = AlignThrust(enemy.heading + thrustOffset, awayAngle);
            }
            else if (enemyMode > 0)
            {
                float room = Mathf.Max(0f, distance - standoff);
                float allowed = SpeedRoomAllows(enemy, room, accel);
                if (relClosing > allowed + 1.5f || distance < standoff * 0.45f)
                {
                    float awayAngle = Mathf.Atan2(-to.x, -to.y) * Mathf.Rad2Deg;
                    want = awayAngle - thrustOffset;
                    thrust = AlignThrust(enemy.heading + thrustOffset, awayAngle);
                }
                else
                {
                    Vector2 lead = LeadPoint(enemy, player, mine.muzzle, mine.reach / Mathf.Max(40f, mine.muzzle));
                    want = Mathf.Atan2(lead.x - enemy.position.x, lead.y - enemy.position.y) * Mathf.Rad2Deg - mine.rotation * 90f;
                    if (distance > standoff * 1.15f && relClosing < allowed * 0.85f)
                        thrust = AlignThrust(enemy.heading + thrustOffset, Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg);
                    else if (relClosing < -4f)
                        thrust = 0.45f;
                }
            }
            else
            {
                Vector2 slot = FlankSlot(player, theirs, enemy.position, standoff, enemyJink);
                Vector2 toSlot = slot - enemy.position;
                float slotDist = toSlot.magnitude;
                Vector2 slotDir = slotDist > 1f ? toSlot / slotDist : toDir;
                float slotClosing = Vector2.Dot(enemy.velocity - player.velocity, slotDir);
                float allowed = SpeedRoomAllows(enemy, slotDist, accel);
                if (slotClosing > allowed + 1.5f)
                {
                    float awayAngle = Mathf.Atan2(-slotDir.x, -slotDir.y) * Mathf.Rad2Deg;
                    want = awayAngle - thrustOffset;
                    thrust = AlignThrust(enemy.heading + thrustOffset, awayAngle);
                }
                else if (slotDist < 50f || (distance < mine.reach && GunAngle(enemy, player.position, mine.rotation) < 24f))
                {
                    Vector2 lead = LeadPoint(enemy, player, mine.muzzle, mine.reach / Mathf.Max(40f, mine.muzzle));
                    want = Mathf.Atan2(lead.x - enemy.position.x, lead.y - enemy.position.y) * Mathf.Rad2Deg - mine.rotation * 90f;
                    if (slotDist > 30f && slotClosing < allowed * 0.85f)
                        thrust = AlignThrust(enemy.heading + thrustOffset, Mathf.Atan2(slotDir.x, slotDir.y) * Mathf.Rad2Deg);
                    else if (slotDist <= 30f)
                        thrust = 0.2f;
                }
                else
                {
                    want = Mathf.Atan2(slotDir.x, slotDir.y) * Mathf.Rad2Deg - thrustOffset;
                    if (slotClosing < allowed * 0.85f)
                        thrust = AlignThrust(enemy.heading + thrustOffset, Mathf.Atan2(slotDir.x, slotDir.y) * Mathf.Rad2Deg);
                }
            }

            float orderRate = OrderTurnRate(enemy);
            float delta = Mathf.DeltaAngle(enemy.heading, want);
            float turn = 0f;
            if (orderRate > 0.01f && Mathf.Abs(delta) > 0.8f)
                turn = Mathf.Clamp(delta / Mathf.Max(dt, 0.01f), -orderRate, orderRate) / Mathf.Max(1f, TurnRate(enemy));
            enemyThrust = thrust;
            enemyBrake = 0f;
            StepShip(enemy, dt, thrust, 0f, turn, false);
            if (!enemy.destroyed)
                FireReady(enemy, report, reactorUp);
            RefreshCombat(report, reactorUp, playerReport, playerReactor, distance);
        }

        void RefreshCombat(ShipReport enemyReport, bool enemyReactor, ShipReport playerReport, bool playerReactor, float distance)
        {
            playerEngaging = WillEngage(player, enemy, playerReport, playerReactor, true);
            enemyEngaging = WillEngage(enemy, player, enemyReport, enemyReactor, false);
            float reach = EngageReach(enemy, enemyReport, enemyReactor);
            bool liningUp = enemyMode == 0 && reach > 1f && distance <= reach;
            enemyIntent = enemyMode > 0 || liningUp;
            combat = playerEngaging || enemyEngaging || enemyIntent;
        }

        public string CombatNotice()
        {
            if (!combat)
                return "";
            string text = "";
            if (enemyIntent && !enemyEngaging)
                text = "敌方准备攻击";
            else if (enemyEngaging)
                text = "敌方开火";
            if (playerEngaging)
                text = text.Length == 0 ? "已向敌方开火" : text + "  已向敌方开火";
            if (text.Length == 0)
                text = "战斗";
            return text;
        }

        bool WillEngage(CombatShip ship, CombatShip target, ShipReport report, bool reactorUp, bool playerGates)
        {
            if (ship == null || target == null || ship.destroyed || target.destroyed || ship.build == null)
                return false;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                bool missile = placement.room.kind == RoomKind.Missile;
                bool gun = placement.room.kind == RoomKind.Weapon;
                if (!missile && !gun)
                    continue;
                WeaponDef weapon = placement.room.weapon;
                if (weapon == null || (gun && weapon.homing))
                    continue;
                WeaponFamily family = missile || weapon.family == WeaponFamily.Missile
                    ? WeaponFamily.Missile
                    : weapon.family;
                if (playerGates && !FamilyFiring(family))
                    continue;
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                Vector2 barrel = Direction(ship.heading, placement.rotation);
                Vector2 muzzle = RoomCenter(ship, placement) + barrel * CellMeters;
                float reach = WeaponRange(weapon);
                Vector2 to = target.position - muzzle;
                if (to.sqrMagnitude < 0.01f || to.magnitude > reach)
                    continue;
                if (missile || Vector2.Angle(barrel, to) <= 55f)
                    return true;
            }

            return false;
        }

        static float EngageReach(CombatShip ship, ShipReport report, bool reactorUp)
        {
            if (ship == null || ship.build == null)
                return 0f;
            float reach = 0f;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                bool missile = placement.room.kind == RoomKind.Missile;
                bool gun = placement.room.kind == RoomKind.Weapon;
                if (!missile && !gun)
                    continue;
                WeaponDef weapon = placement.room.weapon;
                if (weapon == null || (gun && weapon.homing))
                    continue;
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                reach = Mathf.Max(reach, WeaponRange(weapon));
            }

            return reach;
        }

        sealed class GunScan
        {
            public int rotation;
            public float muzzle = 80f;
            public float reach = 240f;
            public bool found;
            public bool ready;
            public bool hot;
        }

        static GunScan ScanGun(CombatShip ship, ShipReport report, bool reactorUp)
        {
            var scan = new GunScan();
            if (ship == null || ship.build == null)
                return scan;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null || placement.room.kind != RoomKind.Weapon)
                    continue;
                WeaponDef weapon = placement.room.weapon;
                if (weapon == null || weapon.homing)
                    continue;
                scan.found = true;
                scan.rotation = placement.rotation;
                scan.muzzle = weapon.muzzleSpeed > 1f ? weapon.muzzleSpeed : 80f;
                scan.reach = WeaponRange(weapon);
                scan.ready = Functioning(ship, report, i, reactorUp);
                scan.hot = Overheated(ship, i) || (placement.room.heatCapacity > 0f && ship.heat[i] > placement.room.heatCapacity * 0.72f);
                return scan;
            }

            return scan;
        }

        static float GunAngle(CombatShip ship, Vector2 target, int rotation)
        {
            Vector2 to = target - ship.position;
            if (to.sqrMagnitude < 0.01f)
                return 0f;
            float aim = Mathf.Atan2(to.x, to.y) * Mathf.Rad2Deg;
            return Mathf.Abs(Mathf.DeltaAngle(ship.heading + rotation * 90f, aim));
        }

        static float ThrustOffset(CombatShip ship, ShipReport report, bool reactorUp)
        {
            Vector2 body = Vector2.zero;
            if (ship.build == null)
                return 0f;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                RoomPlacement placement = ship.build.rooms[i];
                if (placement.room.thrust <= 0f)
                    continue;
                body += Direction(0f, placement.rotation) * placement.room.thrust * RoomEfficiency(ship, i);
            }

            if (body.sqrMagnitude < 0.01f)
                return 0f;
            return Mathf.Atan2(body.x, body.y) * Mathf.Rad2Deg;
        }

        static float AlignThrust(float thrustAngle, float desired)
        {
            float gap = Mathf.Abs(Mathf.DeltaAngle(thrustAngle, desired));
            if (gap < 35f)
                return 1f;
            if (gap < 80f)
                return 0.3f;
            return 0f;
        }

        float OrderTurnRate(CombatShip ship)
        {
            return Mathf.Max(80f, Mathf.Abs(TurnRate(ship)) * 6f);
        }

        float FlipSeconds(CombatShip ship)
        {
            return 180f / OrderTurnRate(ship) + TurnInertia(ship);
        }

        static float TurnRate(CombatShip ship)
        {
            HullDef hull = ship != null && ship.build != null ? ship.build.hull : null;
            if (hull == null || hull.turnRateAtRest <= 0.01f)
                return 20f;
            return hull.turnRateAtRest;
        }

        static float TurnInertia(CombatShip ship)
        {
            HullDef hull = ship != null && ship.build != null ? ship.build.hull : null;
            if (hull == null)
                return 0.4f;
            return Mathf.Max(0f, hull.turnInertia);
        }

        float SpeedRoomAllows(CombatShip ship, float room, float accel)
        {
            room = Mathf.Max(0f, room);
            float a = Mathf.Max(accel, 0.5f);
            float flip = FlipSeconds(ship);
            float b = a * flip;
            return Mathf.Max(0f, -b + Mathf.Sqrt(b * b + 2f * a * room));
        }

        float ShipDriveAccel(CombatShip ship, ShipReport report, bool reactorUp)
        {
            if (ship == null || ship.build == null || flight == null || report == null)
                return 0.5f;
            float thrust = 0f;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null || placement.room.thrust <= 0f)
                    continue;
                thrust += placement.room.thrust * RoomEfficiency(ship, i);
            }

            return Mathf.Max(0.5f, flight.Acceleration(thrust, report.mass));
        }

        static Vector2 LeadPoint(CombatShip shooter, CombatShip target, float muzzle, float maxTime)
        {
            Vector2 relative = target.position - shooter.position;
            Vector2 relativeVelocity = target.velocity - shooter.velocity;
            float time = relative.magnitude / Mathf.Max(20f, muzzle);
            for (int i = 0; i < 3; i++)
            {
                Vector2 ahead = relative + relativeVelocity * time;
                time = ahead.magnitude / Mathf.Max(20f, muzzle);
            }

            time = Mathf.Clamp(time, 0f, maxTime);
            return target.position + target.velocity * time;
        }

        static Vector2 FlankSlot(CombatShip target, GunScan gun, Vector2 from, float standoff, float side)
        {
            Vector2 barrel = gun.found ? Direction(target.heading, gun.rotation) : Forward(target.heading);
            float flankSign = Mathf.Sign(Vector2.Dot(from - target.position, new Vector2(-barrel.y, barrel.x)));
            if (Mathf.Abs(flankSign) < 0.01f)
                flankSign = Mathf.Sign(side);
            Vector2 flank = new Vector2(-barrel.y, barrel.x) * flankSign;
            return target.position - barrel * standoff + flank * standoff * 0.5f;
        }

        bool PlanetThreat(CombatShip ship, out Vector2 away)
        {
            away = Vector2.up;
            float nearest = float.MaxValue;
            bool threat = false;
            float hull = HullRadius(ship);
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetPlacement body = planets[i];
                if (body == null || body.well == null)
                    continue;
                Vector2 from = ship.position - body.position;
                float distance = from.magnitude;
                if (distance < 0.01f)
                    continue;
                Vector2 outward = from / distance;
                float skin = body.well.floorRadius + hull;
                float closing = Vector2.Dot(ship.velocity, -outward);
                if (distance > skin + 220f || (closing <= 0f && distance > skin + 40f))
                    continue;
                if (distance >= nearest)
                    continue;
                nearest = distance;
                away = outward;
                threat = true;
            }

            return threat;
        }

        void FireReady(CombatShip ship, ShipReport report, bool reactorUp)
        {
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                bool missile = placement.room.kind == RoomKind.Missile;
                bool gun = placement.room.kind == RoomKind.Weapon;
                if (!missile && !gun)
                    continue;
                WeaponDef weapon = placement.room.weapon;
                if (weapon == null || (gun && weapon.homing))
                    continue;
                WeaponFamily family = missile || weapon.family == WeaponFamily.Missile
                    ? WeaponFamily.Missile
                    : weapon.family;
                if (ship.player && !FamilyFiring(family))
                    continue;
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                if (ship.weaponCooldown[i] > 0f)
                    continue;
                Vector2 barrel = Direction(ship.heading, placement.rotation);
                Vector2 muzzle = RoomCenter(ship, placement) + barrel * CellMeters;
                CombatShip target = ship.player ? enemy : player;
                float life = weapon.lifetime > 0.05f ? weapon.lifetime : 3f;
                float reach = WeaponRange(weapon);
                if (target == null || target.destroyed)
                    continue;
                Vector2 shipTo = target.position - muzzle;
                if (shipTo.sqrMagnitude < 0.01f || shipTo.magnitude > reach)
                    continue;
                int aimRoom = ship.player ? PickAimRoom(target, muzzle, attackFocus) : -1;
                Vector2 aimPoint = aimRoom >= 0 ? RoomCenter(target, target.build.rooms[aimRoom]) : target.position;
                Vector2 toPoint = aimPoint - muzzle;
                if (aimRoom >= 0 && toPoint.magnitude > reach)
                {
                    aimRoom = -1;
                    aimPoint = target.position;
                    toPoint = shipTo;
                }

                Vector2 aim = barrel;
                bool gunHit = false;
                if (missile)
                    aim = toPoint.sqrMagnitude > 0.01f ? toPoint.normalized : barrel;
                else
                {
                    Vector2 facing = toPoint.sqrMagnitude > 0.01f ? toPoint : shipTo;
                    if (Vector2.Angle(barrel, facing) > 55f)
                        continue;
                    float chance = GunHitChance(ship, i, muzzle, aimPoint, target, weapon, reach);
                    gunHit = Random.value <= chance;
                    if (!BallisticAim(ship, target, muzzle, aimPoint, weapon.muzzleSpeed, life, gunHit, out aim))
                        aim = facing.sqrMagnitude > 0.01f ? facing.normalized : barrel;
                }

                float wait = weapon.cooldown > 0.05f ? weapon.cooldown : 0.8f;
                ship.weaponCooldown[i] = wait;
                bool guide = missile ? Random.value <= Mathf.Clamp01(weapon.baseHit) : gunHit;
                Vector2 velocity = ship.velocity + aim * weapon.muzzleSpeed;
                if (missile)
                    velocity = MissileFlight.Launch(muzzle, ship.velocity, aim, weapon).velocity;
                shots.Add(new CombatShot
                {
                    position = muzzle,
                    velocity = velocity,
                    damage = weapon.damage * RoomEfficiency(ship, i),
                    lifetime = life,
                    owner = ship.player ? 0 : 1,
                    weapon = missile ? weapon : null,
                    guide = guide,
                    missSide = Random.value < 0.5f ? -1f : 1f
                });
            }
        }

        void StepShots(float dt)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                CombatShot shot = shots[i];
                CombatShip target = shot.owner == 0 ? enemy : player;
                Vector2 before = shot.position;
                if (shot.weapon != null && shot.weapon.homing)
                {
                    Vector2 aimAt = before + shot.velocity;
                    if (target != null && !target.destroyed)
                    {
                        aimAt = shot.owner == 0 ? AimPoint(target, before) : target.position;
                        if (!shot.guide)
                        {
                            Vector2 to = aimAt - before;
                            Vector2 side = to.sqrMagnitude > 0.0001f ? new Vector2(-to.y, to.x).normalized : Vector2.right;
                            aimAt += side * shot.missSide * (HullRadius(target) + 18f);
                        }
                    }

                    var state = new MissileState
                    {
                        position = shot.position,
                        velocity = shot.velocity,
                        age = shot.age
                    };
                    bool alive = MissileFlight.Step(ref state, aimAt, shot.weapon, dt);
                    shot.position = state.position;
                    shot.velocity = state.velocity;
                    shot.age = state.age;
                    if (!alive)
                    {
                        shots.RemoveAt(i);
                        continue;
                    }
                }
                else
                {
                    shot.age += dt;
                    if (shot.age > shot.lifetime)
                    {
                        shots.RemoveAt(i);
                        continue;
                    }

                    shot.position += shot.velocity * dt;
                }

                if (target == null || target.destroyed)
                    continue;
                Vector2 contact;
                if (!SegmentContact(target, before, shot.position, out contact))
                    continue;
                shot.position = contact;
                if (shot.guide)
                    TryHit(target, shot);
                shots.RemoveAt(i);
            }
        }

        static bool SegmentContact(CombatShip ship, Vector2 from, Vector2 to, out Vector2 contact)
        {
            contact = to;
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CellMeters * 0.45f)));
            for (int n = 0; n <= steps; n++)
            {
                Vector2 pos = from + delta * (n / (float)steps);
                if (!CoversCell(ship, pos))
                    continue;
                contact = pos;
                return true;
            }

            return false;
        }

        public Vector2 AimPoint(CombatShip target, Vector2 from)
        {
            if (target == null)
                return from;
            int room = PickAimRoom(target, from, attackFocus);
            if (room < 0)
                return target.position;
            return RoomCenter(target, target.build.rooms[room]);
        }

        static int PickAimRoom(CombatShip target, Vector2 from, AttackFocus focus)
        {
            if (target == null || target.build == null || focus == AttackFocus.None)
                return -1;
            int best = -1;
            float bestSq = float.PositiveInfinity;
            for (int i = 0; i < target.build.rooms.Count; i++)
            {
                RoomPlacement placement = target.build.rooms[i];
                if (placement == null || placement.room == null || !MatchesFocus(placement.room, focus))
                    continue;
                if (RoomEfficiency(target, i) <= 0.001f)
                    continue;
                Vector2 center = RoomCenter(target, placement);
                float sq = (center - from).sqrMagnitude;
                if (sq >= bestSq)
                    continue;
                bestSq = sq;
                best = i;
            }

            return best;
        }

        static bool MatchesFocus(RoomDef room, AttackFocus focus)
        {
            if (room == null)
                return false;
            if (focus == AttackFocus.Power)
                return room.kind == RoomKind.Reactor || room.powerOutput > 0f;
            if (focus == AttackFocus.Weapon)
                return room.kind == RoomKind.Weapon || room.kind == RoomKind.Missile;
            if (focus == AttackFocus.Drive)
                return room.kind == RoomKind.Thruster;
            return false;
        }

        public static float WeaponRange(WeaponDef weapon)
        {
            if (weapon == null)
                return 0f;
            if (weapon.range > 1f)
                return weapon.range;
            float life = weapon.lifetime > 0.05f ? weapon.lifetime : 3f;
            return Mathf.Max(1f, weapon.muzzleSpeed) * life;
        }

        void AdvanceWeaponLock(CombatShip ship, ShipReport report, bool reactorUp, float dt)
        {
            if (ship == null || ship.weaponLock == null || ship.build == null)
                return;
            CombatShip target = ship.player ? enemy : player;
            bool turning = Mathf.Abs(ship.angularVelocity) > 35f;
            for (int i = 0; i < ship.weaponLock.Length && i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                RoomDef room = placement != null ? placement.room : null;
                bool gun = room != null && room.kind == RoomKind.Weapon && room.weapon != null && !room.weapon.homing;
                if (!gun)
                    continue;
                float gain = room.weapon.lockPerSecond > 0.01f ? room.weapon.lockPerSecond : 0.35f;
                bool tracking = false;
                if (target != null && !target.destroyed && Functioning(ship, report, i, reactorUp))
                {
                    Vector2 barrel = Direction(ship.heading, placement.rotation);
                    Vector2 muzzle = RoomCenter(ship, placement) + barrel * CellMeters;
                    Vector2 to = target.position - muzzle;
                    float reach = WeaponRange(room.weapon);
                    tracking = to.sqrMagnitude > 0.01f && to.magnitude <= reach && Vector2.Angle(barrel, to) <= 55f;
                }

                if (tracking && !turning)
                    ship.weaponLock[i] = Mathf.Min(1f, ship.weaponLock[i] + gain * dt);
                else
                    ship.weaponLock[i] = Mathf.Max(0f, ship.weaponLock[i] - gain * (turning ? 3f : 1.4f) * dt);
            }
        }

        float GunHitChance(CombatShip ship, int index, Vector2 muzzle, Vector2 aimPoint, CombatShip target, WeaponDef weapon, float reach)
        {
            float distance = (aimPoint - muzzle).magnitude;
            Vector2 relVel = target.velocity - ship.velocity;
            float muzzleSpeed = Mathf.Max(1f, weapon.muzzleSpeed);
            float motion = relVel.magnitude * distance / muzzleSpeed;
            float radius = Mathf.Max(1f, HullRadius(target));
            float rangeFactor = reach > 1f ? Mathf.Clamp01(1.15f - 0.5f * (distance / reach)) : 1f;
            float cross = Mathf.Clamp01(radius / (radius + motion * 0.2f) * rangeFactor);
            float locked = ship.weaponLock != null && index >= 0 && index < ship.weaponLock.Length ? ship.weaponLock[index] : 0f;
            float oper = 1f;
            return Mathf.Clamp01(weapon.baseHit * oper * cross * locked);
        }

        static bool BallisticAim(CombatShip ship, CombatShip target, Vector2 muzzle, Vector2 aimPoint, float muzzleSpeed, float life, bool hit, out Vector2 aim)
        {
            aim = Vector2.up;
            Vector2 point = aimPoint;
            if (!hit)
            {
                Vector2 line = target.position - muzzle;
                Vector2 relVel = target.velocity - ship.velocity;
                Vector2 side;
                if (line.sqrMagnitude > 0.01f)
                {
                    Vector2 los = line.normalized;
                    Vector2 lateral = relVel - los * Vector2.Dot(relVel, los);
                    side = lateral.sqrMagnitude > 0.25f ? lateral.normalized : new Vector2(-los.y, los.x);
                }
                else
                    side = Vector2.right;
                point = target.position + side * (HullRadius(target) + 8f);
            }

            Vector2 relPos = point - muzzle;
            Vector2 closing = target.velocity - ship.velocity;
            float speed = Mathf.Max(1f, muzzleSpeed);
            if (!SolveIntercept(relPos, closing, speed, out float time) || time > life)
            {
                if (relPos.sqrMagnitude < 0.01f)
                    return false;
                aim = relPos.normalized;
                return true;
            }

            Vector2 shot = relPos + closing * time;
            if (shot.sqrMagnitude < 0.0001f)
                return false;
            aim = shot.normalized;
            return true;
        }

        static bool SolveIntercept(Vector2 relPos, Vector2 relVel, float shotSpeed, out float time)
        {
            time = 0f;
            float a = relVel.sqrMagnitude - shotSpeed * shotSpeed;
            float b = 2f * Vector2.Dot(relPos, relVel);
            float c = relPos.sqrMagnitude;
            if (Mathf.Abs(a) < 0.001f)
            {
                if (Mathf.Abs(b) < 0.001f)
                    return false;
                time = -c / b;
                return time > 0.02f;
            }

            float disc = b * b - 4f * a * c;
            if (disc < 0f)
                return false;
            float root = Mathf.Sqrt(disc);
            float t1 = (-b - root) / (2f * a);
            float t2 = (-b + root) / (2f * a);
            time = float.PositiveInfinity;
            if (t1 > 0.02f)
                time = t1;
            if (t2 > 0.02f && t2 < time)
                time = t2;
            return time < 1000f;
        }

        static bool AimHits(Vector2 origin, Vector2 aim, CombatShip target, float reach)
        {
            if (target == null || target.destroyed || aim.sqrMagnitude < 0.01f)
                return false;
            float step = CellMeters * 0.5f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(reach / step));
            Vector2 pos = origin;
            for (int i = 0; i < steps; i++)
            {
                pos += aim * step;
                if (CoversCell(target, pos))
                    return true;
            }

            return false;
        }

        static bool CoversCell(CombatShip ship, Vector2 world)
        {
            HullDef hull = ship.build != null ? ship.build.hull : null;
            if (hull == null)
                return false;
            Vector2 delta = world - ship.position;
            float localX = Vector2.Dot(delta, Right(ship.heading));
            float localY = Vector2.Dot(delta, Forward(ship.heading));
            int cellX = Mathf.FloorToInt(localX / CellMeters + hull.width * 0.5f);
            int cellY = Mathf.FloorToInt(localY / CellMeters + hull.height * 0.5f);
            return hull.IsOpen(cellX, cellY);
        }

        bool TryHit(CombatShip ship, CombatShot shot)
        {
            HullDef hull = ship.build.hull;
            if (hull == null || ship.destroyed)
                return false;
            Vector2 right = Right(ship.heading);
            Vector2 forward = Forward(ship.heading);
            Vector2 delta = shot.position - ship.position;
            float localX = Vector2.Dot(delta, right);
            float localY = Vector2.Dot(delta, forward);
            int cellX = Mathf.FloorToInt(localX / CellMeters + hull.width * 0.5f);
            int cellY = Mathf.FloorToInt(localY / CellMeters + hull.height * 0.5f);
            if (!hull.IsOpen(cellX, cellY))
                return false;
            int key = cellY * hull.width + cellX;
            float damage = shot.damage;
            if (key >= 0 && key < ship.cellArmor.Length && ship.cellArmor[key] > 0f)
            {
                float absorbed = Mathf.Min(ship.cellArmor[key], damage);
                ship.cellArmor[key] -= absorbed;
                damage -= absorbed;
            }

            if (damage > 0f)
                HurtCell(ship, cellX, cellY, damage);
            if (shot.weapon != null && shot.weapon.blastRadius > 0.01f)
                Splash(ship, cellX, cellY, shot.damage, shot.weapon.blastRadius);

            return true;
        }

        static void Splash(CombatShip ship, int cellX, int cellY, float damage, float radius)
        {
            HullDef hull = ship.build.hull;
            int reach = Mathf.CeilToInt(radius);
            for (int y = cellY - reach; y <= cellY + reach; y++)
            {
                for (int x = cellX - reach; x <= cellX + reach; x++)
                {
                    if (x == cellX && y == cellY)
                        continue;
                    if (Mathf.Max(Mathf.Abs(x - cellX), Mathf.Abs(y - cellY)) > radius)
                        continue;
                    if (!hull.IsOpen(x, y))
                        continue;
                    int key = y * hull.width + x;
                    float left = damage;
                    if (key >= 0 && key < ship.cellArmor.Length && ship.cellArmor[key] > 0f)
                    {
                        float absorbed = Mathf.Min(ship.cellArmor[key], left);
                        ship.cellArmor[key] -= absorbed;
                        left -= absorbed;
                    }

                    if (left > 0f)
                        HurtCell(ship, x, y, left);
                }
            }
        }

        static void ApplyHeat(CombatShip ship, ShipReport report, bool reactorUp, float dt)
        {
            int count = ship.build.rooms.Count;
            var cooledBy = new List<int>[count];
            for (int i = 0; i < count; i++)
            {
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                RoomDef room = ship.build.rooms[i].room;
                if (room.heatOutput > 0f)
                    ship.heat[i] += room.heatOutput * RoomEfficiency(ship, i) * dt;
                if (room.kind != RoomKind.Radiator || room.heatDissipation <= 0f)
                    continue;
                var cells = new HashSet<int>();
                ShipEvaluator.CollectRange(ship.build.hull, ship.build.rooms[i], cells);
                for (int n = 0; n < count; n++)
                {
                    if (n >= report.valid.Count || !report.valid[n] || ship.build.rooms[n].room == null)
                        continue;
                    if (ship.build.rooms[n].room.kind == RoomKind.Corridor)
                        continue;
                    if (!Touches(ship.build.hull, ship.build.rooms[n], cells))
                        continue;
                    if (cooledBy[n] == null)
                        cooledBy[n] = new List<int>();
                    cooledBy[n].Add(i);
                }
            }

            var budget = new float[count];
            for (int i = 0; i < count; i++)
            {
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                RoomDef room = ship.build.rooms[i].room;
                if (room.kind == RoomKind.Radiator)
                    budget[i] = room.heatDissipation * RoomEfficiency(ship, i) * dt;
            }

            for (int n = 0; n < count; n++)
            {
                if (cooledBy[n] == null)
                    continue;
                float cool = 0f;
                for (int k = 0; k < cooledBy[n].Count; k++)
                    cool += budget[cooledBy[n][k]];
                ship.heat[n] = Mathf.Max(0f, ship.heat[n] - cool);
            }
        }

        static bool Touches(HullDef hull, RoomPlacement placement, HashSet<int> cells)
        {
            for (int y = 0; y < placement.SpanY; y++)
            {
                for (int x = 0; x < placement.SpanX; x++)
                {
                    int key = (placement.y + y) * hull.width + (placement.x + x);
                    if (cells.Contains(key))
                        return true;
                }
            }

            return false;
        }

        void ApplyGravity(CombatShip ship, float dt)
        {
            PullGravity(ship.position, ref ship.velocity, ship.wells, dt);
        }

        void PullGravity(Vector2 position, ref Vector2 velocity, WellPass[] passes, float dt)
        {
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetPlacement body = planets[i];
                if (body == null || body.well == null)
                    continue;
                WellPass pass = passes != null && i < passes.Length ? passes[i] : null;
                Vector2 toPlanet = body.position - position;
                float distance = toPlanet.magnitude;
                if (distance <= 0.01f || distance > body.well.influenceRadius)
                {
                    if (pass != null)
                    {
                        pass.inside = false;
                        pass.closing = false;
                        pass.flung = false;
                    }

                    continue;
                }

                Vector2 inward = toPlanet / distance;
                float pull = body.well.AccelerationAt(distance);
                Vector2 gravity = inward * pull;
                float radial = Vector2.Dot(velocity, inward);
                if (pass != null && radial > 0.5f)
                    pass.closing = true;
                if (pass != null && pass.inside && pass.closing && !pass.flung && radial <= 0f)
                {
                    Vector2 tangent = velocity - inward * radial;
                    if (tangent.sqrMagnitude > 4f)
                        pass.flung = true;
                    pass.closing = false;
                }

                if (pass != null && pass.flung && gravity.sqrMagnitude > 0f)
                {
                    float speedSq = velocity.sqrMagnitude;
                    float along = Vector2.Dot(gravity, velocity);
                    if (along < 0f && speedSq > 0.01f)
                        gravity -= velocity * (along / speedSq);
                }

                velocity += gravity * dt;
                if (pass != null)
                    pass.inside = true;
            }
        }

        bool NearWell(Vector2 position)
        {
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetPlacement body = planets[i];
                if (body == null || body.well == null)
                    continue;
                if (Vector2.Distance(position, body.position) < body.well.influenceRadius * 1.2f)
                    return true;
            }

            return false;
        }

        static float HullRadius(CombatShip ship)
        {
            HullDef hull = ship != null && ship.build != null ? ship.build.hull : null;
            if (hull == null)
                return 0f;
            float width = hull.width * CellMeters;
            float height = hull.height * CellMeters;
            return 0.5f * Mathf.Sqrt(width * width + height * height);
        }

        bool HitPlanet(CombatShip ship, Vector2 from, Vector2 to, out Vector2 hit)
        {
            hit = to;
            if (ship == null)
                return false;
            float hull = HullRadius(ship);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetPlacement body = planets[i];
                if (body == null || body.well == null)
                    continue;
                Vector2 candidate;
                if (!SegmentHitsCircle(from, to, body.position, body.well.floorRadius + hull, out candidate))
                    continue;
                float along = (candidate - from).sqrMagnitude;
                if (along >= best)
                    continue;
                best = along;
                hit = candidate;
                found = true;
            }

            return found;
        }

        static bool SegmentHitsCircle(Vector2 from, Vector2 to, Vector2 center, float radius, out Vector2 hit)
        {
            hit = to;
            Vector2 delta = to - from;
            Vector2 offset = from - center;
            float lengthSq = Vector2.Dot(delta, delta);
            float inside = Vector2.Dot(offset, offset) - radius * radius;
            if (inside <= 0f)
            {
                hit = from;
                return true;
            }

            if (lengthSq < 0.000001f)
                return false;
            float twice = 2f * Vector2.Dot(offset, delta);
            float disc = twice * twice - 4f * lengthSq * inside;
            if (disc < 0f)
                return false;
            float t = (-twice - Mathf.Sqrt(disc)) / (2f * lengthSq);
            if (t < 0f || t > 1f)
                return false;
            hit = from + delta * t;
            return true;
        }

        public void PredictCourse(CombatShip ship, List<Vector2> into, Vector2 viewCenter, Vector2 viewHalf, float drive, bool throughView = false)
        {
            into.Clear();
            if (ship == null || ship.destroyed || ship.build == null || flight == null)
                return;
            ShipReport report = ShipEvaluator.Evaluate(ship.build);
            ApplyCombatPower(ship, report);
            bool reactorUp = ReactorUp(ship, report);
            Vector2 thrustBody = Vector2.zero;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (!Functioning(ship, report, i, reactorUp))
                    continue;
                RoomPlacement placement = ship.build.rooms[i];
                if (placement.room.thrust <= 0f)
                    continue;
                thrustBody += Direction(0f, placement.rotation) * placement.room.thrust * RoomEfficiency(ship, i);
            }

            float thrustMag = thrustBody.magnitude;
            Vector2 thrustDir = thrustMag > 0.01f ? thrustBody / thrustMag : Vector2.up;
            float accel = flight.Acceleration(thrustMag, report.mass);
            Vector2 position = ship.position;
            Vector2 origin = viewCenter;
            Vector2 velocity = ship.velocity;
            float heading = ship.heading;
            WellPass[] passes = CopyPasses(ship.wells);
            float limitX = Mathf.Max(8f, viewHalf.x);
            float limitY = Mathf.Max(8f, viewHalf.y);
            float reach = Mathf.Sqrt(limitX * limitX + limitY * limitY);
            float speedHint = velocity.magnitude;
            if (drive > 0.01f)
                speedHint = Mathf.Max(speedHint, accel * drive * 6f);
            speedHint = Mathf.Max(speedHint, 1f);
            float stride = Mathf.Max(6f, reach / 180f);
            bool crashed = false;
            bool armed = !throughView || !PastView(position, origin, limitX, limitY);
            KeepCoursePoint(into, position, origin, limitX, limitY, throughView, armed);
            int maxSteps = 420;
            if (throughView)
            {
                float far = Vector2.Distance(position, origin) + reach * 2f;
                maxSteps = Mathf.Clamp(Mathf.CeilToInt(far / stride) + 8, 420, 1600);
            }

            for (int i = 0; i < maxSteps; i++)
            {
                float speed = Mathf.Max(velocity.magnitude, speedHint * 0.35f);
                float dt = stride / speed;
                int sub = 1;
                if (NearWell(position))
                {
                    dt = Mathf.Min(dt, 0.2f);
                    sub = Mathf.Clamp(Mathf.CeilToInt(dt / 0.04f), 1, 8);
                }
                else
                {
                    dt = Mathf.Clamp(dt, 0.02f, 1.2f);
                }

                float step = dt / sub;
                for (int s = 0; s < sub; s++)
                {
                    Vector2 world = Right(heading) * thrustDir.x + Forward(heading) * thrustDir.y;
                    PushVelocity(ref velocity, world, accel, drive, step);
                    PullGravity(position, ref velocity, passes, step);
                    Vector2 before = position;
                    position += velocity * step;
                    Vector2 impact;
                    if (HitPlanet(ship, before, position, out impact))
                    {
                        position = impact;
                        crashed = true;
                        break;
                    }
                }

                bool outside = PastView(position, origin, limitX, limitY);
                if (!outside)
                    armed = true;
                KeepCoursePoint(into, position, origin, limitX, limitY, throughView, armed);
                if (crashed)
                    break;
                if (outside && (!throughView || armed))
                    break;
                if (velocity.sqrMagnitude < 0.04f && drive <= 0.01f)
                    break;
            }

            if (!crashed && velocity.sqrMagnitude > 0.04f && !PastView(position, origin, limitX, limitY))
                into.Add(RayToViewEdge(position, origin, velocity, limitX, limitY));
        }

        static void KeepCoursePoint(List<Vector2> into, Vector2 position, Vector2 origin, float limitX, float limitY, bool throughView, bool armed)
        {
            bool near = !PastView(position, origin, limitX * 1.25f, limitY * 1.25f);
            if (throughView && !armed && !near)
            {
                if (into.Count == 0)
                    into.Add(position);
                else
                    into[into.Count - 1] = position;
                return;
            }

            into.Add(position);
        }

        static bool PastView(Vector2 position, Vector2 origin, float limitX, float limitY)
        {
            return Mathf.Abs(position.x - origin.x) > limitX || Mathf.Abs(position.y - origin.y) > limitY;
        }

        static Vector2 RayToViewEdge(Vector2 position, Vector2 origin, Vector2 velocity, float limitX, float limitY)
        {
            float best = float.PositiveInfinity;
            if (velocity.x > 0.0001f)
                best = Mathf.Min(best, (origin.x + limitX - position.x) / velocity.x);
            else if (velocity.x < -0.0001f)
                best = Mathf.Min(best, (origin.x - limitX - position.x) / velocity.x);
            if (velocity.y > 0.0001f)
                best = Mathf.Min(best, (origin.y + limitY - position.y) / velocity.y);
            else if (velocity.y < -0.0001f)
                best = Mathf.Min(best, (origin.y - limitY - position.y) / velocity.y);
            if (float.IsInfinity(best) || best < 0f)
                return position;
            return position + velocity * best;
        }

        static void CarryVelocity(ref Vector2 velocity, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.0001f || velocity.sqrMagnitude < 0.0001f)
                return;
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float x = velocity.x * cos + velocity.y * sin;
            float y = -velocity.x * sin + velocity.y * cos;
            velocity = new Vector2(x, y);
        }

        static void PushVelocity(ref Vector2 velocity, Vector2 thrustWorld, float accel, float thrustLevel, float dt)
        {
            if (thrustLevel <= 0.01f || accel <= 0f || thrustWorld.sqrMagnitude <= 0.0001f)
                return;
            velocity += thrustWorld.normalized * accel * thrustLevel * dt;
        }

        static WellPass[] CopyPasses(WellPass[] source)
        {
            if (source == null)
                return null;
            var copy = new WellPass[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                WellPass pass = source[i];
                copy[i] = new WellPass();
                if (pass == null)
                    continue;
                copy[i].inside = pass.inside;
                copy[i].closing = pass.closing;
                copy[i].flung = pass.flung;
            }

            return copy;
        }

        public static float RoomEfficiency(CombatShip ship, int index)
        {
            if (ship == null || ship.build == null || ship.cellStructure == null || ship.cellStructureMax <= 0.0001f)
                return 1f;
            if (index < 0 || index >= ship.build.rooms.Count)
                return 0f;
            RoomPlacement placement = ship.build.rooms[index];
            HullDef hull = ship.build.hull;
            if (placement == null || placement.room == null || hull == null)
                return 0f;
            float sum = 0f;
            int count = 0;
            for (int y = 0; y < placement.SpanY; y++)
            {
                for (int x = 0; x < placement.SpanX; x++)
                {
                    int cellX = placement.x + x;
                    int cellY = placement.y + y;
                    if (!hull.IsOpen(cellX, cellY))
                        continue;
                    int key = cellY * hull.width + cellX;
                    if (key < 0 || key >= ship.cellStructure.Length)
                        continue;
                    sum += ship.cellStructure[key] / ship.cellStructureMax;
                    count++;
                }
            }

            if (count == 0)
                return 1f;
            return Mathf.Clamp01(sum / count);
        }

        static void HurtCell(CombatShip ship, int cellX, int cellY, float damage)
        {
            if (ship == null || ship.destroyed || damage <= 0f || ship.build == null || ship.cellStructure == null)
                return;
            HullDef hull = ship.build.hull;
            if (hull == null || !hull.IsOpen(cellX, cellY))
                return;
            int key = cellY * hull.width + cellX;
            if (key < 0 || key >= ship.cellStructure.Length)
                return;
            ship.cellStructure[key] = Mathf.Max(0f, ship.cellStructure[key] - damage);
            float sum = 0f;
            for (int i = 0; i < ship.cellStructure.Length; i++)
                sum += ship.cellStructure[i];
            ship.structure = sum;
            if (ship.structure <= 0.05f)
            {
                ship.structure = 0f;
                ship.destroyed = true;
            }
        }

        static void ApplyCombatPower(CombatShip ship, ShipReport report)
        {
            if (ship == null || ship.build == null || report == null)
                return;
            float output = 0f;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (i >= report.valid.Count || !report.valid[i])
                    continue;
                RoomDef room = ship.build.rooms[i].room;
                if (room == null || room.powerOutput <= 0f)
                    continue;
                float efficiency = RoomEfficiency(ship, i);
                if (efficiency <= 0.001f || Overheated(ship, i))
                {
                    report.online[i] = false;
                    continue;
                }

                report.online[i] = true;
                output += room.powerOutput * efficiency;
            }

            float left = output;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (i >= report.valid.Count || !report.valid[i])
                    continue;
                RoomDef room = ship.build.rooms[i].room;
                if (room == null || room.powerOutput > 0f)
                    continue;
                if (RoomEfficiency(ship, i) <= 0.001f)
                {
                    report.online[i] = false;
                    continue;
                }

                if (ShipEvaluator.NeedsEnergy(room) && (i >= report.crewed.Count || !report.crewed[i]))
                {
                    report.online[i] = false;
                    continue;
                }

                if (room.powerDraw <= left)
                {
                    report.online[i] = true;
                    left -= room.powerDraw;
                }
                else
                    report.online[i] = false;
            }
        }

        public static bool Functioning(CombatShip ship, ShipReport report, int index, bool reactorUp)
        {
            if (ship.build == null || index < 0 || index >= ship.build.rooms.Count)
                return false;
            if (index >= report.valid.Count || !report.valid[index] || !report.online[index])
                return false;
            RoomDef room = ship.build.rooms[index].room;
            if (room == null)
                return false;
            if (ShipEvaluator.NeedsEnergy(room) && (!report.crewed[index] || !reactorUp))
                return false;
            if (RoomEfficiency(ship, index) <= 0.001f)
                return false;
            return !Overheated(ship, index);
        }

        public static bool Overheated(CombatShip ship, int index)
        {
            if (index < 0 || index >= ship.heat.Length || ship.build == null || index >= ship.build.rooms.Count)
                return false;
            RoomDef room = ship.build.rooms[index].room;
            if (room == null || room.heatOutput <= 0f)
                return false;
            return ship.heat[index] > room.heatCapacity;
        }

        static bool ReactorUp(CombatShip ship, ShipReport report)
        {
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomDef room = ship.build.rooms[i] != null ? ship.build.rooms[i].room : null;
                if (room == null || room.kind != RoomKind.Reactor || room.powerOutput <= 0f)
                    continue;
                if (i < report.valid.Count && report.valid[i] && report.online[i] && !Overheated(ship, i))
                    return true;
            }

            return false;
        }

        public static Vector2 RoomCenter(CombatShip ship, RoomPlacement placement)
        {
            HullDef hull = ship.build.hull;
            float localX = (placement.x + placement.SpanX * 0.5f - hull.width * 0.5f) * CellMeters;
            float localY = (placement.y + placement.SpanY * 0.5f - hull.height * 0.5f) * CellMeters;
            return ship.position + Right(ship.heading) * localX + Forward(ship.heading) * localY;
        }

        public static Vector2 Forward(float heading)
        {
            float rad = heading * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        public static Vector2 Right(float heading)
        {
            float rad = heading * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad));
        }

        public static Vector2 Direction(float heading, int rotation)
        {
            int turns = ((rotation % 4) + 4) % 4;
            return Forward(heading + turns * 90f);
        }
    }
}
