using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    public sealed class ShipReport
    {
        public float mass;
        public float thrust;
        public float powerOutput;
        public float powerDraw;
        public float heatCapacity;
        public float heatOutput;
        public float heatUncovered;
        public float heatDissipation;
        public int weapons;
        public int missiles;
        public int corridors;
        public float homingTurnRate;
        public int valves;
        public int damageCovered;
        public int heatCovered;
        public readonly List<string> errors = new List<string>();
        public readonly List<bool> valid = new List<bool>();
        public readonly List<bool> online = new List<bool>();
        public readonly List<bool> crewed = new List<bool>();
        public readonly List<bool> cooled = new List<bool>();
        public readonly List<bool> salvaged = new List<bool>();
        public readonly HashSet<int> heatCells = new HashSet<int>();
        public readonly HashSet<int> damageCells = new HashSet<int>();
    }

    public static class ShipEvaluator
    {
        public static ShipReport Evaluate(ShipBuild build)
        {
            var report = new ShipReport();
            if (build == null)
            {
                report.errors.Add("没有拼船方案。");
                return report;
            }

            if (build.hull == null)
            {
                report.errors.Add("拼船方案没有船体。");
                return report;
            }

            var occupied = new CellMap();
            float sourceOutput = 0f;
            var consumers = new List<int>();

            for (int i = 0; i < build.rooms.Count; i++)
            {
                RoomPlacement placement = build.rooms[i];
                report.valid.Add(false);
                report.online.Add(false);
                report.crewed.Add(false);
                if (placement == null || placement.room == null)
                {
                    report.errors.Add("第 " + (i + 1) + " 个舱是空引用。");
                    continue;
                }

                if (!Fits(build, placement, occupied, out string fitError))
                {
                    report.errors.Add(placement.room.displayName + "：" + fitError);
                    continue;
                }

                report.valid[i] = true;
                report.mass += placement.room.mass;
                Mark(build.hull, placement, occupied, i);
                if (placement.room.powerOutput > 0f)
                {
                    report.online[i] = true;
                    sourceOutput += placement.room.powerOutput;
                }
                else
                {
                    consumers.Add(i);
                }
            }

            report.mass += build.hull.baseMass;
            report.powerOutput = sourceOutput;
            MarkCrew(build, occupied, report);
            float powerLeft = sourceOutput;
            for (int n = 0; n < consumers.Count; n++)
            {
                int index = consumers[n];
                RoomDef room = build.rooms[index].room;
                if (NeedsEnergy(room) && !report.crewed[index])
                    continue;
                if (room.powerDraw <= powerLeft)
                {
                    report.online[index] = true;
                    powerLeft -= room.powerDraw;
                    report.powerDraw += room.powerDraw;
                }
            }

            ReportEnergy(build, report);

            for (int i = 0; i < build.rooms.Count; i++)
            {
                if (!report.online[i] || build.rooms[i] == null || build.rooms[i].room == null)
                    continue;
                if (NeedsEnergy(build.rooms[i].room) && !report.crewed[i])
                    continue;
                RoomDef room = build.rooms[i].room;
                report.thrust += room.thrust;
                report.heatCapacity += room.heatCapacity;
                report.heatOutput += room.heatOutput;
                report.heatDissipation += room.heatDissipation;
                if (room.kind == RoomKind.Weapon && room.weapon != null)
                    report.weapons++;
                if (room.kind == RoomKind.Missile && room.weapon != null)
                {
                    report.missiles++;
                    if (room.weapon.homing)
                        report.homingTurnRate = Mathf.Max(report.homingTurnRate, room.weapon.homingTurnRate);
                }
                if (room.sealsAir)
                    report.valves++;
            }

            MarkRanges(build, report);
            return report;
        }

        public static void CollectRange(HullDef hull, RoomPlacement source, HashSet<int> cells)
        {
            if (hull == null || source == null || source.room == null || cells == null)
                return;
            int range = source.room.effectRange;
            var origin = new List<int>();
            for (int y = 0; y < source.SpanY; y++)
            {
                for (int x = 0; x < source.SpanX; x++)
                {
                    int cellX = source.x + x;
                    int cellY = source.y + y;
                    if (!hull.IsOpen(cellX, cellY))
                        continue;
                    origin.Add(cellY * hull.width + cellX);
                }
            }

            for (int y = 0; y < hull.height; y++)
            {
                for (int x = 0; x < hull.width; x++)
                {
                    if (!hull.IsOpen(x, y))
                        continue;
                    for (int n = 0; n < origin.Count; n++)
                    {
                        int ox = origin[n] % hull.width;
                        int oy = origin[n] / hull.width;
                        if (Mathf.Max(Mathf.Abs(x - ox), Mathf.Abs(y - oy)) <= range)
                        {
                            cells.Add(y * hull.width + x);
                            break;
                        }
                    }
                }
            }
        }

        static void MarkRanges(ShipBuild build, ShipReport report)
        {
            int count = build.rooms.Count;
            for (int i = 0; i < count; i++)
            {
                report.cooled.Add(false);
                report.salvaged.Add(false);
            }

            var heat = new HashSet<int>();
            var damage = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                if (!report.valid[i] || !report.online[i] || (NeedsEnergy(build.rooms[i].room) && !report.crewed[i]))
                    continue;
                RoomPlacement placement = build.rooms[i];
                RoomDef room = placement.room;
                if (room.kind == RoomKind.Radiator && room.heatDissipation > 0f)
                    CollectRange(build.hull, placement, heat);
                if (room.kind == RoomKind.DamageControl && room.suppressesFire)
                    CollectRange(build.hull, placement, damage);
            }

            report.heatCells.UnionWith(heat);
            report.damageCells.UnionWith(damage);
            int width = build.hull.width;
            for (int i = 0; i < count; i++)
            {
                if (!report.valid[i] || build.rooms[i].room == null)
                    continue;
                if (build.rooms[i].room.kind == RoomKind.Corridor)
                    continue;
                RoomPlacement placement = build.rooms[i];
                bool inHeat = false;
                bool inDamage = false;
                for (int y = 0; y < placement.SpanY; y++)
                {
                    for (int x = 0; x < placement.SpanX; x++)
                    {
                        int key = (placement.y + y) * width + (placement.x + x);
                        if (heat.Contains(key))
                            inHeat = true;
                        if (damage.Contains(key))
                            inDamage = true;
                    }
                }

                report.cooled[i] = inHeat;
                report.salvaged[i] = inDamage;
                if (inHeat)
                    report.heatCovered++;
                if (inDamage)
                    report.damageCovered++;
            }

            for (int i = 0; i < count; i++)
            {
                if (!report.valid[i] || !report.online[i] || build.rooms[i] == null || build.rooms[i].room == null)
                    continue;
                if (NeedsEnergy(build.rooms[i].room) && !report.crewed[i])
                    continue;
                if (build.rooms[i].room.heatOutput <= 0f || report.cooled[i])
                    continue;
                report.heatUncovered += build.rooms[i].room.heatOutput;
            }
        }

        public static bool CrewCanWalk(RoomDef room)
        {
            return room != null && room.kind != RoomKind.Armor;
        }

        public static bool NeedsEnergy(RoomDef room)
        {
            return room != null
                && room.kind != RoomKind.Armor
                && room.kind != RoomKind.Corridor
                && room.kind != RoomKind.Reactor;
        }

        static void MarkCrew(ShipBuild build, CellMap occupied, ShipReport report)
        {
            int width = build.hull.width;
            int height = build.hull.height;
            var walk = new bool[width * height];
            var seen = new bool[width * height];
            var queue = new Queue<int>();
            NoteWalk(build, occupied.floor, walk, seen, queue);
            NoteWalk(build, occupied.half, walk, seen, queue);

            while (queue.Count > 0)
            {
                int key = queue.Dequeue();
                int x = key % width;
                int y = key / width;
                TryWalk(walk, seen, queue, width, height, x + 1, y);
                TryWalk(walk, seen, queue, width, height, x - 1, y);
                TryWalk(walk, seen, queue, width, height, x, y + 1);
                TryWalk(walk, seen, queue, width, height, x, y - 1);
            }

            for (int i = 0; i < build.rooms.Count; i++)
            {
                if (!report.valid[i] || build.rooms[i] == null || build.rooms[i].room == null)
                    continue;
                RoomDef room = build.rooms[i].room;
                if (!CrewCanWalk(room))
                {
                    report.crewed[i] = true;
                    continue;
                }

                if (room.kind == RoomKind.Corridor)
                    report.corridors++;
                RoomPlacement placement = build.rooms[i];
                bool reached = false;
                for (int y = 0; y < placement.SpanY && !reached; y++)
                {
                    for (int x = 0; x < placement.SpanX; x++)
                    {
                        int cell = (placement.y + y) * width + (placement.x + x);
                        if (cell >= 0 && cell < seen.Length && seen[cell])
                        {
                            reached = true;
                            break;
                        }
                    }
                }

                report.crewed[i] = reached;
            }
        }

        static void ReportEnergy(ShipBuild build, ShipReport report)
        {
            for (int i = 0; i < build.rooms.Count; i++)
            {
                if (!report.valid[i] || build.rooms[i] == null || build.rooms[i].room == null)
                    continue;
                RoomDef room = build.rooms[i].room;
                if (!NeedsEnergy(room) || report.crewed[i])
                    continue;
                if (room.kind == RoomKind.Bridge || room.kind == RoomKind.Thruster)
                    continue;
                report.errors.Add(room.displayName + "没有能源");
            }

            Require(build, report, RoomKind.Bridge, "舰桥");
            Require(build, report, RoomKind.Reactor, "反应堆");
            Require(build, report, RoomKind.Thruster, "主推");
        }

        static void TryWalk(bool[] walk, bool[] seen, Queue<int> queue, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            int key = y * width + x;
            if (seen[key] || !walk[key])
                return;
            seen[key] = true;
            queue.Enqueue(key);
        }

        static void Require(ShipBuild build, ShipReport report, RoomKind kind, string label)
        {
            bool placed = false;
            bool reached = false;
            bool powered = false;
            for (int i = 0; i < build.rooms.Count; i++)
            {
                if (!report.valid[i] || build.rooms[i].room == null || build.rooms[i].room.kind != kind)
                    continue;
                placed = true;
                if (!report.crewed[i])
                    continue;
                reached = true;
                if (report.online[i])
                    powered = true;
            }

            if (!placed)
                report.errors.Add("还缺" + label);
            else if (!reached)
                report.errors.Add(label + "没有能源");
            else if (!powered)
                report.errors.Add(label + "没有电");
        }

        public static bool Fits(ShipBuild build, RoomPlacement placement, CellMap occupied, out string error)
        {
            error = null;
            HullDef hull = build != null ? build.hull : null;
            RoomDef room = placement != null ? placement.room : null;
            if (hull == null || room == null)
            {
                error = hull == null ? "没有船体。" : "先在左侧选一个舱室。";
                return false;
            }

            for (int y = 0; y < placement.SpanY; y++)
            {
                for (int x = 0; x < placement.SpanX; x++)
                {
                    int cellX = placement.x + x;
                    int cellY = placement.y + y;
                    if (!hull.IsOpen(cellX, cellY))
                    {
                        error = "超出船壳 (" + cellX + "," + cellY + ")";
                        return false;
                    }

                    int key = cellY * hull.width + cellX;
                    if (occupied != null && !occupied.Allows(build, room, key))
                    {
                        error = "与已有舱室重叠 (" + cellX + "," + cellY + ")";
                        return false;
                    }
                }
            }

            return true;
        }

        static void Mark(HullDef hull, RoomPlacement placement, CellMap occupied, int index)
        {
            Dictionary<int, int> slot = placement.room.halfCell ? occupied.half : occupied.floor;
            for (int y = 0; y < placement.SpanY; y++)
            {
                for (int x = 0; x < placement.SpanX; x++)
                {
                    int cellX = placement.x + x;
                    int cellY = placement.y + y;
                    slot[cellY * hull.width + cellX] = index;
                }
            }
        }

        static void NoteWalk(ShipBuild build, Dictionary<int, int> slots, bool[] walk, bool[] seen, Queue<int> queue)
        {
            foreach (KeyValuePair<int, int> pair in slots)
            {
                RoomDef room = build.rooms[pair.Value].room;
                if (!CrewCanWalk(room))
                    continue;
                walk[pair.Key] = true;
                if (room.kind != RoomKind.Reactor || seen[pair.Key])
                    continue;
                queue.Enqueue(pair.Key);
                seen[pair.Key] = true;
            }
        }
    }

    public sealed class CellMap
    {
        public readonly Dictionary<int, int> floor = new Dictionary<int, int>();
        public readonly Dictionary<int, int> half = new Dictionary<int, int>();

        public bool Allows(ShipBuild build, RoomDef room, int key)
        {
            if (room == null)
                return false;
            bool hasFloor = floor.TryGetValue(key, out int floorIndex);
            bool hasHalf = half.ContainsKey(key);
            if (room.halfCell)
            {
                if (hasHalf)
                    return false;
                if (!hasFloor)
                    return true;
                RoomPlacement under = build != null && floorIndex >= 0 && floorIndex < build.rooms.Count
                    ? build.rooms[floorIndex]
                    : null;
                return under != null && under.room != null && under.room.kind == RoomKind.Corridor;
            }

            if (hasFloor)
                return false;
            return !hasHalf || room.kind == RoomKind.Corridor;
        }

        public static CellMap Build(ShipBuild build)
        {
            var map = new CellMap();
            if (build == null || build.hull == null)
                return map;
            for (int i = 0; i < build.rooms.Count; i++)
            {
                RoomPlacement placement = build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                if (!ShipEvaluator.Fits(build, placement, map, out _))
                    continue;
                for (int y = 0; y < placement.SpanY; y++)
                {
                    for (int x = 0; x < placement.SpanX; x++)
                    {
                        int key = (placement.y + y) * build.hull.width + (placement.x + x);
                        Dictionary<int, int> slot = placement.room.halfCell ? map.half : map.floor;
                        slot[key] = i;
                    }
                }
            }

            return map;
        }
    }
}
