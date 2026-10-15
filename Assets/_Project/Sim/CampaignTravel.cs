using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SpaceShip
{
    public enum TravelMode
    {
        Conventional = 0,
        PulseCruise = 1,
        Warp = 2
    }

    public enum TravelPhase
    {
        AtStarOrbit = 0,
        Warping = 1,
        Pulsing = 2,
        AtPlanetOrbit = 3,
        Docked = 4
    }

    public sealed class CargoLine
    {
        public GoodDef good;
        public int units;
    }

    public sealed class CampaignTravel
    {
        public WorldSnapshot world;
        public FlightTuning flight;
        public ShipBuild ship;
        public TravelPhase phase = TravelPhase.AtStarOrbit;
        public TravelMode mode = TravelMode.Conventional;
        public string starId = "";
        public string planetId = "";
        public string stationId = "";
        public string status = "";
        public bool inCombat;
        public bool focusSystem;
        public int revision;
        public float clock;
        public float elapsed;
        public float visualDuration;
        public float trueDuration;
        public string transitLabel = "";
        public RouteKind arrivalKind = RouteKind.Official;
        public double credits = 8000d;
        public readonly List<CargoLine> hold = new List<CargoLine>();
        public Vector2 systemKm;

        string warpTargetId = "";
        string pulsePlanetId = "";
        bool pulseToStar;
        Vector2 pulseFrom;
        Vector2 pulseTo;

        public bool Ready
        {
            get { return world != null && world.generated; }
        }

        public float Progress
        {
            get
            {
                if (visualDuration <= 0.01f)
                    return 1f;
                return Mathf.Clamp01(elapsed / visualDuration);
            }
        }

        public bool Begin(WorldSnapshot snapshot, FlightTuning flightTuning, ShipBuild playerShip)
        {
            world = WorldGen.Load(snapshot);
            flight = flightTuning;
            ship = playerShip;
            hold.Clear();
            credits = 8000d;
            inCombat = false;
            clock = 0f;
            revision++;
            if (world == null || !world.generated)
            {
                phase = TravelPhase.AtStarOrbit;
                starId = "";
                status = "星图还没有生成。在编辑器里按种子写入快照，读档不会重新生成。";
                return false;
            }

            starId = world.startStarId;
            planetId = "";
            stationId = "";
            phase = TravelPhase.AtStarOrbit;
            mode = TravelMode.Conventional;
            arrivalKind = RouteKind.Official;
            systemKm = StellarOffset();
            SeedHold();
            StarRecord star = world.FindStar(starId);
            status = "位于 " + (star != null ? star.displayName : starId) + " 轨道。恒星不能降落。";
            return true;
        }

        public void Tick(float dt)
        {
            if (!Ready || inCombat)
                return;
            if (dt < 0f)
                dt = 0f;
            clock += dt;
            if (phase != TravelPhase.Warping && phase != TravelPhase.Pulsing)
                return;
            elapsed += dt;
            if (phase == TravelPhase.Pulsing)
                systemKm = Vector2.Lerp(pulseFrom, pulseTo, Progress);
            if (elapsed + 0.0001f < visualDuration)
                return;
            if (phase == TravelPhase.Warping)
                ArriveWarp();
            else
                ArrivePulse();
        }

        public float ConventionalMetersPerSecond()
        {
            if (flight == null || ship == null)
                return 100f;
            ShipReport report = ShipEvaluator.Evaluate(ship);
            float accel = flight.Acceleration(report.thrust, Mathf.Max(report.mass, 0.01f));
            float weighted = 0f;
            float weight = 0f;
            if (ship.rooms != null)
            {
                for (int i = 0; i < ship.rooms.Count; i++)
                {
                    if (report.online != null && i < report.online.Count && !report.online[i])
                        continue;
                    RoomPlacement placement = ship.rooms[i];
                    if (placement == null || placement.room == null || placement.room.thrust <= 0f)
                        continue;
                    if (placement.room.speedAdjustTime <= 0.01f)
                        continue;
                    weighted += placement.room.speedAdjustTime * placement.room.thrust;
                    weight += placement.room.thrust;
                }
            }

            if (weight <= 0.0001f || accel <= 0f)
                return Mathf.Max(accel, 0f);
            return accel * (weighted / weight);
        }

        public float PulseKilometersPerSecond()
        {
            float factor = world != null && world.rules != null && world.rules.travel != null
                ? world.rules.travel.pulseOverConventional
                : 40f;
            return ConventionalMetersPerSecond() * factor / 1000f;
        }

        public string SpeedLine()
        {
            float conventional = ConventionalMetersPerSecond();
            float pulse = PulseKilometersPerSecond();
            return "常规引擎 " + conventional.ToString("0", CultureInfo.InvariantCulture)
                + " 米/秒（战斗用这套飞行手感）\n脉冲巡航 "
                + pulse.ToString("0.00", CultureInfo.InvariantCulture)
                + " 千米/秒";
        }

        public string LocationLine()
        {
            if (!Ready)
                return status;
            StarRecord star = world.FindStar(starId);
            string where = star != null ? star.displayName : starId;
            if (phase == TravelPhase.Warping)
                return "曲速前往 " + transitLabel;
            if (phase == TravelPhase.Pulsing)
                return "脉冲巡航 " + transitLabel;
            if (phase == TravelPhase.Docked)
            {
                StationRecord station = world.FindStation(stationId);
                return "停靠 " + (station != null ? station.displayName : where);
            }

            if (phase == TravelPhase.AtPlanetOrbit)
            {
                PlanetRecord planet = world.FindPlanet(planetId);
                return where + " / " + (planet != null ? planet.displayName : planetId) + " 轨道";
            }

            return where + " 恒星轨道";
        }

        public bool TryDiscover(string routeId, out string error)
        {
            error = "";
            if (!ReadyForOrder(out error))
                return false;
            if (phase != TravelPhase.AtStarOrbit && !(phase == TravelPhase.Docked && string.IsNullOrEmpty(planetId)))
            {
                error = "先回到恒星轨道，再探测航路。";
                return false;
            }

            RouteRecord route = world.FindRoute(routeId);
            if (route == null || !world.RouteTouches(route, starId))
            {
                error = "这条航路不从这里出发。";
                return false;
            }

            if (route.discovered)
            {
                error = "已经在海图上。";
                return false;
            }

            route.discovered = true;
            WorldGen.RefreshUnlocks(world);
            string otherId = world.OtherEnd(route, starId);
            StarRecord other = world.FindStar(otherId);
            string name = other != null ? other.displayName : otherId;
            if (route.kind == RouteKind.Expired || !RouteDef.AllowsWarp(world.rules, route.kind))
            {
                status = "航路已经过期，不能曲速。";
                world.AppendChange("探测 " + route.id + "，过期，不能曲速。");
            }
            else
            {
                status = "航路写入海图，解锁 " + name + "。";
                world.AppendChange("发现航路 " + route.id + "（" + RouteDef.Label(route.kind) + "），解锁 " + name + "。");
            }

            error = "";
            revision++;
            return true;
        }

        public bool TryWarp(string targetStarId, out string error)
        {
            error = "";
            if (!ReadyForOrder(out error))
                return false;
            if (phase != TravelPhase.AtStarOrbit && !(phase == TravelPhase.Docked && string.IsNullOrEmpty(planetId)))
            {
                error = "先脉冲巡航回恒星轨道，再曲速。";
                return false;
            }

            if (targetStarId == starId)
            {
                error = "已经在这颗恒星。";
                return false;
            }

            RouteRecord route = OpenRoute(starId, targetStarId);
            if (route == null)
            {
                error = "没有已发现且还能走的航路。";
                return false;
            }

            StarRecord target = world.FindStar(targetStarId);
            if (target == null)
            {
                error = "没有这颗恒星。";
                return false;
            }

            StarRecord from = world.FindStar(starId);
            if (from == null)
            {
                error = "当前恒星不在快照里。";
                return false;
            }

            float lightYears = Vector2.Distance(from.position, target.position);
            float secondsPer = world.rules != null && world.rules.travel != null ? world.rules.travel.secondsPerLightYear : 6f;
            trueDuration = Mathf.Max(0.5f, lightYears * secondsPer);
            visualDuration = Compress(trueDuration);
            elapsed = 0f;
            phase = TravelPhase.Warping;
            mode = TravelMode.Warp;
            warpTargetId = targetStarId;
            arrivalKind = route.kind;
            transitLabel = target.displayName + "（" + lightYears.ToString("0.0", CultureInfo.InvariantCulture) + " 光年）";
            status = "曲速启动。真实航时 " + trueDuration.ToString("0", CultureInfo.InvariantCulture) + " 秒。";
            revision++;
            return true;
        }

        public bool TryPulseToPlanet(string targetPlanetId, out string error)
        {
            error = "";
            if (!ReadyForOrder(out error))
                return false;
            PlanetRecord planet = world.FindPlanet(targetPlanetId);
            if (planet == null || planet.starId != starId)
            {
                error = "脉冲巡航只在本星系里走。";
                return false;
            }

            if (phase == TravelPhase.AtPlanetOrbit && planetId == targetPlanetId)
            {
                error = "已经在这颗行星的轨道上。";
                return false;
            }

            if (phase == TravelPhase.Docked)
                LeaveDock();
            return BeginPulse(planet.displayName, false, targetPlanetId, PlanetOffset(planet));
        }

        public bool TryPulseToStar(out string error)
        {
            error = "";
            if (!ReadyForOrder(out error))
                return false;
            if (phase == TravelPhase.AtStarOrbit)
            {
                error = "已经在恒星轨道。";
                return false;
            }

            if (phase == TravelPhase.Docked)
                LeaveDock();
            StarRecord star = world.FindStar(starId);
            string name = star != null ? star.displayName : "恒星";
            return BeginPulse(name + "轨道站", true, "", StellarOffset());
        }

        public bool TryDock(out string error)
        {
            error = "";
            if (!ReadyForOrder(out error))
                return false;
            StationRecord station = null;
            if (phase == TravelPhase.AtPlanetOrbit)
                station = world.PlanetStation(planetId);
            else if (phase == TravelPhase.AtStarOrbit)
                station = world.StellarStation(starId);
            if (station == null)
            {
                error = phase == TravelPhase.AtPlanetOrbit ? "这颗行星没有轨道站。" : "先进入轨道再停靠。";
                return false;
            }

            phase = TravelPhase.Docked;
            mode = TravelMode.Conventional;
            stationId = station.id;
            status = "已停靠 " + station.displayName + "。交易在站内，不在星图上。";
            world.AppendChange("停靠 " + station.displayName + "。");
            revision++;
            return true;
        }

        public bool TryUndock(out string error)
        {
            error = "";
            if (phase != TravelPhase.Docked)
            {
                error = "没有停靠。";
                return false;
            }

            LeaveDock();
            status = string.IsNullOrEmpty(planetId) ? "离开轨道站，停在恒星轨道。" : "离开行星站，停在行星轨道。";
            revision++;
            return true;
        }

        public bool TryTransfer(GoodDef good, bool buy, out string error)
        {
            error = "";
            if (phase != TravelPhase.Docked)
            {
                error = "先停靠轨道站，才能转运货物。";
                return false;
            }

            StationRecord station = world.FindStation(stationId);
            StockLine line = FindStock(station, good);
            if (line == null || good == null)
            {
                error = "这站没有这种货。";
                return false;
            }

            float price = Quote(station, line);
            if (buy)
            {
                if (line.stock < 1f)
                {
                    error = "库存不够。";
                    return false;
                }

                if (credits + 0.001d < price)
                {
                    error = "信用点不够。";
                    return false;
                }

                line.stock -= 1f;
                credits -= price;
                AddCargo(good, 1);
            }
            else
            {
                if (CargoOf(good) < 1)
                {
                    error = "船上没有这种货。";
                    return false;
                }

                if (line.stock + 1f > line.capacity)
                {
                    error = "车站堆满了。";
                    return false;
                }

                line.stock += 1f;
                credits += price;
                AddCargo(good, -1);
            }

            string verb = buy ? "买入" : "卖出";
            status = verb + " " + good.displayName + "，价格 " + price.ToString("0.0", CultureInfo.InvariantCulture) + "。";
            world.AppendChange("停靠 " + station.displayName + "，" + verb + good.displayName + " 1，价格 " + price.ToString("0.0", CultureInfo.InvariantCulture) + "。");
            revision++;
            return true;
        }

        public void EnterConventional()
        {
            if (phase == TravelPhase.Warping || phase == TravelPhase.Pulsing)
            {
                phase = string.IsNullOrEmpty(planetId) ? TravelPhase.AtStarOrbit : TravelPhase.AtPlanetOrbit;
                elapsed = 0f;
            }

            inCombat = true;
            mode = TravelMode.Conventional;
            status = "切到常规引擎。战斗沿用现有自动驾驶和飞行手感。";
            revision++;
        }

        public void ExitConventional()
        {
            inCombat = false;
            status = "离开战斗，回到星图。曲速和脉冲巡航可以再用。";
            revision++;
        }

        public float Quote(StationRecord station, StockLine line)
        {
            float tax = TaxAt(station);
            float angle = PhaseAt(station);
            return MarketMath.Quote(world != null ? world.rules : null, line, tax, arrivalKind, angle);
        }

        public int CargoOf(GoodDef good)
        {
            for (int i = 0; i < hold.Count; i++)
            {
                if (hold[i].good == good)
                    return hold[i].units;
            }

            return 0;
        }

        bool BeginPulse(string label, bool toStar, string targetPlanet, Vector2 targetKm)
        {
            string error;
            if (!ReadyForOrder(out error))
                return false;
            pulseFrom = systemKm;
            pulseTo = targetKm;
            pulseToStar = toStar;
            pulsePlanetId = targetPlanet ?? "";
            float kilos = Vector2.Distance(pulseFrom, pulseTo);
            float speed = Mathf.Max(0.05f, PulseKilometersPerSecond());
            trueDuration = Mathf.Max(0.5f, kilos / speed);
            visualDuration = Compress(trueDuration);
            elapsed = 0f;
            phase = TravelPhase.Pulsing;
            mode = TravelMode.PulseCruise;
            stationId = "";
            transitLabel = label + "（" + kilos.ToString("0", CultureInfo.InvariantCulture) + " 千米）";
            status = "脉冲巡航。真实航时 " + trueDuration.ToString("0", CultureInfo.InvariantCulture) + " 秒。";
            revision++;
            return true;
        }

        void ArriveWarp()
        {
            starId = warpTargetId;
            planetId = "";
            stationId = "";
            phase = TravelPhase.AtStarOrbit;
            mode = TravelMode.Conventional;
            systemKm = StellarOffset();
            StarRecord star = world.FindStar(starId);
            string name = star != null ? star.displayName : starId;
            focusSystem = true;
            status = "抵达 " + name + " 轨道。恒星不能降落，要去行星请脉冲巡航。";
            world.AppendChange("曲速抵达 " + name + "。");
            revision++;
        }

        void ArrivePulse()
        {
            systemKm = pulseTo;
            stationId = "";
            mode = TravelMode.Conventional;
            if (pulseToStar)
            {
                planetId = "";
                phase = TravelPhase.AtStarOrbit;
                status = "脉冲巡航结束，回到恒星轨道。恒星不能降落。";
            }
            else
            {
                planetId = pulsePlanetId;
                phase = TravelPhase.AtPlanetOrbit;
                PlanetRecord planet = world.FindPlanet(planetId);
                string name = planet != null ? planet.displayName : planetId;
                status = "进入 " + name + " 轨道。有站才能停靠转运。";
            }

            revision++;
        }

        void LeaveDock()
        {
            stationId = "";
            phase = string.IsNullOrEmpty(planetId) ? TravelPhase.AtStarOrbit : TravelPhase.AtPlanetOrbit;
        }

        bool ReadyForOrder(out string error)
        {
            error = "";
            if (!Ready)
            {
                error = "星图还没有生成。";
                return false;
            }

            if (inCombat)
            {
                error = "战斗中只能用常规引擎。";
                return false;
            }

            if (phase == TravelPhase.Warping || phase == TravelPhase.Pulsing)
            {
                error = "正在航途中。";
                return false;
            }

            return true;
        }

        RouteRecord OpenRoute(string fromId, string toId)
        {
            if (world.routes == null)
                return null;
            for (int i = 0; i < world.routes.Count; i++)
            {
                RouteRecord route = world.routes[i];
                if (route == null || !route.discovered)
                    continue;
                if (!RouteDef.AllowsWarp(world.rules, route.kind))
                    continue;
                bool touches = (route.fromStarId == fromId && route.toStarId == toId)
                    || (route.fromStarId == toId && route.toStarId == fromId);
                if (touches)
                    return route;
            }

            return null;
        }

        float Compress(float seconds)
        {
            TravelTuning tuning = world != null && world.rules != null ? world.rules.travel : null;
            if (tuning == null || tuning.demoSeconds <= 0f)
                return seconds;
            return Mathf.Min(seconds, tuning.demoSeconds);
        }

        float TaxAt(StationRecord station)
        {
            if (world == null || world.rules == null)
                return 1f;
            string factionId = "";
            if (station != null && !string.IsNullOrEmpty(station.planetId))
            {
                PlanetRecord planet = world.FindPlanet(station.planetId);
                if (planet != null)
                    factionId = planet.factionId;
            }
            else
            {
                StarRecord star = world.FindStar(starId);
                if (star != null)
                    factionId = star.factionId;
            }

            FactionDef faction = FindFaction(factionId);
            if (faction == null)
                return world.rules.wildTax;
            return faction.tax;
        }

        float PhaseAt(StationRecord station)
        {
            if (station != null && !string.IsNullOrEmpty(station.planetId))
            {
                PlanetRecord planet = world.FindPlanet(station.planetId);
                if (planet != null)
                    return MarketMath.OrbitAngle(planet.orbitPhase, planet.orbitPeriod, clock);
            }

            return MarketMath.OrbitAngle(0f, 20f, clock);
        }

        public Vector2 PlanetOffset(PlanetRecord planet)
        {
            if (planet == null)
                return Vector2.zero;
            return MarketMath.OrbitOffset(planet.orbitPhase, planet.orbitPeriod, planet.orbitRadius, clock);
        }

        public Vector2 StellarOffset()
        {
            float radius = 500f;
            if (world != null && world.rules != null && world.rules.travel != null)
                radius = world.rules.travel.stellarStationKm;
            return MarketMath.OrbitOffset(0f, 20f, radius, clock);
        }

        FactionDef FindFaction(string factionId)
        {
            if (world == null || world.rules == null || world.rules.factions == null || string.IsNullOrEmpty(factionId))
                return null;
            for (int i = 0; i < world.rules.factions.Count; i++)
            {
                FactionDef faction = world.rules.factions[i];
                if (faction != null && faction.factionId == factionId)
                    return faction;
            }

            return null;
        }

        public FactionDef FactionOf(string factionId)
        {
            return FindFaction(factionId);
        }

        static StockLine FindStock(StationRecord station, GoodDef good)
        {
            if (station == null || station.stock == null)
                return null;
            for (int i = 0; i < station.stock.Count; i++)
            {
                if (station.stock[i] != null && station.stock[i].good == good)
                    return station.stock[i];
            }

            return null;
        }

        void SeedHold()
        {
            if (world.rules == null || world.rules.goods == null)
                return;
            for (int i = 0; i < world.rules.goods.Count; i++)
            {
                GoodDef good = world.rules.goods[i];
                if (good == null)
                    continue;
                if (good.goodId == "水")
                    AddCargo(good, 20);
                else if (good.goodId == "合金")
                    AddCargo(good, 8);
            }
        }

        void AddCargo(GoodDef good, int units)
        {
            for (int i = 0; i < hold.Count; i++)
            {
                if (hold[i].good != good)
                    continue;
                hold[i].units = Mathf.Max(0, hold[i].units + units);
                return;
            }

            if (units > 0)
                hold.Add(new CargoLine { good = good, units = units });
        }
    }
}
