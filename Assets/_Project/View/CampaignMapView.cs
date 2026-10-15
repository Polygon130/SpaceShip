using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip
{
    public sealed class CampaignMapView
    {
        readonly VisualElement parent;
        readonly CampaignTravel travel;
        readonly Action<string> onStatus;
        readonly Action onGenerate;
        readonly Action onMutated;
        readonly Action onCombat;
        readonly bool allowGenerate;
        VisualElement root;
        VisualElement map;
        Label progressLabel;
        readonly List<OrbitMark> orbits = new List<OrbitMark>();
        int builtRevision = -1;
        bool systemMap;
        bool showAll = true;
        string selectedStar = "";
        const float MapSize = 640f;

        sealed class OrbitMark
        {
            public VisualElement dot;
            public PlanetRecord planet;
            public bool stellar;
        }

        public CampaignMapView(
            VisualElement parent,
            CampaignTravel travel,
            bool allowGenerate,
            Action<string> onStatus,
            Action onGenerate,
            Action onMutated,
            Action onCombat)
        {
            this.parent = parent;
            this.travel = travel;
            this.allowGenerate = allowGenerate;
            this.onStatus = onStatus;
            this.onGenerate = onGenerate;
            this.onMutated = onMutated;
            this.onCombat = onCombat;
        }

        public void Live()
        {
            if (travel == null)
                return;
            if (builtRevision != travel.revision)
                Rebuild();
            else
            {
                MoveOrbits();
                RefreshProgress();
            }
        }

        public void Hide()
        {
            if (root != null)
                root.style.display = DisplayStyle.None;
        }

        public void Show()
        {
            builtRevision = -1;
            if (root != null)
                root.style.display = DisplayStyle.Flex;
            Live();
        }

        public void Rebuild()
        {
            builtRevision = travel != null ? travel.revision : 0;
            if (travel != null && travel.focusSystem)
            {
                systemMap = true;
                travel.focusSystem = false;
            }

            orbits.Clear();
            parent.Clear();
            root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.flexDirection = FlexDirection.Row;
            root.style.backgroundColor = new Color(0.09f, 0.1f, 0.12f, 1f);
            root.style.paddingLeft = 12;
            root.style.paddingTop = 12;
            root.style.paddingRight = 12;
            root.style.paddingBottom = 12;
            parent.Add(root);

            var left = Column(340);
            var center = new VisualElement();
            center.style.flexGrow = 1;
            center.style.alignItems = Align.Center;
            var right = Column(360);
            root.Add(left);
            root.Add(center);
            root.Add(right);

            FillStatus(left);
            map = new VisualElement();
            map.style.width = MapSize;
            map.style.height = MapSize;
            map.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
            map.style.position = Position.Relative;
            center.Add(map);
            if (travel == null || !travel.Ready)
            {
                map.Add(Hint("生成之后，这里画出恒星。"));
                right.Add(Hint("星图上不能买卖。停靠轨道站之后才转运货物。"));
            }
            else if (systemMap)
            {
                DrawSystem();
                FillSystemActions(left);
                FillDock(right);
            }
            else
            {
                DrawGalaxy();
                FillGalaxyActions(left);
                FillStarDetail(right);
            }

            UiFonts.Apply(root);
        }

        void FillStatus(VisualElement column)
        {
            column.Add(Head(systemMap ? "本系" : "星图"));
            if (travel == null || travel.world == null)
            {
                column.Add(Hint("没有星图快照。"));
                return;
            }

            WorldSnapshot world = travel.world;
            column.Add(Hint(world.generated ? world.summary : "快照还是空的。种子写在规则里，生成一次就固定下来。"));
            if (!string.IsNullOrEmpty(world.fingerprint))
                column.Add(Hint("指纹 " + world.fingerprint));
            column.Add(Hint(travel.LocationLine()));
            column.Add(Hint(ModeLabel()));
            if (travel.Ready)
                column.Add(Hint(travel.SpeedLine()));
            progressLabel = Hint("");
            column.Add(progressLabel);
            RefreshProgress();
            column.Add(Hint(travel.status));
            if (!world.generated && allowGenerate)
                column.Add(ActionButton("按种子生成", onGenerate));
            if (travel.Ready)
            {
                column.Add(Hint("信用 " + travel.credits.ToString("0", CultureInfo.InvariantCulture)));
                column.Add(Hint(HoldLine()));
                column.Add(ActionButton(systemMap ? "返回星图" : "查看本系", ToggleMap));
                column.Add(ActionButton(showAll ? "星图：全部恒星" : "星图：只显示已发现", ToggleFog));
                if (onCombat != null)
                    column.Add(ActionButton("进入战斗（常规引擎）", onCombat));
                else
                    column.Add(Hint("播放星图场景可进入战斗。这个窗口走曲速和脉冲巡航。"));
            }
        }

        void FillGalaxyActions(VisualElement column)
        {
            if (travel.phase == TravelPhase.Warping || travel.phase == TravelPhase.Pulsing)
                return;
            column.Add(Head("航路"));
            if (travel.phase != TravelPhase.AtStarOrbit && !(travel.phase == TravelPhase.Docked && string.IsNullOrEmpty(travel.planetId)))
            {
                column.Add(Hint("人在行星附近。曲速要先脉冲巡航回恒星轨道。"));
                return;
            }

            int links = 0;
            List<RouteRecord> routes = travel.world.routes;
            if (routes == null)
                return;
            for (int i = 0; i < routes.Count; i++)
            {
                RouteRecord route = routes[i];
                if (route == null || !travel.world.RouteTouches(route, travel.starId))
                    continue;
                links++;
                string otherId = travel.world.OtherEnd(route, travel.starId);
                StarRecord other = travel.world.FindStar(otherId);
                string name = other != null && (route.discovered || showAll) ? other.displayName : "未探测";
                string kind = RouteDef.Label(route.kind);
                if (!route.discovered)
                {
                    string id = route.id;
                    column.Add(ActionButton("探测 " + kind + " → " + name, () =>
                    {
                        string error;
                        Finish(travel.TryDiscover(id, out error), error);
                    }));
                    continue;
                }

                if (!RouteDef.AllowsWarp(travel.world.rules, route.kind))
                {
                    column.Add(Hint(kind + " → " + name + "，不能曲速。"));
                    continue;
                }

                string target = otherId;
                column.Add(ActionButton("曲速 " + kind + " → " + name, () =>
                {
                    string error;
                    Finish(travel.TryWarp(target, out error), error);
                }));
            }

            if (links == 0)
                column.Add(Hint("这里没有出发的航路。"));
        }

        void FillSystemActions(VisualElement column)
        {
            if (travel.phase == TravelPhase.Warping || travel.phase == TravelPhase.Pulsing)
                return;
            column.Add(Head("脉冲巡航"));
            var planets = new List<PlanetRecord>();
            travel.world.CountPlanets(travel.starId, planets);
            if (travel.phase != TravelPhase.AtStarOrbit)
                column.Add(ActionButton("脉冲巡航回恒星轨道", () =>
                {
                    string error;
                    Finish(travel.TryPulseToStar(out error), error);
                }));
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetRecord planet = planets[i];
                string id = planet.id;
                string owner = OwnerName(planet.factionId);
                column.Add(ActionButton("脉冲巡航 → " + planet.displayName + " " + owner, () =>
                {
                    string error;
                    Finish(travel.TryPulseToPlanet(id, out error), error);
                }));
            }

            if (travel.phase == TravelPhase.AtPlanetOrbit || travel.phase == TravelPhase.AtStarOrbit)
                column.Add(ActionButton("停靠", () =>
                {
                    string error;
                    Finish(travel.TryDock(out error), error);
                }));
            if (travel.phase == TravelPhase.Docked)
                column.Add(ActionButton("离开车站", () =>
                {
                    string error;
                    Finish(travel.TryUndock(out error), error);
                }));
        }

        void FillStarDetail(VisualElement column)
        {
            column.Add(Head("恒星"));
            column.Add(Hint("恒星不能降落。交互在轨道站，买卖要等脉冲巡航过去再停靠。"));
            string id = string.IsNullOrEmpty(selectedStar) ? travel.starId : selectedStar;
            StarRecord star = travel.world.FindStar(id);
            if (star == null)
                return;
            column.Add(Hint(star.displayName + (star.factionCore ? "  核心" : "  外围")));
            column.Add(Hint(string.IsNullOrEmpty(star.factionId) ? "无主星域" : OwnerName(star.factionId)));
            column.Add(Hint(star.unlocked ? "已解锁" : "未解锁"));
            var planets = new List<PlanetRecord>();
            travel.world.CountPlanets(star.id, planets);
            int owned = 0;
            for (int i = 0; i < planets.Count; i++)
            {
                if (!string.IsNullOrEmpty(planets[i].factionId))
                    owned++;
            }

            column.Add(Hint("行星 " + planets.Count + "，有主 " + owned + "，无主 " + (planets.Count - owned)));
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetRecord planet = planets[i];
                string well = planet.well != null ? planet.well.displayName : "无重力井";
                column.Add(Hint(planet.displayName + "  " + OwnerName(planet.factionId) + "  " + FactionCoreTemplate.RoleLabel(planet.role) + "  " + well));
            }

            column.Add(Head("占领"));
            column.Add(Hint(WorldGen.OwnershipReport(travel.world)));
        }

        void FillDock(VisualElement column)
        {
            column.Add(Head("轨道站"));
            if (travel.phase != TravelPhase.Docked)
            {
                column.Add(Hint("还没停靠。星图上没有一键买卖。"));
                column.Add(Hint(WorldGen.OwnershipReport(travel.world)));
                return;
            }

            StationRecord station = travel.world.FindStation(travel.stationId);
            if (station == null)
                return;
            column.Add(Hint(station.displayName + (station.stellar ? "  绕恒星" : "  绕行星")));
            if (station.stock == null)
                return;
            for (int i = 0; i < station.stock.Count; i++)
            {
                StockLine line = station.stock[i];
                if (line == null || line.good == null)
                    continue;
                GoodDef good = line.good;
                float price = travel.Quote(station, line);
                float pressure = MarketMath.StockPressure(line.stock, line.capacity, line.target);
                column.Add(Hint(good.displayName
                    + "  库存 " + line.stock.ToString("0", CultureInfo.InvariantCulture)
                    + "  压力 " + pressure.ToString("0.00", CultureInfo.InvariantCulture)
                    + "  价格 " + price.ToString("0.0", CultureInfo.InvariantCulture)
                    + "  船上 " + travel.CargoOf(good)));
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                GoodDef buyGood = good;
                row.Add(ActionButton("买入", () =>
                {
                    string error;
                    Finish(travel.TryTransfer(buyGood, true, out error), error);
                }));
                row.Add(ActionButton("卖出", () =>
                {
                    string error;
                    Finish(travel.TryTransfer(buyGood, false, out error), error);
                }));
                column.Add(row);
            }
        }

        void DrawGalaxy()
        {
            DrawRoutes();
            if (travel.world.stars == null)
                return;
            for (int i = 0; i < travel.world.stars.Count; i++)
            {
                StarRecord star = travel.world.stars[i];
                if (star == null)
                    continue;
                if (!showAll && !star.unlocked && star.id != travel.starId)
                    continue;
                Vector2 local = GalaxyPoint(star.position);
                var dot = Dot(ColorOf(star), star.id == travel.starId ? 18f : 12f);
                dot.style.left = local.x - 8f;
                dot.style.top = local.y - 8f;
                string id = star.id;
                dot.RegisterCallback<ClickEvent>(_ =>
                {
                    selectedStar = id;
                    builtRevision = -1;
                    Rebuild();
                });
                map.Add(dot);
                if (star.unlocked || star.id == travel.starId || showAll)
                {
                    var label = new Label(star.displayName);
                    label.style.position = Position.Absolute;
                    label.style.left = local.x + 10f;
                    label.style.top = local.y - 16f;
                    label.pickingMode = PickingMode.Ignore;
                    map.Add(label);
                }
            }
        }

        void DrawRoutes()
        {
            if (travel.world.routes == null)
                return;
            for (int i = 0; i < travel.world.routes.Count; i++)
            {
                RouteRecord route = travel.world.routes[i];
                if (route == null)
                    continue;
                StarRecord from = travel.world.FindStar(route.fromStarId);
                StarRecord to = travel.world.FindStar(route.toStarId);
                if (from == null || to == null)
                    continue;
                if (!showAll && !route.discovered)
                    continue;
                Vector2 a = GalaxyPoint(from.position);
                Vector2 b = GalaxyPoint(to.position);
                var line = new VisualElement();
                line.pickingMode = PickingMode.Ignore;
                line.style.position = Position.Absolute;
                line.style.height = 2;
                float dx = b.x - a.x;
                float dy = b.y - a.y;
                float length = Mathf.Sqrt(dx * dx + dy * dy);
                line.style.width = length;
                line.style.left = a.x;
                line.style.top = a.y;
                line.style.transformOrigin = new TransformOrigin(new Length(0f), Length.Percent(50f));
                line.style.rotate = new Rotate(Angle.Degrees(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg));
                Color color = route.discovered ? new Color(0.45f, 0.55f, 0.62f, 0.9f) : new Color(0.35f, 0.35f, 0.38f, 0.45f);
                if (route.kind == RouteKind.Smuggler)
                    color = new Color(0.72f, 0.45f, 0.28f, route.discovered ? 0.9f : 0.4f);
                if (route.kind == RouteKind.Expired)
                    color = new Color(0.45f, 0.28f, 0.28f, 0.7f);
                line.style.backgroundColor = color;
                map.Add(line);
            }
        }

        void DrawSystem()
        {
            var starDot = Dot(new Color(0.95f, 0.82f, 0.45f, 1f), 22f);
            starDot.style.left = MapSize * 0.5f - 11f;
            starDot.style.top = MapSize * 0.5f - 11f;
            starDot.pickingMode = PickingMode.Ignore;
            map.Add(starDot);
            var starLabel = new Label("恒星");
            starLabel.style.position = Position.Absolute;
            starLabel.style.left = MapSize * 0.5f + 14f;
            starLabel.style.top = MapSize * 0.5f - 12f;
            starLabel.pickingMode = PickingMode.Ignore;
            map.Add(starLabel);

            var stellar = new OrbitMark();
            stellar.stellar = true;
            stellar.dot = Dot(new Color(0.55f, 0.75f, 0.9f, 1f), 14f);
            map.Add(stellar.dot);
            orbits.Add(stellar);

            var planets = new List<PlanetRecord>();
            travel.world.CountPlanets(travel.starId, planets);
            for (int i = 0; i < planets.Count; i++)
            {
                var mark = new OrbitMark();
                mark.planet = planets[i];
                Color color = ColorOfFaction(planets[i].factionId);
                mark.dot = Dot(color, 14f);
                map.Add(mark.dot);
                orbits.Add(mark);
            }

            var ship = Dot(new Color(0.92f, 0.95f, 0.98f, 1f), 10f);
            ship.name = "ship-mark";
            ship.pickingMode = PickingMode.Ignore;
            map.Add(ship);
            MoveOrbits();
        }

        void MoveOrbits()
        {
            if (!systemMap || map == null || travel == null || !travel.Ready)
                return;
            float maxRadius = 1f;
            for (int i = 0; i < orbits.Count; i++)
            {
                if (orbits[i].planet != null)
                    maxRadius = Mathf.Max(maxRadius, orbits[i].planet.orbitRadius);
            }

            for (int i = 0; i < orbits.Count; i++)
            {
                OrbitMark mark = orbits[i];
                Vector2 offset = mark.stellar ? travel.StellarOffset() : travel.PlanetOffset(mark.planet);
                Place(mark.dot, offset, maxRadius, 7f);
            }

            VisualElement ship = map.Q("ship-mark");
            if (ship != null && travel.phase != TravelPhase.Warping)
                Place(ship, travel.systemKm, maxRadius, 5f);
        }

        static void Place(VisualElement element, Vector2 kilometers, float maxRadius, float pad)
        {
            float nx = 0.5f + kilometers.x / maxRadius * 0.42f;
            float ny = 0.5f + kilometers.y / maxRadius * 0.42f;
            element.style.left = nx * MapSize - pad;
            element.style.top = (1f - ny) * MapSize - pad;
        }

        Vector2 GalaxyPoint(Vector2 lightYears)
        {
            float radius = 90f;
            if (travel.world.rules != null)
                radius = Mathf.Max(10f, travel.world.rules.galaxyRadius);
            float nx = 0.5f + lightYears.x / (radius * 2.2f);
            float ny = 0.5f + lightYears.y / (radius * 2.2f);
            return new Vector2(Mathf.Clamp(nx, 0.04f, 0.96f) * MapSize, (1f - Mathf.Clamp(ny, 0.04f, 0.96f)) * MapSize);
        }

        Color ColorOf(StarRecord star)
        {
            if (star.id == travel.starId)
                return new Color(0.95f, 0.95f, 0.9f, 1f);
            if (!star.unlocked)
                return new Color(0.32f, 0.34f, 0.38f, 1f);
            return ColorOfFaction(star.factionId);
        }

        Color ColorOfFaction(string factionId)
        {
            FactionDef faction = travel.FactionOf(factionId);
            if (faction == null)
                return new Color(0.55f, 0.5f, 0.42f, 1f);
            return faction.color;
        }

        string OwnerName(string factionId)
        {
            if (string.IsNullOrEmpty(factionId))
                return "无主";
            FactionDef faction = travel.FactionOf(factionId);
            return faction != null ? faction.displayName : factionId;
        }

        string HoldLine()
        {
            if (travel.hold.Count == 0)
                return "货舱是空的。";
            var text = "货舱";
            for (int i = 0; i < travel.hold.Count; i++)
            {
                CargoLine line = travel.hold[i];
                if (line == null || line.good == null || line.units <= 0)
                    continue;
                text += "  " + line.good.displayName + line.units;
            }

            return text;
        }

        string ModeLabel()
        {
            if (travel.inCombat || travel.mode == TravelMode.Conventional && travel.phase != TravelPhase.Warping && travel.phase != TravelPhase.Pulsing)
                return "常规引擎";
            if (travel.mode == TravelMode.Warp || travel.phase == TravelPhase.Warping)
                return "曲速";
            if (travel.mode == TravelMode.PulseCruise || travel.phase == TravelPhase.Pulsing)
                return "脉冲巡航";
            return "常规引擎";
        }

        void ToggleMap()
        {
            systemMap = !systemMap;
            builtRevision = -1;
            Rebuild();
        }

        void ToggleFog()
        {
            showAll = !showAll;
            builtRevision = -1;
            Rebuild();
        }

        void RefreshProgress()
        {
            if (progressLabel == null || travel == null)
                return;
            if (travel.phase == TravelPhase.Warping || travel.phase == TravelPhase.Pulsing)
            {
                int percent = Mathf.RoundToInt(travel.Progress * 100f);
                progressLabel.text = percent + "%  真实航时 " + travel.trueDuration.ToString("0", CultureInfo.InvariantCulture) + " 秒";
            }
            else
                progressLabel.text = "";
        }

        void Finish(bool ok, string error)
        {
            if (!ok)
                Fail(error);
            else
                Done();
        }

        void Done()
        {
            if (travel.phase == TravelPhase.Warping)
                systemMap = false;
            if (onMutated != null)
                onMutated();
            builtRevision = -1;
            Rebuild();
        }

        void Fail(string error)
        {
            travel.status = error;
            if (onStatus != null)
                onStatus(error);
            builtRevision = -1;
            Rebuild();
        }

        static VisualElement Column(float width)
        {
            var column = new VisualElement();
            column.style.width = width;
            column.style.marginRight = 12;
            column.style.flexShrink = 0;
            return column;
        }

        static Label Head(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        static Label Hint(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            return label;
        }

        static VisualElement Dot(Color color, float size)
        {
            var dot = new VisualElement();
            dot.style.position = Position.Absolute;
            dot.style.width = size;
            dot.style.height = size;
            dot.style.borderTopLeftRadius = size * 0.5f;
            dot.style.borderTopRightRadius = size * 0.5f;
            dot.style.borderBottomLeftRadius = size * 0.5f;
            dot.style.borderBottomRightRadius = size * 0.5f;
            dot.style.backgroundColor = color;
            return dot;
        }

        static Button ActionButton(string text, Action click)
        {
            var button = new Button(click) { text = text };
            button.style.marginBottom = 4;
            button.style.marginRight = 4;
            button.style.whiteSpace = WhiteSpace.Normal;
            return button;
        }
    }
}
