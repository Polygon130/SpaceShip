using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace SpaceShip
{
    public sealed class CampaignRng
    {
        uint state;

        public CampaignRng(int seed)
        {
            state = (uint)seed;
            if (state == 0)
                state = 1u;
        }

        public uint NextU()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public float Float()
        {
            return (NextU() & 0xFFFFFFu) / 16777216f;
        }

        public int Int(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
                return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextU() % span);
        }

        public float Range(float min, float max)
        {
            return min + Float() * (max - min);
        }
    }

    public static class WorldGen
    {
        static readonly string[] NameA =
        {
            "青", "赤", "白", "玄", "金", "沧", "霜", "岚", "朔", "望", "临", "归", "远", "静", "明", "沉"
        };

        static readonly string[] NameB =
        {
            "垣", "宿", "津", "浦", "阙", "渊", "野", "湾", "门", "台", "川", "陵"
        };

        static readonly string[] NameC =
        {
            "星", "陆", "泽", "原", "丘", "环", "洲", "峡"
        };

        sealed class GenContext
        {
            public CampaignRng rng;
            public GalaxyRules rules;
            public WorldSnapshot world;
            public List<string> audit = new List<string>();
            public HashSet<string> names = new HashSet<string>();
            public HashSet<string> pairs = new HashSet<string>();
            public List<Vector2> placed = new List<Vector2>();
            public List<FactionDef> used = new List<FactionDef>();
            public int nameCursor;
            public int starSerial;
            public int planetSerial;
            public int stationSerial;
            public int routeSerial;
        }

        public static WorldSnapshot Load(WorldSnapshot snapshot)
        {
            return snapshot;
        }

        public static string Validate(GalaxyRules rules)
        {
            if (rules == null)
                return "没有星图规则。";
            if (rules.wells == null || rules.wells.Count == 0 || rules.wells[0] == null)
                return "规则里没有重力井。外围行星沿用战斗里的行星文档。";
            if (rules.factions == null || rules.factions.Count == 0)
                return "规则里没有势力。";
            return null;
        }

        public static WorldSnapshot Build(GalaxyRules rules)
        {
            string error = Validate(rules);
            if (error != null)
                return null;
            var snapshot = ScriptableObject.CreateInstance<WorldSnapshot>();
            snapshot.name = "星图预览";
            Fill(snapshot, rules, null);
            return snapshot;
        }

        public static string TryWrite(GalaxyRules rules, WorldSnapshot destination, bool force)
        {
            string error = Validate(rules);
            if (error != null)
                return error;
            if (destination == null)
                return "没有星图快照。";
            if (destination.generated && !force)
                return "快照已经生成。打开它不会重新生成。";
            List<ChangeEntry> prior = null;
            if (force && destination.changelog != null)
                prior = new List<ChangeEntry>(destination.changelog);
            Fill(destination, rules, prior);
            return null;
        }

        public static void Fill(WorldSnapshot destination, GalaxyRules rules, List<ChangeEntry> priorChangelog)
        {
            var ctx = new GenContext();
            ctx.rules = rules;
            ctx.rng = new CampaignRng(rules.seed);
            ctx.world = destination;
            destination.seed = rules.seed;
            destination.rulesVersion = rules.rulesVersion;
            destination.rules = rules;
            destination.stars = new List<StarRecord>();
            destination.planets = new List<PlanetRecord>();
            destination.stations = new List<StationRecord>();
            destination.routes = new List<RouteRecord>();
            destination.audit = ctx.audit;
            destination.startStarId = "";
            destination.generated = false;

            SelectFactions(ctx);
            for (int i = 0; i < ctx.used.Count; i++)
                PlaceCore(ctx, ctx.used[i]);

            int remaining = rules.planetTarget - destination.planets.Count;
            if (remaining < 0)
            {
                ctx.audit.Add("手写核心行星已经超过目标 " + rules.planetTarget + "，多出来的核心仍保留。");
                remaining = 0;
            }

            int guard = 0;
            while (remaining > 0 && guard < 80)
            {
                guard++;
                int minCount = Mathf.Max(1, rules.peripheryMinPlanets);
                int maxCount = Mathf.Max(minCount, rules.peripheryMaxPlanets);
                int count = ctx.rng.Int(minCount, maxCount + 1);
                if (count > remaining || remaining < minCount)
                    count = remaining;
                PlacePeriphery(ctx, count);
                remaining -= count;
            }

            if (remaining > 0)
                ctx.audit.Add("外围恒星没能放完，还差 " + remaining + " 颗行星。");

            LinkRoutes(ctx);
            PickStart(ctx);
            DiscoverFromStart(ctx);
            destination.generated = true;
            destination.fingerprint = FingerprintOf(destination);
            destination.summary = Describe(destination);
            destination.changelog = new List<ChangeEntry>();
            if (priorChangelog != null)
            {
                for (int i = 0; i < priorChangelog.Count; i++)
                {
                    ChangeEntry entry = priorChangelog[i];
                    if (entry == null)
                        continue;
                    destination.changelog.Add(new ChangeEntry { order = destination.changelog.Count + 1, summary = entry.summary });
                }

                destination.AppendChange("强制重生成。种子 " + rules.seed + "。上一张海图作废，记录仍留着。");
            }

            destination.AppendChange(destination.summary + " 此后读档不重新生成。");
            Audit(ctx);
        }

        public static string Describe(WorldSnapshot world)
        {
            if (world == null)
                return "没有快照。";
            int official = 0;
            int priv = 0;
            int smuggler = 0;
            int expired = 0;
            if (world.routes != null)
            {
                for (int i = 0; i < world.routes.Count; i++)
                {
                    RouteRecord route = world.routes[i];
                    if (route == null)
                        continue;
                    if (route.kind == RouteKind.Official)
                        official++;
                    else if (route.kind == RouteKind.Private)
                        priv++;
                    else if (route.kind == RouteKind.Smuggler)
                        smuggler++;
                    else
                        expired++;
                }
            }

            int wild = 0;
            if (world.planets != null)
            {
                for (int i = 0; i < world.planets.Count; i++)
                {
                    if (world.planets[i] != null && string.IsNullOrEmpty(world.planets[i].factionId))
                        wild++;
                }
            }

            return "种子 " + world.seed
                + "，恒星 " + (world.stars == null ? 0 : world.stars.Count)
                + "，行星 " + (world.planets == null ? 0 : world.planets.Count)
                + "，无主 " + wild
                + "，官道 " + official
                + "，私航 " + priv
                + "，走私 " + smuggler
                + "，过期 " + expired
                + "。";
        }

        public static string OwnershipReport(WorldSnapshot world)
        {
            if (world == null || world.rules == null || world.rules.factions == null)
                return "";
            var counts = new Dictionary<string, int>();
            if (world.planets != null)
            {
                for (int i = 0; i < world.planets.Count; i++)
                {
                    PlanetRecord planet = world.planets[i];
                    if (planet == null || string.IsNullOrEmpty(planet.factionId))
                        continue;
                    int have;
                    counts.TryGetValue(planet.factionId, out have);
                    counts[planet.factionId] = have + 1;
                }
            }

            var builder = new StringBuilder();
            for (int i = 0; i < world.rules.factions.Count; i++)
            {
                FactionDef faction = world.rules.factions[i];
                if (faction == null)
                    continue;
                int have;
                counts.TryGetValue(faction.factionId, out have);
                if (have == 0 && !OwnsStar(world, faction.factionId))
                    continue;
                if (builder.Length > 0)
                    builder.Append("\n");
                builder.Append(faction.displayName);
                builder.Append(" ");
                builder.Append(FactionDef.RankLabel(faction.rank));
                builder.Append(" ");
                builder.Append(have);
                builder.Append("/");
                builder.Append(faction.Cap);
            }

            return builder.ToString();
        }

        public static string FingerprintOf(WorldSnapshot world)
        {
            var builder = new StringBuilder();
            builder.Append(world.seed);
            AppendStars(builder, world);
            AppendPlanets(builder, world);
            AppendRoutes(builder, world);
            ulong hash = 14695981039346656037UL;
            string text = builder.ToString();
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 1099511628211UL;
            }

            return hash.ToString("X16", CultureInfo.InvariantCulture);
        }

        static void AppendStars(StringBuilder builder, WorldSnapshot world)
        {
            if (world.stars == null)
                return;
            for (int i = 0; i < world.stars.Count; i++)
            {
                StarRecord star = world.stars[i];
                if (star == null)
                    continue;
                builder.Append('|');
                builder.Append(star.id);
                builder.Append(star.displayName);
                builder.Append(star.position.x.ToString("0.00", CultureInfo.InvariantCulture));
                builder.Append(star.position.y.ToString("0.00", CultureInfo.InvariantCulture));
                builder.Append(star.factionId);
                builder.Append(star.unlocked ? '1' : '0');
                builder.Append(star.landable ? '1' : '0');
            }
        }

        static void AppendPlanets(StringBuilder builder, WorldSnapshot world)
        {
            if (world.planets == null)
                return;
            for (int i = 0; i < world.planets.Count; i++)
            {
                PlanetRecord planet = world.planets[i];
                if (planet == null)
                    continue;
                builder.Append('|');
                builder.Append(planet.id);
                builder.Append(planet.starId);
                builder.Append(planet.displayName);
                builder.Append(planet.factionId);
                builder.Append(planet.orbitRadius.ToString("0", CultureInfo.InvariantCulture));
                builder.Append((int)planet.role);
            }
        }

        static void AppendRoutes(StringBuilder builder, WorldSnapshot world)
        {
            if (world.routes == null)
                return;
            for (int i = 0; i < world.routes.Count; i++)
            {
                RouteRecord route = world.routes[i];
                if (route == null)
                    continue;
                builder.Append('|');
                builder.Append(route.id);
                builder.Append((int)route.kind);
                builder.Append(route.fromStarId);
                builder.Append(route.toStarId);
                builder.Append(route.discovered ? '1' : '0');
            }
        }

        static bool OwnsStar(WorldSnapshot world, string factionId)
        {
            if (world.stars == null)
                return false;
            for (int i = 0; i < world.stars.Count; i++)
            {
                StarRecord star = world.stars[i];
                if (star != null && star.factionCore && star.factionId == factionId)
                    return true;
            }

            return false;
        }

        static void SelectFactions(GenContext ctx)
        {
            int majors = 0;
            int minors = 0;
            List<FactionDef> factions = ctx.rules.factions;
            for (int i = 0; i < factions.Count; i++)
            {
                FactionDef faction = factions[i];
                if (faction == null)
                    continue;
                if (faction.rank == FactionRank.Minor)
                {
                    if (minors >= ctx.rules.minorCount)
                        continue;
                    minors++;
                }
                else
                {
                    if (majors >= ctx.rules.majorCount)
                        continue;
                    majors++;
                }

                if (faction.core == null)
                {
                    ctx.audit.Add(faction.displayName + " 没有核心模板，这轮不放它的恒星。");
                    continue;
                }

                ctx.used.Add(faction);
            }

            if (majors < 4 || majors > 5)
                ctx.audit.Add("主要势力是 " + majors + " 个。设计默认 4 到 5 个，数量以后可以改。");
            if (minors < 4 || minors > 5)
                ctx.audit.Add("次要势力是 " + minors + " 个。设计默认 4 到 5 个，数量以后可以改。");
        }

        static void PlaceCore(GenContext ctx, FactionDef faction)
        {
            FactionCoreTemplate core = faction.core;
            var star = AddStar(ctx, core.starName, core.mapPosition, true, faction.factionId);
            ctx.names.Add(core.starName);
            NoteSpacing(ctx, star);
            AddStellarStation(ctx, star, faction, PlanetRole.Port);
            if (core.planets == null)
                return;
            int owned = 0;
            for (int i = 0; i < core.planets.Count; i++)
            {
                CorePlanetSlot slot = core.planets[i];
                if (slot == null)
                    continue;
                bool claim = slot.owned && owned < faction.Cap;
                if (slot.owned && !claim)
                    ctx.audit.Add(faction.displayName + " 的 " + slot.planetName + " 超出占领上限，改为无主。");
                if (claim)
                    owned++;
                string owner = claim ? faction.factionId : "";
                GravityWellDef well = slot.well != null ? slot.well : ctx.rules.wells[0];
                var planet = AddPlanet(ctx, star, slot.planetName, owner, well, slot.orbitRadius, slot.orbitPeriod, slot.orbitPhase, slot.role, slot.station);
                ctx.names.Add(slot.planetName);
                if (slot.station)
                    AddPlanetStation(ctx, star, planet, faction, slot.role);
            }
        }

        static void NoteSpacing(GenContext ctx, StarRecord star)
        {
            float need = ctx.rules.minStarSpacing;
            for (int i = 0; i < ctx.placed.Count; i++)
            {
                if (Vector2.Distance(ctx.placed[i], star.position) < need)
                {
                    ctx.audit.Add(star.displayName + " 和另一颗手写核心靠得比间距更近，位置保持不动。");
                    break;
                }
            }

            ctx.placed.Add(star.position);
        }

        static void PlacePeriphery(GenContext ctx, int planetCount)
        {
            Vector2 position = PlaceStar(ctx);
            var star = AddStar(ctx, NextName(ctx, false), position, false, "");
            ctx.placed.Add(position);
            AddStellarStation(ctx, star, null, PlanetRole.Wild);
            for (int i = 0; i < planetCount; i++)
            {
                float radius = 9000f + i * 8000f + ctx.rng.Range(0f, 1200f);
                float period = 36f + Mathf.Sqrt(radius) * 0.15f;
                float phase = ctx.rng.Float();
                bool station = ctx.rng.Float() < ctx.rules.wildStationChance;
                var role = (PlanetRole)ctx.rng.Int(0, 6);
                GravityWellDef well = RollWell(ctx);
                var planet = AddPlanet(ctx, star, NextName(ctx, true), "", well, radius, period, phase, role, station);
                if (station)
                    AddPlanetStation(ctx, star, planet, null, role);
            }
        }

        static Vector2 PlaceStar(GenContext ctx)
        {
            float radius = ctx.rules.galaxyRadius;
            float spacing = ctx.rules.minStarSpacing;
            for (int pass = 0; pass < 2; pass++)
            {
                float need = pass == 0 ? spacing : spacing * 0.5f;
                int attempts = pass == 0 ? 60 : 40;
                for (int i = 0; i < attempts; i++)
                {
                    float angle = ctx.rng.Float() * Mathf.PI * 2f;
                    float dist = Mathf.Sqrt(ctx.rng.Float()) * radius;
                    var point = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                    if (Free(point, ctx.placed, need))
                        return point;
                }
            }

            float fallback = ctx.rng.Float() * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(fallback), Mathf.Sin(fallback)) * (radius + 4f);
        }

        static bool Free(Vector2 point, List<Vector2> placed, float spacing)
        {
            float need = spacing * spacing;
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - point).sqrMagnitude < need)
                    return false;
            }

            return true;
        }

        static GravityWellDef RollWell(GenContext ctx)
        {
            List<GravityWellDef> wells = ctx.rules.wells;
            float total = 0f;
            for (int i = 0; i < wells.Count; i++)
            {
                if (wells[i] == null)
                    continue;
                total += WeightAt(ctx.rules, i);
            }

            if (total <= 0f)
                return wells[0];
            float pick = ctx.rng.Float() * total;
            float walked = 0f;
            GravityWellDef chosen = wells[0];
            for (int i = 0; i < wells.Count; i++)
            {
                if (wells[i] == null)
                    continue;
                walked += WeightAt(ctx.rules, i);
                chosen = wells[i];
                if (pick <= walked)
                    return wells[i];
            }

            return chosen;
        }

        static float WeightAt(GalaxyRules rules, int index)
        {
            if (rules.wellWeights == null || index >= rules.wellWeights.Count)
                return 1f;
            return Mathf.Max(0f, rules.wellWeights[index]);
        }

        static string NextName(GenContext ctx, bool planet)
        {
            int spanA = NameA.Length;
            int spanB = NameB.Length;
            int spanC = NameC.Length;
            for (int n = 0; n < 4000; n++)
            {
                int index = ctx.nameCursor++;
                string name = NameA[index % spanA] + NameB[(index / spanA) % spanB];
                if (planet)
                    name += NameC[(index / (spanA * spanB)) % spanC];
                if (ctx.names.Add(name))
                    return name;
            }

            return "外" + ctx.nameCursor;
        }

        static StarRecord AddStar(GenContext ctx, string displayName, Vector2 position, bool core, string factionId)
        {
            var star = new StarRecord();
            star.id = "S" + ctx.starSerial.ToString("000", CultureInfo.InvariantCulture);
            ctx.starSerial++;
            star.displayName = displayName;
            star.position = position;
            star.factionCore = core;
            star.factionId = factionId ?? "";
            star.unlocked = false;
            star.landable = false;
            ctx.world.stars.Add(star);
            return star;
        }

        static PlanetRecord AddPlanet(
            GenContext ctx,
            StarRecord star,
            string displayName,
            string factionId,
            GravityWellDef well,
            float radius,
            float period,
            float phase,
            PlanetRole role,
            bool station)
        {
            var planet = new PlanetRecord();
            planet.id = "P" + ctx.planetSerial.ToString("000", CultureInfo.InvariantCulture);
            ctx.planetSerial++;
            planet.starId = star.id;
            planet.displayName = string.IsNullOrEmpty(displayName) ? NextName(ctx, true) : displayName;
            planet.factionId = factionId ?? "";
            planet.well = well;
            planet.orbitRadius = Mathf.Max(100f, radius);
            planet.orbitPeriod = Mathf.Max(8f, period);
            planet.orbitPhase = phase;
            planet.role = role;
            planet.hasStation = station;
            ctx.world.planets.Add(planet);
            return planet;
        }

        static void AddStellarStation(GenContext ctx, StarRecord star, FactionDef faction, PlanetRole role)
        {
            var station = NewStation(ctx, star.displayName + "轨道站", star.id, "", true);
            FillMarket(ctx, station, faction, role);
        }

        static void AddPlanetStation(GenContext ctx, StarRecord star, PlanetRecord planet, FactionDef faction, PlanetRole role)
        {
            var station = NewStation(ctx, planet.displayName + "站", star.id, planet.id, false);
            FillMarket(ctx, station, faction, role);
        }

        static StationRecord NewStation(GenContext ctx, string displayName, string starId, string planetId, bool stellar)
        {
            var station = new StationRecord();
            station.id = "T" + ctx.stationSerial.ToString("000", CultureInfo.InvariantCulture);
            ctx.stationSerial++;
            station.displayName = displayName;
            station.starId = starId;
            station.planetId = planetId ?? "";
            station.stellar = stellar;
            station.stock = new List<StockLine>();
            ctx.world.stations.Add(station);
            return station;
        }

        static void FillMarket(GenContext ctx, StationRecord station, FactionDef faction, PlanetRole role)
        {
            List<GoodDef> goods = ctx.rules.goods;
            if (goods == null)
                return;
            for (int i = 0; i < goods.Count; i++)
            {
                GoodDef good = goods[i];
                if (good == null)
                    continue;
                bool export = Exports(faction, role, good);
                bool import = !export && Imports(faction, role, good);
                float target = export ? 32f : import ? 78f : 50f;
                float stock = export ? 76f : import ? 24f : 50f;
                stock = Mathf.Clamp(stock + ctx.rng.Range(-5f, 5f), 1f, 99f);
                station.stock.Add(new StockLine
                {
                    good = good,
                    stock = stock,
                    capacity = 100f,
                    target = target,
                    eventMultiplier = 1f
                });
            }
        }

        static bool Exports(FactionDef faction, PlanetRole role, GoodDef good)
        {
            if (Listed(faction != null ? faction.exports : null, good))
                return true;
            return RoleExports(role, good.goodId);
        }

        static bool Imports(FactionDef faction, PlanetRole role, GoodDef good)
        {
            if (Listed(faction != null ? faction.imports : null, good))
                return true;
            return RoleImports(role, good.goodId);
        }

        static bool Listed(List<GoodDef> goods, GoodDef good)
        {
            if (goods == null)
                return false;
            for (int i = 0; i < goods.Count; i++)
            {
                if (goods[i] == good)
                    return true;
            }

            return false;
        }

        static bool RoleExports(PlanetRole role, string goodId)
        {
            if (role == PlanetRole.Farm && goodId == "水")
                return true;
            if (role == PlanetRole.Mine && goodId == "矿石")
                return true;
            if (role == PlanetRole.Industry && goodId == "合金")
                return true;
            if (role == PlanetRole.Refinery && goodId == "燃料")
                return true;
            if (role == PlanetRole.Port && goodId == "香料")
                return true;
            return false;
        }

        static bool RoleImports(PlanetRole role, string goodId)
        {
            if (role == PlanetRole.Farm && goodId == "合金")
                return true;
            if (role == PlanetRole.Mine && goodId == "药品")
                return true;
            if (role == PlanetRole.Industry && goodId == "矿石")
                return true;
            if (role == PlanetRole.Refinery && goodId == "水")
                return true;
            if (role == PlanetRole.Port && goodId == "燃料")
                return true;
            return false;
        }

        static void LinkRoutes(GenContext ctx)
        {
            var cores = new List<int>();
            var wild = new List<int>();
            for (int i = 0; i < ctx.world.stars.Count; i++)
            {
                if (ctx.world.stars[i].factionCore)
                    cores.Add(i);
                else
                    wild.Add(i);
            }

            SpanCores(ctx, cores);
            LinkPrivate(ctx, cores, wild);
            LinkSmugglers(ctx, wild);
            ExpireSome(ctx);
        }

        static void SpanCores(GenContext ctx, List<int> cores)
        {
            if (cores.Count < 2)
                return;
            var inside = new HashSet<int>();
            inside.Add(cores[0]);
            while (inside.Count < cores.Count)
            {
                float best = float.MaxValue;
                int from = cores[0];
                int to = cores[0];
                for (int a = 0; a < cores.Count; a++)
                {
                    if (!inside.Contains(cores[a]))
                        continue;
                    for (int b = 0; b < cores.Count; b++)
                    {
                        if (inside.Contains(cores[b]))
                            continue;
                        float dist = Vector2.Distance(ctx.world.stars[cores[a]].position, ctx.world.stars[cores[b]].position);
                        if (dist < best - 0.0001f || (Mathf.Abs(dist - best) <= 0.0001f && cores[b] < to))
                        {
                            best = dist;
                            from = cores[a];
                            to = cores[b];
                        }
                    }
                }

                inside.Add(to);
                AddRoute(ctx, RouteKind.Official, ctx.world.stars[from].id, ctx.world.stars[to].id);
            }
        }

        static void LinkPrivate(GenContext ctx, List<int> cores, List<int> wild)
        {
            if (wild.Count == 0)
                return;
            for (int i = 0; i < cores.Count; i++)
            {
                int nearest = wild[0];
                float best = float.MaxValue;
                for (int w = 0; w < wild.Count; w++)
                {
                    float dist = Vector2.Distance(ctx.world.stars[cores[i]].position, ctx.world.stars[wild[w]].position);
                    if (dist < best - 0.0001f || (Mathf.Abs(dist - best) <= 0.0001f && wild[w] < nearest))
                    {
                        best = dist;
                        nearest = wild[w];
                    }
                }

                AddRoute(ctx, RouteKind.Private, ctx.world.stars[cores[i]].id, ctx.world.stars[nearest].id);
            }
        }

        static void LinkSmugglers(GenContext ctx, List<int> wild)
        {
            if (wild.Count < 2 || ctx.rules.smugglerRoutes <= 0)
                return;
            int made = 0;
            int tries = 0;
            int limit = ctx.rules.smugglerRoutes * 12;
            while (made < ctx.rules.smugglerRoutes && tries < limit)
            {
                tries++;
                int a = wild[ctx.rng.Int(0, wild.Count)];
                int b = wild[ctx.rng.Int(0, wild.Count)];
                if (a == b)
                    continue;
                if (AddRoute(ctx, RouteKind.Smuggler, ctx.world.stars[a].id, ctx.world.stars[b].id))
                    made++;
            }
        }

        static void ExpireSome(GenContext ctx)
        {
            var smugglers = new List<RouteRecord>();
            for (int i = 0; i < ctx.world.routes.Count; i++)
            {
                if (ctx.world.routes[i].kind == RouteKind.Smuggler)
                    smugglers.Add(ctx.world.routes[i]);
            }

            int canExpire = Mathf.Max(0, smugglers.Count - 1);
            int expire = Mathf.Min(ctx.rules.expiredRoutes, canExpire);
            if (expire < ctx.rules.expiredRoutes)
                ctx.audit.Add("走私航路不够改成过期，实际过期 " + expire + " 条。");
            for (int i = 0; i < expire; i++)
                smugglers[smugglers.Count - 1 - i].kind = RouteKind.Expired;
        }

        static bool AddRoute(GenContext ctx, RouteKind kind, string fromId, string toId)
        {
            if (fromId == toId)
                return false;
            string left = fromId;
            string right = toId;
            if (string.CompareOrdinal(left, right) > 0)
            {
                left = toId;
                right = fromId;
            }

            string pair = left + ">" + right;
            if (!ctx.pairs.Add(pair))
                return false;
            var route = new RouteRecord();
            route.id = "L" + ctx.routeSerial.ToString("000", CultureInfo.InvariantCulture);
            ctx.routeSerial++;
            route.kind = kind;
            route.fromStarId = left;
            route.toStarId = right;
            route.discovered = false;
            ctx.world.routes.Add(route);
            return true;
        }

        static void PickStart(GenContext ctx)
        {
            string want = ctx.rules.startFactionId;
            StarRecord fallback = ctx.world.stars.Count > 0 ? ctx.world.stars[0] : null;
            StarRecord chosen = fallback;
            for (int i = 0; i < ctx.world.stars.Count; i++)
            {
                StarRecord star = ctx.world.stars[i];
                if (star.factionCore && star.factionId == want)
                {
                    chosen = star;
                    break;
                }
            }

            ctx.world.startStarId = chosen != null ? chosen.id : "";
        }

        static void DiscoverFromStart(GenContext ctx)
        {
            string start = ctx.world.startStarId;
            for (int i = 0; i < ctx.world.routes.Count; i++)
            {
                RouteRecord route = ctx.world.routes[i];
                if (route.kind != RouteKind.Official && route.kind != RouteKind.Private)
                    continue;
                if (route.fromStarId == start || route.toStarId == start)
                    route.discovered = true;
            }

            RefreshUnlocks(ctx.world);
        }

        public static void RefreshUnlocks(WorldSnapshot world)
        {
            if (world == null || world.stars == null)
                return;
            for (int i = 0; i < world.stars.Count; i++)
                world.stars[i].unlocked = world.stars[i].id == world.startStarId;
            if (world.routes == null)
                return;
            for (int i = 0; i < world.routes.Count; i++)
            {
                RouteRecord route = world.routes[i];
                if (route == null || !route.discovered)
                    continue;
                StarRecord from = world.FindStar(route.fromStarId);
                StarRecord to = world.FindStar(route.toStarId);
                if (from != null)
                    from.unlocked = true;
                if (to != null)
                    to.unlocked = true;
            }
        }

        static void Audit(GenContext ctx)
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < ctx.world.planets.Count; i++)
            {
                PlanetRecord planet = ctx.world.planets[i];
                if (string.IsNullOrEmpty(planet.factionId))
                    continue;
                int have;
                counts.TryGetValue(planet.factionId, out have);
                counts[planet.factionId] = have + 1;
            }

            for (int i = 0; i < ctx.used.Count; i++)
            {
                FactionDef faction = ctx.used[i];
                int have;
                counts.TryGetValue(faction.factionId, out have);
                if (have > faction.Cap)
                    ctx.audit.Add(faction.displayName + " 占领 " + have + "，超过上限 " + faction.Cap + "。");
                if (faction.rank == FactionRank.Minor && have != 1)
                    ctx.audit.Add(faction.displayName + " 是次要势力，占领应是 1，现在是 " + have + "。");
            }

            for (int i = 0; i < ctx.world.stars.Count; i++)
            {
                StarRecord star = ctx.world.stars[i];
                if (star.landable)
                    ctx.audit.Add(star.displayName + " 被标成可以降落。恒星不能降落。");
                if (ctx.world.StellarStation(star.id) == null)
                    ctx.audit.Add(star.displayName + " 没有轨道站。");
            }

            if (ctx.world.planets.Count != ctx.rules.planetTarget)
                ctx.audit.Add("行星数是 " + ctx.world.planets.Count + "，目标是 " + ctx.rules.planetTarget + "。");
        }
    }
}
