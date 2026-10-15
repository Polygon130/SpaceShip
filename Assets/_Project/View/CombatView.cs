using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip
{
    public static class CombatView
    {
        const float PixelsPerMeter = 5f;
        const float CellPixels = CombatSim.CellMeters * PixelsPerMeter;
        const float MinZoom = 0.05f;
        static readonly float[] ZoomStops = { 0.05f, 0.125f, 0.25f, 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.25f, 2.5f, 2.75f, 3f, 3.5f, 4f, 4.5f, 5f, 5.5f, 6f, 6.5f, 7f, 7.5f, 8f, 8.5f, 9f, 9.5f, 10f, 10.5f, 11f, 11.5f, 12f, 12.5f, 13f, 13.5f, 14f, 14.5f, 15f, 15.5f, 16f, 18f, 20f, 24f, 28f, 32f };
        const float MaxZoom = 16f;
        const float MapWheelMax = 5f;
        const float DetailMaxZoom = 32f;
        const float IconSize = 22f;
        const float IconSwitch = 40f;
        static float detailFloor = 0.1f;

        sealed class ShipWidget
        {
            public VisualElement root;
            public VisualElement marker;
            public VisualElement[] cells;
            public VisualElement[] hurt;
            public bool player;
        }

        static VisualElement root;
        static VisualElement field;
        static VisualElement shotLayer;
        static Label hud;
        static Label autoHint;
        static VisualElement fireDock;
        static readonly List<VisualElement> shotMarks = new List<VisualElement>();
        sealed class PlanetMark
        {
            public VisualElement ring;
            public VisualElement body;
            public int index;
        }

        static ShipWidget playerWidget;
        static ShipWidget enemyWidget;
        static readonly List<PlanetMark> planetMarks = new List<PlanetMark>();
        static VisualElement orbit;
        static VisualElement enemyOrbit;
        static readonly List<Vector2> orbitPoints = new List<Vector2>();
        static readonly List<Vector2> smoothPoints = new List<Vector2>();
        static readonly List<Vector2> enemyOrbitPoints = new List<Vector2>();
        static readonly List<Vector2> enemySmoothPoints = new List<Vector2>();
        static readonly List<Vector2> playerTrail = new List<Vector2>();
        static readonly List<Vector2> enemyTrail = new List<Vector2>();
        const float TrailSpacing = 4f;
        const int TrailCap = 4000;
        static float playerLineHold;
        static float enemyLineHold;
        static float zoom = 1f;
        static float viewScale = PixelsPerMeter;
        static Vector2 cameraFocus;
        static float cameraRoll;
        static bool mapMode;
        static float helmZoom = 1f;
        static bool enemySelected;
        enum TrackWho
        {
            None,
            Player,
            Enemy
        }

        const int TrackGrades = 4;
        static readonly float[] TrackRate = { 0f, 0.7f, 2.2f, 6f };
        static TrackWho trackWho = TrackWho.Player;
        static int trackGrade = TrackGrades;
        static float lastEnemyClick = -1f;
        static int lastEnemyClickFrame = -1;
        static bool followPlayer = true;
        static bool combatState;
        static CombatShip combatPivotShip;
        static bool zoomAnchorLive;
        static Vector2 zoomAnchorOffset;
        static bool lockHeading;
        static bool detailMode;
        static Vector2 detailPan;
        static float mapZoom = 1f;
        static bool panCandidate;
        static bool panDrag;
        static Vector2 panOrigin;
        static Vector2 panLast;
        static Label zoomLabel;
        static VisualElement enemyHit;
        static Rect enemyBox;
        static bool enemyBoxLive;
        static Vector2 fieldPointerLocal;
        static bool pointerOverField;
        static int enemyMenuFrame = -1;
        static int menuClosedFrame = -1;
        static CombatSim bound;
        static VisualElement tagLayer;
        static Label fightTag;
        static VisualElement orderMenu;
        static VisualElement rangeRing;
        static Label rangeReadout;
        static bool rangePicking;
        static ShipOrder rangeOrder;
        static float rangeMeters = 40f;
        static int rangeArmFrame = -1;
        static int rangeHandledFrame = -1;
        static int rangeCancelFrame = -1;
        static Vector2 rangeAnchorLocal;
        static bool rangeMoved;
        static Vector2 menuFieldAnchor;
        static readonly List<RoomTag> roomTags = new List<RoomTag>();

        sealed class RoomTag
        {
            public Label label;
            public CombatShip ship;
            public int index;
        }

        public static void Open(VisualElement parent, CombatSim sim, Action onExit)
        {
            Open(parent, sim, onExit, "返回拼船");
        }

        public static void Open(VisualElement parent, CombatSim sim, Action onExit, string exitLabel)
        {
            Close();
            root = new VisualElement { name = "combat-root" };
            root.AddToClassList("combat-root");
            parent.Add(root);

            var bar = new VisualElement();
            bar.AddToClassList("combat-bar");
            var back = new Button(onExit) { text = string.IsNullOrEmpty(exitLabel) ? "返回拼船" : exitLabel };
            back.AddToClassList("fight-button");
            bar.Add(back);
            var title = new Label("战斗") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("header");
            bar.Add(title);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1f;
            bar.Add(spacer);
            var zoomOut = new Button(() => ZoomStep(-1)) { text = "-" };
            zoomOut.AddToClassList("zoom-button");
            bar.Add(zoomOut);
            zoomLabel = new Label(ZoomText()) { pickingMode = PickingMode.Ignore };
            zoomLabel.AddToClassList("zoom-label");
            bar.Add(zoomLabel);
            var zoomIn = new Button(() => ZoomStep(1)) { text = "+" };
            zoomIn.AddToClassList("zoom-button");
            bar.Add(zoomIn);
            root.Add(bar);

            field = new VisualElement { name = "combat-field" };
            field.AddToClassList("combat-field");
            field.RegisterCallback<PointerMoveEvent>(evt =>
            {
                fieldPointerLocal = field.WorldToLocal(evt.position);
                pointerOverField = true;
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<PointerLeaveEvent>(_ => pointerOverField = false);
            field.RegisterCallback<WheelEvent>(evt =>
            {
                if (!detailMode)
                    DismissOrderMenu();
                Vector2 local = field.WorldToLocal(evt.mousePosition);
                fieldPointerLocal = local;
                pointerOverField = true;
                ZoomStep(evt.delta.y < 0f ? 1 : -1, (mapMode || !followPlayer) && !detailMode, detailMode ? 0f : MapWheelMax, true, local);
                evt.StopPropagation();
            });
            root.RegisterCallback<ContextualMenuPopulateEvent>(evt => evt.StopImmediatePropagation(), TrickleDown.TrickleDown);
            root.Add(field);

            if (sim.planets != null)
            {
                for (int i = 0; i < sim.planets.Count; i++)
                {
                    PlanetPlacement placement = sim.planets[i];
                    if (placement == null || placement.well == null)
                        continue;
                    var mark = new PlanetMark { index = i };
                    mark.ring = new VisualElement { pickingMode = PickingMode.Ignore };
                    mark.ring.AddToClassList("planet-ring");
                    mark.body = new VisualElement { pickingMode = PickingMode.Ignore };
                    mark.body.AddToClassList("planet-body");
                    mark.body.style.backgroundColor = placement.well.bodyColor;
                    field.Add(mark.ring);
                    field.Add(mark.body);
                    planetMarks.Add(mark);
                }
            }

            enemyOrbit = new VisualElement { name = "enemy-orbit-line", pickingMode = PickingMode.Ignore };
            enemyOrbit.AddToClassList("orbit-line");
            enemyOrbit.generateVisualContent += DrawEnemyOrbit;
            field.Add(enemyOrbit);
            orbit = new VisualElement { name = "orbit-line", pickingMode = PickingMode.Ignore };
            orbit.AddToClassList("orbit-line");
            orbit.generateVisualContent += DrawOrbit;
            field.Add(orbit);

            shotLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            shotLayer.AddToClassList("shot-layer");
            field.Add(shotLayer);
            rangeRing = new VisualElement { name = "range-ring", pickingMode = PickingMode.Ignore };
            rangeRing.AddToClassList("range-ring");
            rangeRing.style.display = DisplayStyle.None;
            field.Add(rangeRing);
            rangeReadout = new Label { pickingMode = PickingMode.Ignore };
            rangeReadout.AddToClassList("range-readout");
            rangeReadout.style.display = DisplayStyle.None;
            field.Add(rangeReadout);
            playerWidget = BuildShip(sim.player);
            enemyWidget = BuildShip(sim.enemy);
            enemyHit = BuildEnemyHit();
            tagLayer = new VisualElement { name = "room-tags", pickingMode = PickingMode.Ignore };
            tagLayer.AddToClassList("shot-layer");
            field.Add(tagLayer);
            fightTag = new Label("战斗") { pickingMode = PickingMode.Ignore };
            fightTag.AddToClassList("fight-tag");
            fightTag.style.position = Position.Absolute;
            fightTag.style.display = DisplayStyle.None;
            tagLayer.Add(fightTag);
            BuildTags(sim.player);
            BuildTags(sim.enemy);
            bound = sim;
            mapMode = false;
            detailMode = false;
            lockHeading = false;
            cameraRoll = 0f;
            if (sim.player != null && !sim.player.destroyed)
            {
                cameraFocus = sim.player.position;
                followPlayer = true;
                trackWho = TrackWho.Player;
            }

            hud = new Label { pickingMode = PickingMode.Ignore };
            hud.AddToClassList("combat-hud");
            root.Add(hud);
            autoHint = new Label { pickingMode = PickingMode.Ignore };
            autoHint.AddToClassList("auto-hint");
            autoHint.style.display = DisplayStyle.None;
            field.Add(autoHint);
            fireDock = BuildFireDock();
            field.Add(fireDock);
            UiFonts.Apply(root);
            field.schedule.Execute(() => Sync(sim)).StartingIn(16);
        }

        public static void Close()
        {
            if (root != null && root.parent != null)
                root.RemoveFromHierarchy();
            root = null;
            field = null;
            shotLayer = null;
            hud = null;
            autoHint = null;
            fireDock = null;
            playerWidget = null;
            enemyWidget = null;
            enemyHit = null;
            enemyBoxLive = false;
            pointerOverField = false;
            orbit = null;
            enemyOrbit = null;
            orbitPoints.Clear();
            smoothPoints.Clear();
            enemyOrbitPoints.Clear();
            enemySmoothPoints.Clear();
            playerTrail.Clear();
            enemyTrail.Clear();
            playerLineHold = 0f;
            enemyLineHold = 0f;
            planetMarks.Clear();
            zoom = 1f;
            viewScale = PixelsPerMeter;
            cameraFocus = Vector2.zero;
            cameraRoll = 0f;
            mapMode = false;
            helmZoom = 1f;
            followPlayer = true;
            combatState = false;
            combatPivotShip = null;
            zoomAnchorLive = false;
            trackWho = TrackWho.Player;
            trackGrade = TrackGrades;
            lastEnemyClick = -1f;
            lockHeading = false;
            detailMode = false;
            detailPan = Vector2.zero;
            mapZoom = 1f;
            enemySelected = false;
            panCandidate = false;
            panDrag = false;
            zoomLabel = null;
            shotMarks.Clear();
            roomTags.Clear();
            tagLayer = null;
            fightTag = null;
            orderMenu = null;
            rangeRing = null;
            rangeReadout = null;
            rangePicking = false;
            bound = null;
        }

        public static void Sync(CombatSim sim)
        {
            if (root == null || field == null || sim == null || field.panel == null)
                return;
            combatState = sim.combat;
            if (!combatState || !mapMode)
                zoomAnchorLive = false;
            CombatShip helmShip = sim.player != null && !sim.player.destroyed ? sim.player : null;
            CombatShip detailShip = null;
            if (mapMode)
            {
                lockHeading = false;
                cameraRoll = 0f;
                ApplyTrack(sim);
            }
            else if (detailMode && helmShip != null)
            {
                lockHeading = true;
                cameraRoll = HelmRoll(helmShip);
                detailShip = helmShip;
                cameraFocus = helmShip.position;
            }
            else
            {
                lockHeading = false;
                cameraRoll = 0f;
                if (followPlayer && helmShip != null)
                    cameraFocus = helmShip.position;
            }
            viewScale = PixelsPerMeter * zoom;
            if (detailShip != null)
                ApplyDetailPan(detailShip);
            if (zoomLabel != null)
                zoomLabel.text = ZoomText();
            NoteTrail(sim.player, playerTrail);
            NoteTrail(sim.enemy, enemyTrail);
            ShowTrail(playerTrail, sim.player, orbitPoints);
            ShowTrail(enemyTrail, sim.enemy, enemyOrbitPoints);
            RefreshCourse(orbitPoints, smoothPoints);
            RefreshCourse(enemyOrbitPoints, enemySmoothPoints);
            if (orbit != null)
                orbit.MarkDirtyRepaint();
            if (enemyOrbit != null)
                enemyOrbit.MarkDirtyRepaint();
            PlacePlanet(sim);
            PlaceShip(playerWidget, sim.player);
            PlaceShip(enemyWidget, sim.enemy);
            PlaceFightTag(sim);
            PlaceEnemyHit();
            PlaceTags();
            PlaceShots(sim);
            if (tagLayer != null)
                tagLayer.BringToFront();
            if (shotLayer != null)
                shotLayer.BringToFront();
            if (enemyHit != null)
                enemyHit.BringToFront();
            PlaceRangeRing();
            if (hud != null)
                hud.text = Format(sim);
            PlaceAutoHint(sim);
        }

        static string OrderStatus(CombatSim sim)
        {
            if (sim.order == ShipOrder.Dock)
                return "停靠";
            return CombatSim.OrderLabel(sim.order) + " " + CombatSim.RangeLabel(sim.orderRange);
        }

        static void PlaceAutoHint(CombatSim sim)
        {
            if (autoHint == null)
                return;
            bool auto = sim != null && sim.order != ShipOrder.Manual && string.IsNullOrEmpty(sim.outcome);
            autoHint.style.display = auto ? DisplayStyle.Flex : DisplayStyle.None;
            if (!auto)
                return;
            autoHint.text = "自动驾驶  " + OrderStatus(sim);
            autoHint.BringToFront();
            RefreshFireDock();
            if (fireDock != null)
                fireDock.BringToFront();
        }

        static ShipWidget BuildShip(CombatShip ship)
        {
            var widget = new ShipWidget();
            widget.player = ship != null && ship.player;
            widget.root = new VisualElement { pickingMode = PickingMode.Ignore };
            widget.root.AddToClassList("combat-ship");
            field.Add(widget.root);
            widget.marker = new VisualElement { pickingMode = PickingMode.Ignore, name = "ship-icon" };
            widget.marker.style.position = Position.Absolute;
            widget.marker.style.width = IconSize;
            widget.marker.style.height = IconSize;
            widget.marker.style.display = DisplayStyle.None;
            widget.marker.generateVisualContent += ctx => DrawTriangle(ctx, widget);
            field.Add(widget.marker);
            if (ship == null || ship.build == null || ship.build.hull == null)
                return widget;
            HullDef hull = ship.build.hull;
            widget.root.style.width = hull.width * CellPixels;
            widget.root.style.height = hull.height * CellPixels;
            widget.cells = new VisualElement[hull.width * hull.height];
            widget.hurt = new VisualElement[hull.width * hull.height];
            ShipReport report = ShipEvaluator.Evaluate(ship.build);
            var roomAt = CellMap.Build(ship.build);
            for (int y = hull.height - 1; y >= 0; y--)
            {
                var row = new VisualElement();
                row.AddToClassList("combat-row");
                for (int x = 0; x < hull.width; x++)
                {
                    var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                    cell.AddToClassList("combat-cell");
                    cell.style.position = Position.Relative;
                    cell.style.width = CellPixels - 1f;
                    cell.style.height = CellPixels - 1f;
                    var plate = new VisualElement { pickingMode = PickingMode.Ignore, name = "hurt" };
                    plate.style.position = Position.Absolute;
                    plate.style.left = 0f;
                    plate.style.top = 0f;
                    plate.style.right = 0f;
                    plate.style.bottom = 0f;
                    plate.style.display = DisplayStyle.None;
                    cell.Add(plate);
                    int key = y * hull.width + x;
                    if (!hull.IsOpen(x, y))
                        cell.AddToClassList("cell-blocked");
                    else if (roomAt.floor.TryGetValue(key, out int index) && index < ship.build.rooms.Count)
                        cell.style.backgroundColor = ship.build.rooms[index].room.swatch;
                    else if (hull.defaultCabin != null)
                        cell.style.backgroundColor = hull.defaultCabin.swatch;
                    else
                        cell.AddToClassList("cell-empty");
                    widget.cells[key] = cell;
                    widget.hurt[key] = plate;
                    row.Add(cell);
                }

                widget.root.Add(row);
            }

            var nose = new VisualElement { pickingMode = PickingMode.Ignore };
            nose.AddToClassList("ship-nose");
            widget.root.Add(nose);
            PaintFacing(widget.root, ship, report);
            TintShip(widget, ship, report);
            return widget;
        }

        static void PaintFacing(VisualElement shipRoot, CombatShip ship, ShipReport report)
        {
            HullDef hull = ship.build.hull;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                if (i >= report.valid.Count || !report.valid[i])
                    continue;
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                int turns = ((placement.rotation % 4) + 4) % 4;
                float left = placement.x * CellPixels;
                float top = (hull.height - placement.y - placement.SpanY) * CellPixels;
                float width = placement.SpanX * CellPixels - 1f;
                float height = placement.SpanY * CellPixels - 1f;
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.style.position = Position.Absolute;
                bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.95f);
                if (turns == 0 || turns == 2)
                {
                    bar.style.left = left;
                    bar.style.width = width;
                    bar.style.height = 2f;
                    bar.style.top = turns == 0 ? top : top + height - 2f;
                }
                else
                {
                    bar.style.top = top;
                    bar.style.width = 2f;
                    bar.style.height = height;
                    bar.style.left = turns == 1 ? left + width - 2f : left;
                }

                shipRoot.Add(bar);
            }
        }

        static void BuildTags(CombatShip ship)
        {
            if (tagLayer == null || ship == null || ship.build == null)
                return;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("room-label");
                label.style.position = Position.Absolute;
                label.style.display = DisplayStyle.None;
                tagLayer.Add(label);
                roomTags.Add(new RoomTag { label = label, ship = ship, index = i });
            }
        }

        static void PlaceFightTag(CombatSim sim)
        {
            if (fightTag == null)
                return;
            CombatShip ship = sim != null ? sim.enemy : null;
            bool show = ship != null && !ship.destroyed && sim.enemyMarked;
            fightTag.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show)
                return;
            Vector2 local = Project(ship.position);
            float half = IconSize * 0.5f;
            if (!ShowIcon(ship) && ship.build != null && ship.build.hull != null)
                half = ship.build.hull.height * CellPixels * zoom * 0.5f;
            fightTag.style.left = local.x - 22f;
            fightTag.style.top = local.y - half - 28f;
            fightTag.BringToFront();
        }

        static void PlaceTags()
        {
            const float text = 20f;
            for (int i = 0; i < roomTags.Count; i++)
            {
                RoomTag tag = roomTags[i];
                CombatShip ship = tag.ship;
                if (tag.label == null || ship == null || ship.build == null || ship.destroyed || zoom < 2f)
                {
                    if (tag.label != null)
                        tag.label.style.display = DisplayStyle.None;
                    continue;
                }

                if (tag.index < 0 || tag.index >= ship.build.rooms.Count)
                    continue;
                RoomPlacement placement = ship.build.rooms[tag.index];
                if (placement == null || placement.room == null || string.IsNullOrEmpty(placement.room.displayName))
                {
                    tag.label.style.display = DisplayStyle.None;
                    continue;
                }

                float width = placement.SpanX * CellPixels * zoom;
                float height = placement.SpanY * CellPixels * zoom;
                if (height < text)
                {
                    tag.label.style.display = DisplayStyle.None;
                    continue;
                }

                string name = placement.room.displayName;
                tag.label.text = width >= name.Length * text ? name : name.Substring(0, 1);
                Vector2 local = Project(CombatSim.RoomCenter(ship, placement));
                tag.label.style.display = DisplayStyle.Flex;
                tag.label.style.left = local.x - width * 0.5f;
                tag.label.style.top = local.y - height * 0.5f;
                tag.label.style.width = width;
                tag.label.style.height = height;
            }
        }

        static void PlacePlanet(CombatSim sim)
        {
            for (int i = 0; i < planetMarks.Count; i++)
            {
                PlanetMark mark = planetMarks[i];
                if (mark.index < 0 || mark.index >= sim.planets.Count)
                    continue;
                PlanetPlacement placement = sim.planets[mark.index];
                if (placement == null || placement.well == null)
                    continue;
                float bodySize = placement.well.floorRadius * 2f * viewScale;
                PlaceCircle(mark.body, placement.position, bodySize);
                float ringSize = placement.well.influenceRadius * 2f * viewScale;
                PlaceCircle(mark.ring, placement.position, ringSize);
            }
        }

        static void PlaceCircle(VisualElement element, Vector2 world, float size)
        {
            Vector2 local = Project(world);
            element.style.width = size;
            element.style.height = size;
            element.style.left = local.x - size * 0.5f;
            element.style.top = local.y - size * 0.5f;
            element.style.borderTopLeftRadius = size * 0.5f;
            element.style.borderTopRightRadius = size * 0.5f;
            element.style.borderBottomLeftRadius = size * 0.5f;
            element.style.borderBottomRightRadius = size * 0.5f;
        }

        static void PlaceShip(ShipWidget widget, CombatShip ship)
        {
            if (widget == null || widget.root == null || ship == null)
                return;
            Vector2 local = Project(ship.position);
            float width = widget.root.resolvedStyle.width;
            float height = widget.root.resolvedStyle.height;
            if (width <= 1f && ship.build != null && ship.build.hull != null)
            {
                width = ship.build.hull.width * CellPixels;
                height = ship.build.hull.height * CellPixels;
            }

            bool icon = ShowIcon(ship);
            if (widget.marker != null)
            {
                widget.marker.style.display = icon ? DisplayStyle.Flex : DisplayStyle.None;
                if (icon)
                {
                    widget.marker.style.left = local.x - IconSize * 0.5f;
                    widget.marker.style.top = local.y - IconSize * 0.5f;
                    widget.marker.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0f);
                    widget.marker.style.scale = new Scale(Vector3.one);
                    widget.marker.style.rotate = new Rotate(Angle.Degrees(ship.heading + cameraRoll));
                    widget.marker.style.opacity = ship.destroyed ? 0.35f : 1f;
                    widget.marker.BringToFront();
                }
            }

            widget.root.style.display = icon ? DisplayStyle.None : DisplayStyle.Flex;
            if (icon)
                return;
            widget.root.style.left = local.x - width * 0.5f;
            widget.root.style.top = local.y - height * 0.5f;
            widget.root.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(50), 0f);
            float graphic = viewScale / PixelsPerMeter;
            widget.root.style.scale = new Scale(new Vector3(graphic, graphic, 1f));
            widget.root.style.rotate = new Rotate(Angle.Degrees(ship.heading + cameraRoll));
            widget.root.style.opacity = ship.destroyed ? 0.35f : 1f;
            TintShip(widget, ship, ShipEvaluator.Evaluate(ship.build));
        }

        static bool ShowIcon(CombatShip ship)
        {
            if (detailMode || ship == null || ship.build == null || ship.build.hull == null)
                return false;
            HullDef hull = ship.build.hull;
            float longSide = Mathf.Max(hull.width, hull.height) * CellPixels * zoom;
            return longSide < IconSwitch;
        }

        static void DrawTriangle(MeshGenerationContext ctx, ShipWidget widget)
        {
            var painter = ctx.painter2D;
            painter.fillColor = widget.player ? new Color(0.86f, 0.93f, 1f, 1f) : new Color(0.95f, 0.34f, 0.28f, 1f);
            const float pad = 1f;
            float cx = IconSize * 0.5f;
            float cy = IconSize * 0.5f;
            float height = (cx - pad) * 1.5f;
            float half = height / 1.7320508f;
            Vector2 bow = new Vector2(cx, cy - height * (2f / 3f));
            float baseY = cy + height / 3f;
            painter.BeginPath();
            painter.MoveTo(bow);
            painter.LineTo(new Vector2(cx + half, baseY));
            painter.LineTo(new Vector2(cx - half, baseY));
            painter.ClosePath();
            painter.Fill();
        }

        static void TintShip(ShipWidget widget, CombatShip ship, ShipReport report)
        {
            if (widget.cells == null || ship.build == null || ship.build.hull == null)
                return;
            HullDef hull = ship.build.hull;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                if (i >= report.valid.Count || !report.valid[i])
                    continue;
                Color color = placement.room.swatch;
                bool hot = CombatSim.Overheated(ship, i);
                if (hot)
                    color = Color.Lerp(color, new Color(0.85f, 0.2f, 0.16f), 0.72f);
                else if (placement.room.heatCapacity > 0.01f && ship.heat[i] > 0f)
                    color = Color.Lerp(color, new Color(0.85f, 0.45f, 0.2f), Mathf.Clamp01(ship.heat[i] / placement.room.heatCapacity) * 0.55f);
                for (int y = 0; y < placement.SpanY; y++)
                {
                    for (int x = 0; x < placement.SpanX; x++)
                    {
                        int key = (placement.y + y) * hull.width + (placement.x + x);
                        if (key < 0 || key >= widget.cells.Length || widget.cells[key] == null)
                            continue;
                        widget.cells[key].style.backgroundColor = color;
                    }
                }
            }

            for (int y = 0; y < hull.height; y++)
            {
                for (int x = 0; x < hull.width; x++)
                {
                    if (!hull.IsOpen(x, y))
                        continue;
                    PaintHurt(widget, ship, y * hull.width + x);
                }
            }
        }

        static void PaintHurt(ShipWidget widget, CombatShip ship, int key)
        {
            if (widget.hurt == null || key < 0 || key >= widget.hurt.Length || widget.hurt[key] == null)
                return;
            VisualElement plate = widget.hurt[key];
            float cap = ship.cellStructureMax;
            float left = ship.cellStructure != null && key < ship.cellStructure.Length ? ship.cellStructure[key] : cap;
            float loss = cap > 0.0001f ? 1f - left / cap : 0f;
            if (loss <= 0.02f)
            {
                plate.style.display = DisplayStyle.None;
                return;
            }

            plate.style.display = DisplayStyle.Flex;
            plate.style.backgroundColor = new Color(0.86f, 0.08f, 0.08f, Mathf.Clamp01(loss));
        }

        static void PlaceShots(CombatSim sim)
        {
            while (shotMarks.Count < sim.shots.Count)
            {
                var mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.AddToClassList("shot-mark");
                shotLayer.Add(mark);
                shotMarks.Add(mark);
            }

            for (int i = 0; i < shotMarks.Count; i++)
            {
                if (i >= sim.shots.Count)
                {
                    shotMarks[i].style.display = DisplayStyle.None;
                    continue;
                }

                Vector2 local = Project(sim.shots[i].position);
                bool missile = sim.shots[i].weapon != null && sim.shots[i].weapon.homing;
                float mark = missile ? 8f : 4f;
                shotMarks[i].style.display = DisplayStyle.Flex;
                shotMarks[i].style.width = mark;
                shotMarks[i].style.height = mark;
                shotMarks[i].style.backgroundColor = missile
                    ? new Color(1f, 0.55f, 0.22f)
                    : new Color(1f, 0.89f, 0.67f);
                shotMarks[i].style.left = local.x - mark * 0.5f;
                shotMarks[i].style.top = local.y - mark * 0.5f;
            }
        }

        static Vector2 ViewHalf()
        {
            float width = field.resolvedStyle.width;
            float height = field.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width < 8f || height < 8f || viewScale < 0.01f)
                return new Vector2(400f, 300f);
            const float pad = 64f;
            Vector2 half = new Vector2((width * 0.5f + pad) / viewScale, (height * 0.5f + pad) / viewScale);
            if (Mathf.Abs(cameraRoll) > 0.5f)
            {
                float diag = Mathf.Sqrt(half.x * half.x + half.y * half.y);
                return new Vector2(diag, diag);
            }

            return half;
        }

        static void ZoomStep(int direction, bool aroundPointer = false, float ceilingLimit = 0f, bool hasPointer = false, Vector2 pointerLocal = default)
        {
            if (bound != null)
                combatState = bound.combat;
            float floor = detailMode ? detailFloor : MinZoom;
            float ceiling = ceilingLimit > 0.001f ? ceilingLimit : (detailMode ? DetailMaxZoom : MaxZoom);
            float next = zoom;
            if (direction > 0)
            {
                for (int i = 0; i < ZoomStops.Length; i++)
                {
                    if (ZoomStops[i] > zoom + 0.001f && ZoomStops[i] <= ceiling + 0.001f)
                    {
                        next = ZoomStops[i];
                        break;
                    }
                }
            }
            else if (direction < 0)
            {
                for (int i = ZoomStops.Length - 1; i >= 0; i--)
                {
                    if (ZoomStops[i] < zoom - 0.001f && ZoomStops[i] + 0.001f >= floor)
                    {
                        next = ZoomStops[i];
                        break;
                    }
                }
            }

            if (mapMode && direction < 0 && next <= MinZoom + 0.001f && TryZoomPointer(hasPointer, pointerLocal, out Vector2 minAim))
            {
                CombatShip wide = NearestShip(minAim);
                if (wide != null)
                {
                    if (Mathf.Abs(next - zoom) >= 0.001f)
                    {
                        zoom = next;
                        viewScale = PixelsPerMeter * zoom;
                    }

                    BeginTrack(wide == bound.enemy ? TrackWho.Enemy : TrackWho.Player);
                    cameraFocus = wide.position;
                    zoomAnchorOffset = Vector2.zero;
                    zoomAnchorLive = false;
                    combatPivotShip = wide;
                    if (zoomLabel != null)
                        zoomLabel.text = ZoomText();
                    return;
                }
            }

            if (Mathf.Abs(next - zoom) < 0.001f)
                return;
            if (combatState && mapMode && TryZoomPointer(hasPointer, pointerLocal, out Vector2 aim))
            {
                CombatShip ship = NearestShip(aim);
                if (ship != null)
                {
                    Vector2 screen = Project(ship.position);
                    Vector2 shipAt = ship.position;
                    zoom = next;
                    viewScale = PixelsPerMeter * zoom;
                    cameraFocus = FocusKeeping(shipAt, screen);
                    combatPivotShip = ship;
                    zoomAnchorOffset = cameraFocus - shipAt;
                    zoomAnchorLive = true;
                    if (zoomLabel != null)
                        zoomLabel.text = ZoomText();
                    return;
                }
            }

            bool tracking = mapMode && trackWho != TrackWho.None && trackGrade > 0;
            Vector2 pointer = Vector2.zero;
            Vector2 world = Vector2.zero;
            bool anchor = aroundPointer && !tracking && TryMouseFieldLocal(out pointer);
            if (anchor)
                world = Unproject(pointer);
            zoom = next;
            viewScale = PixelsPerMeter * zoom;
            if (anchor)
            {
                followPlayer = false;
                cameraFocus = FocusKeeping(world, pointer);
            }

            if (zoomLabel != null)
                zoomLabel.text = ZoomText();
        }

        static bool TryZoomPointer(bool hasPointer, Vector2 pointerLocal, out Vector2 pointer)
        {
            if (hasPointer)
            {
                pointer = pointerLocal;
                return true;
            }

            if (TryMouseFieldLocal(out pointer))
                return true;
            if (pointerOverField)
            {
                pointer = fieldPointerLocal;
                return true;
            }

            if (field == null)
            {
                pointer = Vector2.zero;
                return false;
            }

            pointer = field.WorldToLocal(field.worldBound.center);
            return true;
        }

        static CombatShip NearestShip(Vector2 pointer)
        {
            CombatShip best = null;
            float bestSq = float.PositiveInfinity;
            if (bound == null)
                return null;
            Consider(bound.player);
            Consider(bound.enemy);
            return best;

            void Consider(CombatShip ship)
            {
                if (ship == null || ship.destroyed)
                    return;
                float sq = (Project(ship.position) - pointer).sqrMagnitude;
                if (sq >= bestSq)
                    return;
                bestSq = sq;
                best = ship;
            }
        }

        static string ZoomText()
        {
            float percent = zoom * 100f;
            if (Mathf.Abs(percent - Mathf.Round(percent)) < 0.05f)
                return Mathf.RoundToInt(percent) + "%";
            return percent.ToString("0.0") + "%";
        }

        static Vector2 Project(Vector2 world)
        {
            Rect bound = field.worldBound;
            Vector2 center = field.WorldToLocal(bound.center);
            Vector2 delta = world - cameraFocus;
            Vector2 screen = new Vector2(delta.x, -delta.y) * viewScale;
            if (Mathf.Abs(cameraRoll) > 0.01f)
                screen = RotateClockwise(screen, cameraRoll);
            return center + screen;
        }

        static bool TryMouseFieldLocal(out Vector2 local)
        {
            local = Vector2.zero;
            if (field == null || field.panel == null)
                return false;
            Vector2 screen = Input.mousePosition;
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(field.panel, screen);
            if (!field.worldBound.Contains(panel))
            {
                Vector2 flipped = new Vector2(screen.x, Screen.height - screen.y);
                if (!field.worldBound.Contains(flipped))
                    return false;
                panel = flipped;
            }

            local = field.WorldToLocal(panel);
            return true;
        }

        static Vector2 Unproject(Vector2 local)
        {
            Vector2 center = field.WorldToLocal(field.worldBound.center);
            Vector2 screen = local - center;
            if (Mathf.Abs(cameraRoll) > 0.01f)
                screen = RotateClockwise(screen, -cameraRoll);
            float scale = Mathf.Max(viewScale, 0.0001f);
            return cameraFocus + new Vector2(screen.x / scale, -screen.y / scale);
        }

        static Vector2 FocusKeeping(Vector2 world, Vector2 local)
        {
            Vector2 center = field.WorldToLocal(field.worldBound.center);
            Vector2 screen = local - center;
            if (Mathf.Abs(cameraRoll) > 0.01f)
                screen = RotateClockwise(screen, -cameraRoll);
            float scale = Mathf.Max(viewScale, 0.0001f);
            return world - new Vector2(screen.x / scale, -screen.y / scale);
        }

        static Vector2 RotateClockwise(Vector2 point, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(point.x * cos - point.y * sin, point.x * sin + point.y * cos);
        }

        static void ApplyDetailPan(CombatShip ship)
        {
            ClampDetailPan(ship);
            float scale = Mathf.Max(viewScale, 0.0001f);
            Vector2 screen = detailPan;
            if (Mathf.Abs(cameraRoll) > 0.01f)
                screen = RotateClockwise(detailPan, -cameraRoll);
            cameraFocus = ship.position - new Vector2(screen.x / scale, -screen.y / scale);
        }

        static void ClampDetailPan(CombatShip ship)
        {
            float viewW = field != null ? field.resolvedStyle.width : 0f;
            float viewH = field != null ? field.resolvedStyle.height : 0f;
            if (field != null && (float.IsNaN(viewW) || viewW < 8f))
                viewW = field.worldBound.width;
            if (field != null && (float.IsNaN(viewH) || viewH < 8f))
                viewH = field.worldBound.height;
            if (viewW < 8f)
                viewW = 1280f;
            if (viewH < 8f)
                viewH = 720f;
            ShipScreenSpan(ship, out float boxW, out float boxH);
            float maxX = Mathf.Abs(boxW - viewW) * 0.5f;
            float maxY = Mathf.Abs(boxH - viewH) * 0.5f;
            detailPan.x = Mathf.Clamp(detailPan.x, -maxX, maxX);
            detailPan.y = Mathf.Clamp(detailPan.y, -maxY, maxY);
        }

        static void ShipScreenSpan(CombatShip ship, out float boxW, out float boxH)
        {
            boxW = 100f;
            boxH = 60f;
            HullDef hull = ship != null && ship.build != null ? ship.build.hull : null;
            if (hull == null)
                return;
            float w = hull.width * CellPixels * zoom;
            float h = hull.height * CellPixels * zoom;
            float rad = (ship.heading + cameraRoll) * Mathf.Deg2Rad;
            float c = Mathf.Abs(Mathf.Cos(rad));
            float s = Mathf.Abs(Mathf.Sin(rad));
            boxW = w * c + h * s;
            boxH = w * s + h * c;
        }

        static void KeepLine(bool live, List<Vector2> source, List<Vector2> smooth, ref float hold)
        {
            if (live)
            {
                hold = 0.25f;
                RefreshCourse(source, smooth);
                return;
            }

            hold -= Time.deltaTime;
            if (hold <= 0f)
                smooth.Clear();
        }

        static void RefreshCourse(List<Vector2> source, List<Vector2> smooth)
        {
            smooth.Clear();
            if (source.Count < 2)
                return;
            float spacing = Mathf.Max(1.5f, ViewHalf().magnitude / 28f);
            int origin = 0;
            while (origin < source.Count && float.IsNaN(source[origin].x))
                origin++;
            if (origin >= source.Count)
                return;
            smooth.Add(source[origin]);
            Vector2 lastDir = Vector2.zero;
            bool hasDir = false;
            float since = 0f;
            for (int i = origin + 1; i < source.Count; i++)
            {
                if (float.IsNaN(source[i].x))
                {
                    smooth.Add(source[i]);
                    hasDir = false;
                    since = 0f;
                    continue;
                }

                int previous = i - 1;
                while (previous > origin && float.IsNaN(source[previous].x))
                    previous--;
                Vector2 step = source[i] - source[previous];
                float length = step.magnitude;
                if (length < 0.0001f)
                    continue;
                Vector2 dir = step / length;
                since += length;
                bool corner = hasDir && Vector2.Angle(lastDir, dir) > 14f && since > 0.35f;
                bool last = i == source.Count - 1;
                if (!corner && since < spacing && !last)
                    continue;
                smooth.Add(source[i]);
                lastDir = dir;
                hasDir = true;
                since = 0f;
                if (smooth.Count >= 800 && !last)
                {
                    smooth.Add(source[source.Count - 1]);
                    break;
                }
            }
        }

        static void NoteTrail(CombatShip ship, List<Vector2> trail)
        {
            if (ship == null || ship.destroyed || float.IsNaN(ship.position.x))
                return;
            if (trail.Count == 0)
            {
                trail.Add(ship.position);
                return;
            }

            Vector2 last = trail[trail.Count - 1];
            if ((ship.position - last).sqrMagnitude < TrailSpacing * TrailSpacing)
                return;
            trail.Add(ship.position);
            if (trail.Count > TrailCap)
                trail.RemoveAt(0);
        }

        static void ShowTrail(List<Vector2> trail, CombatShip ship, List<Vector2> into)
        {
            into.Clear();
            if (trail.Count == 0)
                return;
            Vector2 half = ViewHalf();
            Vector2 min = cameraFocus - half;
            Vector2 max = cameraFocus + half;
            for (int i = 1; i < trail.Count; i++)
                AppendTrailSegment(into, trail[i - 1], trail[i], min, max);
            if (ship != null && !ship.destroyed)
                AppendTrailSegment(into, trail[trail.Count - 1], ship.position, min, max);
        }

        static void AppendTrailSegment(List<Vector2> into, Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            if (!ClipTrail(a, b, min, max, out Vector2 c, out Vector2 d))
                return;
            if (into.Count == 0 || float.IsNaN(into[into.Count - 1].x) || (into[into.Count - 1] - c).sqrMagnitude > 1f)
            {
                if (into.Count > 0 && !float.IsNaN(into[into.Count - 1].x))
                    into.Add(new Vector2(float.NaN, 0f));
                into.Add(c);
            }

            if ((c - d).sqrMagnitude > 0.01f)
                into.Add(d);
        }

        static bool ClipTrail(Vector2 a, Vector2 b, Vector2 min, Vector2 max, out Vector2 c, out Vector2 d)
        {
            float t0 = 0f;
            float t1 = 1f;
            Vector2 delta = b - a;
            c = a;
            d = b;
            if (!ClipTrailSide(-delta.x, a.x - min.x, ref t0, ref t1))
                return false;
            if (!ClipTrailSide(delta.x, max.x - a.x, ref t0, ref t1))
                return false;
            if (!ClipTrailSide(-delta.y, a.y - min.y, ref t0, ref t1))
                return false;
            if (!ClipTrailSide(delta.y, max.y - a.y, ref t0, ref t1))
                return false;
            c = a + delta * t0;
            d = a + delta * t1;
            return true;
        }

        static bool ClipTrailSide(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 0.0000001f)
                return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > t1)
                    return false;
                if (t > t0)
                    t0 = t;
            }
            else
            {
                if (t < t0)
                    return false;
                if (t < t1)
                    t1 = t;
            }

            return true;
        }

        static void DrawOrbit(MeshGenerationContext context)
        {
            DrawCourse(context, smoothPoints, new Color(0.78f, 0.92f, 1f));
        }

        static void DrawEnemyOrbit(MeshGenerationContext context)
        {
            DrawCourse(context, enemySmoothPoints, new Color(0.92f, 0.28f, 0.24f));
        }

        static void DrawCourse(MeshGenerationContext context, List<Vector2> points, Color color)
        {
            if (points.Count < 2)
                return;
            Painter2D painter = context.painter2D;
            painter.lineWidth = 2.4f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            int start = 0;
            for (int i = 0; i <= points.Count; i++)
            {
                bool split = i == points.Count || float.IsNaN(points[i].x);
                if (!split)
                    continue;
                int last = i - 1;
                if (last - start >= 1)
                {
                    int span = last - start;
                    int fade = Mathf.Min(10, span);
                    int mid = Mathf.Min(18, span);
                    StrokeSoft(painter, points, color, start, start + fade, 0.34f);
                    StrokeSoft(painter, points, color, start + fade, start + mid, 0.68f);
                    StrokeSoft(painter, points, color, start + mid, last, 0.9f);
                }

                start = i + 1;
            }
        }

        static void StrokeSoft(Painter2D painter, List<Vector2> points, Color color, int from, int to, float alpha)
        {
            if (to <= from)
                return;
            int n = points.Count;
            color.a = alpha;
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(Project(points[from]));
            for (int i = from; i < to; i++)
            {
                Vector2 p0 = Project(points[Mathf.Max(0, i - 1)]);
                Vector2 p1 = Project(points[i]);
                Vector2 p2 = Project(points[i + 1]);
                Vector2 p3 = Project(points[Mathf.Min(n - 1, i + 2)]);
                float span = Vector2.Distance(p1, p2) * 0.45f;
                Vector2 c1 = p1 + Vector2.ClampMagnitude((p2 - p0) / 6f, span);
                Vector2 c2 = p2 + Vector2.ClampMagnitude((p1 - p3) / 6f, span);
                painter.BezierCurveTo(c1, c2, p2);
            }

            painter.Stroke();
        }

        static string EnemyIntent(int mode)
        {
            if (mode > 0)
                return "追击";
            if (mode < 0)
                return "脱离";
            return "占位";
        }

        static string Format(CombatSim sim)
        {
            if (!string.IsNullOrEmpty(sim.outcome))
                return sim.outcome + "\n按「返回拼船」再改布局。";
            string text =
                "速度    " + sim.player.Speed.ToString("0") + " 米/秒    推力 " + sim.player.thrustNow.ToString("0") + "\n" +
                "结构    " + sim.player.structure.ToString("0") + " / " + sim.player.structureMax.ToString("0") +
                "    敌方 " + sim.enemy.structure.ToString("0") + " / " + sim.enemy.structureMax.ToString("0") +
                "  " + EnemyIntent(sim.enemyMode) + "\n";
            text += HeatLine(sim.player);
            string driveText = "空档";
            if (sim.order != ShipOrder.Manual)
                driveText = OrderStatus(sim);
            else if (sim.gear > 0)
                driveText = "推进 " + sim.gear + "/" + sim.gearSpan;
            text += "\n" + driveText + "    " + sim.FireStatus();
            text += "\n点一下切一档。按住 W 加档，按住 S 减到空档。没有倒车，反向要先转 180 度。\n左键点选敌船，方框里右键可下追击 平行 环绕 停靠 脱离。点策略后拉动圆框，再点一下确认，半径就是距离。停靠不拉圆框，碰上后贴到一起再锁住，按质量大的那艘走。大地图里双击敌船，镜头跟着敌船。右边可开关全部、能量、火炮、导弹，这些开关不退出自动驾驶。Q 向左平移，E 向右平移。自动驾驶由电脑使用侧推。按 W S A D Q E 回到手动。";
            if (rangePicking)
                text += "\n" + CombatSim.OrderLabel(rangeOrder) + "  半径 " + CombatSim.RangeLabel(rangeMeters) + "。再点一下确认，右键取消。";
            if (sim.combat && mapMode)
                text += "\n" + sim.CombatNotice() + "。每次缩放都按鼠标离哪艘船更近，以那艘船此刻的位置为中心。";
            else if (sim.combat)
                text += "\n" + sim.CombatNotice() + "。";
            if (mapMode && trackWho == TrackWho.Enemy && trackGrade > 0)
                text += "\n大地图。镜头跟着敌船。按空格回到我方舰船。拖动画面会松开。再按 M 回到开船。";
            else if (mapMode && (followPlayer || (trackWho == TrackWho.Player && trackGrade > 0)))
                text += "\n大地图。按空格镜头回到我方舰船。拖动画面会松开。再按 M 回到开船。";
            else if (mapMode)
                text += "\n大地图。画面已拖开。按空格回到我方舰船。再按 M 回到开船。";
            else if (detailMode && detailPan.sqrMagnitude > 4f)
                text += "\n画面已挪开。船头保持向右。再按空格缩小回来。";
            else if (detailMode)
                text += "\n已放大看舱室。船头保持向右。按住左键只挪画面。再按空格缩小回来。";
            else if (!followPlayer)
                text += "\n画面已拖开，不再跟着船。按住左键继续拖。按空格放大看舱室，再按一次缩小并回到我方舰船。按 M 打开大地图。";
            else
                text += "\n开船。按住左键拖动画面，拖开后不再跟着船。按空格放大看舱室，再按一次缩小回来。按 M 打开大地图。";
            text += "\n滚轮或右上角加减缩放。滚轮放大到 500%。缩到看不清时船改成三角标记，标记不缩放。";
            if (detailMode)
                text += "\n放大后看舱室。缩小不超过 10%，放大可以到 3200%。";
            text += "\n200% 以上看见舱室。\n白线是玩家走过的航迹，红线是敌船走过的航迹。";
            return text;
        }

        public static void PollInput()
        {
            if (field == null || field.panel == null || bound == null)
                return;
            combatState = bound.combat;
            Vector2 panel = MousePanelPoint();
            Vector2 panPoint = RuntimePanelUtils.ScreenToPanel(field.panel, Input.mousePosition);
            bool inField = field.worldBound.Contains(panel);
            Vector2 pointer = inField ? field.WorldToLocal(panel) : Vector2.zero;
            if (rangePicking && (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.E)))
                EndRangePick();
            if (Input.GetMouseButtonDown(0))
            {
                if (PointerOnFireDock())
                {
                    panDrag = false;
                    panCandidate = false;
                }
                else
                {
                bool arming = rangePicking && Time.frameCount == rangeArmFrame;
                bool sizing = rangePicking && !arming && rangeHandledFrame != Time.frameCount && !PointerOnFireDock();
                panDrag = false;
                panOrigin = panPoint;
                panLast = panPoint;
                if (arming || rangeHandledFrame == Time.frameCount)
                    panCandidate = false;
                else if (sizing)
                {
                    TakeRangeClick();
                    panCandidate = false;
                }
                else
                {
                    bool menuHit = PointerOnMenu();
                    bool dockHit = PointerOnFireDock();
                    bool onBox = PointerOnEnemyBox();
                    panCandidate = !menuHit && !dockHit && inField && (!onBox || orderMenu != null);
                    if (onBox && !menuHit && !dockHit)
                    {
                        enemySelected = true;
                        NoteEnemyClick();
                    }
                    else if (!menuHit && !dockHit && inField)
                        enemySelected = false;
                }
                }
            }
            else if (Input.GetMouseButton(0) && panCandidate)
            {
                if (!panDrag)
                {
                    if ((panPoint - panOrigin).sqrMagnitude >= 25f)
                    {
                        panDrag = true;
                        DismissOrderMenu();
                        EndRangePick();
                        if (!detailMode)
                        {
                            followPlayer = false;
                            trackWho = TrackWho.None;
                            trackGrade = 0;
                            zoomAnchorLive = false;
                            combatPivotShip = null;
                            lockHeading = false;
                            cameraRoll = 0f;
                        }
                        panLast = panPoint;
                    }
                }
                else if (viewScale > 0.001f)
                {
                    Vector2 step = panPoint - panLast;
                    panLast = panPoint;
                    if (step.sqrMagnitude < 160000f)
                    {
                        if (detailMode)
                            detailPan += step;
                        else
                        {
                            cameraFocus.x -= step.x / viewScale;
                            cameraFocus.y -= step.y / viewScale;
                        }
                    }
                }
            }
            else if (!Input.GetMouseButton(0))
            {
                panCandidate = false;
                panDrag = false;
            }

            if (Input.GetMouseButtonDown(1) && !PointerOnFireDock())
            {
                if (rangePicking)
                    EndRangePick();
                else if (orderMenu != null && enemyMenuFrame != Time.frameCount)
                    DismissOrderMenu();
                else if (orderMenu == null && PointerOnEnemyBox())
                {
                    enemySelected = true;
                    OpenEnemyMenu(pointer);
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                panDrag = false;
                panCandidate = false;
                if (mapMode)
                    SnapMapToPlayer();
                else if (detailMode)
                    ExitCabin();
                else
                    EnterCabin();
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                panDrag = false;
                panCandidate = false;
                if (mapMode)
                    LeaveMap();
                else
                    EnterMap();
            }
        }

        static float HelmRoll(CombatShip ship)
        {
            if (ship == null)
                return 0f;
            return 90f - BowClockwise(ship) - ship.heading;
        }

        static float BowClockwise(CombatShip ship)
        {
            if (!TryKindCenter(ship, RoomKind.Bridge, out Vector2 bridge))
                return 0f;
            if (!TryKindCenter(ship, RoomKind.Thruster, out Vector2 stern))
                return 0f;
            Vector2 bow = bridge - stern;
            if (bow.sqrMagnitude < 0.25f)
                return 0f;
            return Mathf.Atan2(bow.x, bow.y) * Mathf.Rad2Deg;
        }

        static bool TryKindCenter(CombatShip ship, RoomKind kind, out Vector2 center)
        {
            center = Vector2.zero;
            if (ship == null || ship.build == null)
                return false;
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomPlacement placement = ship.build.rooms[i];
                if (placement == null || placement.room == null || placement.room.kind != kind)
                    continue;
                sum.x += placement.x + placement.SpanX * 0.5f;
                sum.y += placement.y + placement.SpanY * 0.5f;
                count++;
            }

            if (count == 0)
                return false;
            center = sum / count;
            return true;
        }

        static void EnterCabin()
        {
            CombatShip target = bound != null && bound.player != null && !bound.player.destroyed ? bound.player : null;
            if (target == null)
                return;
            helmZoom = zoom;
            EndRangePick();
            detailMode = true;
            lockHeading = true;
            cameraRoll = HelmRoll(target);
            zoomAnchorLive = false;
            detailPan = Vector2.zero;
            followPlayer = true;
            trackWho = TrackWho.Player;
            cameraFocus = target.position;
            zoom = ZoomToFit(target);
            detailFloor = zoom * 0.9f;
            viewScale = PixelsPerMeter * zoom;
        }

        static void ExitCabin()
        {
            detailMode = false;
            detailPan = Vector2.zero;
            zoom = helmZoom;
            lockHeading = false;
            cameraRoll = 0f;
            followPlayer = true;
            trackWho = TrackWho.Player;
            trackGrade = TrackGrades;
            zoomAnchorLive = false;
            if (bound != null && bound.player != null)
                cameraFocus = bound.player.position;
            viewScale = PixelsPerMeter * zoom;
        }

        static void EnterMap()
        {
            if (detailMode)
                ExitCabin();
            helmZoom = zoom;
            mapMode = true;
            detailMode = false;
            detailPan = Vector2.zero;
            lockHeading = false;
            cameraRoll = 0f;
            zoomAnchorLive = false;
            zoom = mapZoom;
            viewScale = PixelsPerMeter * zoom;
            followPlayer = true;
            trackWho = TrackWho.Player;
            trackGrade = TrackGrades;
            if (bound != null && bound.player != null)
                cameraFocus = bound.player.position;
        }

        static void LeaveMap()
        {
            mapZoom = zoom;
            mapMode = false;
            detailMode = false;
            detailPan = Vector2.zero;
            lockHeading = false;
            cameraRoll = 0f;
            zoomAnchorLive = false;
            zoom = helmZoom;
            followPlayer = true;
            trackWho = TrackWho.Player;
            trackGrade = TrackGrades;
            if (bound != null && bound.player != null)
                cameraFocus = bound.player.position;
            viewScale = PixelsPerMeter * zoom;
        }

        static void SnapMapToPlayer()
        {
            lockHeading = false;
            cameraRoll = 0f;
            zoomAnchorLive = false;
            BeginTrack(TrackWho.Player);
            followPlayer = true;
            if (bound != null && bound.player != null)
                cameraFocus = bound.player.position;
        }

        static float ZoomToFit(CombatShip ship)
        {
            float width = field != null ? field.resolvedStyle.width : 0f;
            float height = field != null ? field.resolvedStyle.height : 0f;
            if (width < 8f)
                width = 1280f;
            if (height < 8f)
                height = 720f;
            HullDef hull = ship != null && ship.build != null ? ship.build.hull : null;
            float shipW = hull != null ? hull.width * CellPixels : 100f;
            float shipH = hull != null ? hull.height * CellPixels : 60f;
            const float fill = 0.9f;
            float fit = Mathf.Min(width * fill / shipW, height * fill / shipH);
            return Mathf.Clamp(fit, MinZoom, MaxZoom);
        }

        static VisualElement BuildEnemyHit()
        {
            var hit = new VisualElement { name = "enemy-hit", pickingMode = PickingMode.Position };
            hit.style.position = Position.Absolute;
            hit.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            hit.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == (int)MouseButton.LeftMouse)
                {
                    if (ConsumeRangeClick())
                    {
                        evt.StopPropagation();
                        return;
                    }

                    enemySelected = true;
                    NoteEnemyClick();
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != (int)MouseButton.RightMouse)
                    return;
                if (rangePicking)
                {
                    EndRangePick();
                    evt.StopPropagation();
                    return;
                }
                if (orderMenu != null && enemyMenuFrame != Time.frameCount)
                    DismissOrderMenu();
                else
                {
                    enemySelected = true;
                    OpenEnemyMenu(HitCenter());
                }
                evt.StopPropagation();
            });
            hit.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    if (ConsumeRangeClick())
                    {
                        evt.StopPropagation();
                        return;
                    }

                    enemySelected = true;
                    NoteEnemyClick();
                    evt.StopPropagation();
                    return;
                }

                if (evt.button != 1 && (evt.pressedButtons & 2) == 0)
                    return;
                if (rangePicking)
                {
                    EndRangePick();
                    evt.StopPropagation();
                    return;
                }
                if (orderMenu != null && enemyMenuFrame != Time.frameCount)
                    DismissOrderMenu();
                else
                {
                    enemySelected = true;
                    OpenEnemyMenu(HitCenter());
                }
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            hit.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                if (rangePicking)
                {
                    EndRangePick();
                    evt.StopImmediatePropagation();
                    return;
                }
                enemySelected = true;
                OpenEnemyMenu(HitCenter());
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            field.Add(hit);
            return hit;
        }

        static void PlaceEnemyHit()
        {
            if (enemyHit == null)
                return;
            CombatShip ship = bound != null ? bound.enemy : null;
            if (ship == null || ship.destroyed || ship.build == null || ship.build.hull == null)
            {
                enemySelected = false;
                enemyBoxLive = false;
                enemyHit.style.display = DisplayStyle.None;
                return;
            }

            HullDef hull = ship.build.hull;
            const float pad = 2f;
            float span;
            if (ShowIcon(ship))
                span = IconSize + pad * 2f;
            else
            {
                float w = hull.width * CellPixels * zoom;
                float h = hull.height * CellPixels * zoom;
                float rad = (ship.heading + cameraRoll) * Mathf.Deg2Rad;
                float absCos = Mathf.Abs(Mathf.Cos(rad));
                float absSin = Mathf.Abs(Mathf.Sin(rad));
                span = Mathf.Max(w * absCos + h * absSin, w * absSin + h * absCos) + pad * 2f;
            }
            Vector2 center = Project(ship.position);
            enemyBox = new Rect(center.x - span * 0.5f, center.y - span * 0.5f, span, span);
            enemyBoxLive = true;
            enemyHit.style.display = DisplayStyle.Flex;
            enemyHit.style.left = center.x - span * 0.5f;
            enemyHit.style.top = center.y - span * 0.5f;
            enemyHit.style.width = span;
            enemyHit.style.height = span;
            enemyHit.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            float edge = enemySelected ? 2f : 0f;
            var stroke = new StyleColor(new Color(0.93f, 0.96f, 1f, 1f));
            enemyHit.style.borderTopWidth = edge;
            enemyHit.style.borderRightWidth = edge;
            enemyHit.style.borderBottomWidth = edge;
            enemyHit.style.borderLeftWidth = edge;
            enemyHit.style.borderTopColor = stroke;
            enemyHit.style.borderRightColor = stroke;
            enemyHit.style.borderBottomColor = stroke;
            enemyHit.style.borderLeftColor = stroke;
        }

        static Vector2 HitCenter()
        {
            if (enemyHit == null)
                return Vector2.zero;
            float width = enemyHit.resolvedStyle.width;
            float height = enemyHit.resolvedStyle.height;
            if (width < 1f)
                width = enemyHit.layout.width;
            if (height < 1f)
                height = enemyHit.layout.height;
            return new Vector2(enemyHit.resolvedStyle.left + width * 0.5f, enemyHit.resolvedStyle.top + height * 0.5f);
        }

        static void OpenEnemyMenu(Vector2 fieldLocal)
        {
            if (Time.frameCount == enemyMenuFrame || Time.frameCount == menuClosedFrame || Time.frameCount == rangeCancelFrame)
                return;
            enemyMenuFrame = Time.frameCount;
            ShowOrderMenu(fieldLocal);
        }

        static Vector2 MousePanelPoint()
        {
            Vector2 screen = Input.mousePosition;
            Vector2 converted = RuntimePanelUtils.ScreenToPanel(field.panel, screen);
            if (field.worldBound.Contains(converted))
                return converted;
            Vector2 flipped = new Vector2(screen.x, Screen.height - screen.y);
            if (field.worldBound.Contains(flipped))
                return flipped;
            return converted;
        }

        static bool PointerOnEnemyBox()
        {
            if (!enemyBoxLive || enemyBox.width < 2f || enemyBox.height < 2f)
                return false;
            if (pointerOverField && InsideBox(fieldPointerLocal))
                return true;
            if (TryMouseFieldLocal(out Vector2 local) && InsideBox(local))
                return true;
            return false;
        }

        static bool InsideBox(Vector2 local)
        {
            return local.x >= enemyBox.xMin && local.x <= enemyBox.xMax
                && local.y >= enemyBox.yMin && local.y <= enemyBox.yMax;
        }

        static void NoteEnemyClick()
        {
            if (lastEnemyClickFrame == Time.frameCount)
                return;
            lastEnemyClickFrame = Time.frameCount;
            float now = Time.unscaledTime;
            if (mapMode && now - lastEnemyClick <= 0.35f && bound != null && bound.enemy != null && !bound.enemy.destroyed)
                BeginTrack(TrackWho.Enemy);
            lastEnemyClick = now;
        }

        static void BeginTrack(TrackWho who)
        {
            trackWho = who;
            trackGrade = TrackGrades;
            zoomAnchorLive = false;
            followPlayer = who == TrackWho.Player;
            if (detailMode || bound == null)
                return;
            CombatShip target = who == TrackWho.Enemy ? bound.enemy : bound.player;
            if (target != null && !target.destroyed)
                cameraFocus = target.position;
        }

        static void ApplyTrack(CombatSim sim)
        {
            if (zoomAnchorLive && combatState)
            {
                if (combatPivotShip != null && !combatPivotShip.destroyed)
                {
                    cameraFocus = combatPivotShip.position + zoomAnchorOffset;
                    return;
                }

                zoomAnchorLive = false;
                combatPivotShip = null;
            }

            if (trackWho == TrackWho.None || trackGrade <= 0 || sim == null)
                return;
            CombatShip target = trackWho == TrackWho.Enemy ? sim.enemy : sim.player;
            if (target == null || target.destroyed)
            {
                trackWho = TrackWho.None;
                trackGrade = 0;
                followPlayer = false;
                zoomAnchorLive = false;
                return;
            }

            if (trackGrade >= TrackGrades)
            {
                cameraFocus = target.position;
                return;
            }

            float rate = TrackRate[Mathf.Clamp(trackGrade, 0, TrackRate.Length - 1)];
            float blend = 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime);
            cameraFocus = Vector2.Lerp(cameraFocus, target.position, blend);
        }

        static bool PointerOnMenu()
        {
            if (orderMenu == null || orderMenu.panel == null)
                return false;
            Vector2 screen = Input.mousePosition;
            Vector2 panel = RuntimePanelUtils.ScreenToPanel(orderMenu.panel, screen);
            if (MenuContains(panel))
                return true;
            Vector2 flipped = new Vector2(screen.x, Screen.height - screen.y);
            return MenuContains(flipped);
        }

        static bool MenuContains(Vector2 panel)
        {
            if (orderMenu.worldBound.Contains(panel))
                return true;
            VisualElement picked = orderMenu.panel.Pick(panel);
            return picked != null && orderMenu.Contains(picked);
        }

        static void DismissOrderMenu()
        {
            if (orderMenu == null)
                return;
            menuClosedFrame = Time.frameCount;
            CloseOrderMenu();
        }

        static void CloseOrderMenu()
        {
            if (orderMenu != null && orderMenu.parent != null)
                orderMenu.RemoveFromHierarchy();
            orderMenu = null;
        }

        static void ShowOrderMenu(Vector2 fieldLocal)
        {
            menuFieldAnchor = fieldLocal;
            CloseOrderMenu();
            if (bound == null || root == null || field == null)
                return;
            orderMenu = BuildOrderMenu();
            root.Add(orderMenu);
            Vector2 world = field.LocalToWorld(fieldLocal);
            Vector2 local = root.WorldToLocal(world);
            orderMenu.style.left = local.x + 12f;
            orderMenu.style.top = local.y + 12f;
            UiFonts.Apply(orderMenu);
            orderMenu.schedule.Execute(ClampMenu).StartingIn(16);
        }

        static void ClampMenu()
        {
            if (orderMenu == null || root == null)
                return;
            float width = orderMenu.resolvedStyle.width;
            float height = orderMenu.resolvedStyle.height;
            float limitX = root.resolvedStyle.width;
            float limitY = root.resolvedStyle.height;
            if (width < 2f || limitX < 2f)
                return;
            float left = orderMenu.resolvedStyle.left;
            float top = orderMenu.resolvedStyle.top;
            if (float.IsNaN(left))
                left = 8f;
            if (float.IsNaN(top))
                top = 8f;
            orderMenu.style.left = Mathf.Clamp(left, 8f, Mathf.Max(8f, limitX - width - 8f));
            orderMenu.style.top = Mathf.Clamp(top, 8f, Mathf.Max(8f, limitY - height - 8f));
        }

        static VisualElement BuildOrderMenu()
        {
            var menu = new VisualElement();
            menu.AddToClassList("order-menu");
            var head = new Label("玩家策略");
            head.AddToClassList("order-head");
            menu.Add(head);
            menu.Add(StrategyButton(ShipOrder.Manual, "手动"));
            menu.Add(StrategyButton(ShipOrder.Track, "追击"));
            menu.Add(StrategyButton(ShipOrder.Parallel, "平行"));
            menu.Add(StrategyButton(ShipOrder.Orbit, "环绕"));
            menu.Add(StrategyButton(ShipOrder.Dock, "停靠"));
            menu.Add(StrategyButton(ShipOrder.BreakOff, "脱离"));
            return menu;
        }

        static Button StrategyButton(ShipOrder kind, string title)
        {
            var button = new Button { text = title };
            ArmChip(button, () => ChooseStrategy(kind));
            button.AddToClassList("strategy-button");
            if (bound != null && bound.order == kind)
                button.AddToClassList("order-on");
            return button;
        }

        static void ChooseStrategy(ShipOrder kind)
        {
            if (kind == ShipOrder.Manual)
            {
                if (bound != null)
                    bound.ReleaseMoveOrder();
                EndRangePick();
                DismissOrderMenu();
                return;
            }

            if (kind == ShipOrder.Dock)
            {
                if (bound != null)
                    bound.SetMoveOrder(ShipOrder.Dock, 0f);
                EndRangePick();
                DismissOrderMenu();
                return;
            }

            BeginRangePick(kind);
        }

        static void BeginRangePick(ShipOrder kind)
        {
            if (bound == null || bound.enemy == null || bound.enemy.destroyed || field == null)
                return;
            CloseOrderMenu();
            rangeOrder = kind;
            rangePicking = true;
            rangeMoved = false;
            rangeArmFrame = Time.frameCount;
            rangeMeters = 28f / Mathf.Max(viewScale, 0.0001f);
            if (!TryRangePointer(out rangeAnchorLocal) && bound.enemy != null)
                rangeAnchorLocal = Project(bound.enemy.position);
            PlaceRangeRing();
        }

        static void EndRangePick()
        {
            if (rangePicking)
                rangeCancelFrame = Time.frameCount;
            rangePicking = false;
            rangeMoved = false;
            HideRangeRing();
        }

        static void TakeRangeClick()
        {
            if (!rangePicking || Time.frameCount == rangeArmFrame || rangeHandledFrame == Time.frameCount || bound == null)
                return;
            rangeHandledFrame = Time.frameCount;
            bound.SetMoveOrder(rangeOrder, rangeMeters);
            EndRangePick();
        }

        static bool ConsumeRangeClick()
        {
            if (rangeHandledFrame == Time.frameCount)
                return true;
            if (!rangePicking || Time.frameCount == rangeArmFrame)
                return false;
            TakeRangeClick();
            return rangeHandledFrame == Time.frameCount;
        }

        static void HideRangeRing()
        {
            if (rangeRing != null)
                rangeRing.style.display = DisplayStyle.None;
            if (rangeReadout != null)
                rangeReadout.style.display = DisplayStyle.None;
        }

        static void PlaceRangeRing()
        {
            if (rangeRing == null)
                return;
            if (!rangePicking || bound == null || bound.enemy == null || bound.enemy.destroyed)
            {
                if (rangePicking)
                    EndRangePick();
                else
                    HideRangeRing();
                return;
            }

            CombatShip enemy = bound.enemy;
            if (TryRangePointer(out Vector2 local))
            {
                if (!rangeMoved && (local - rangeAnchorLocal).sqrMagnitude >= 36f)
                    rangeMoved = true;
                if (rangeMoved)
                {
                    Vector2 world = Unproject(local);
                    rangeMeters = Mathf.Clamp((world - enemy.position).magnitude, 1f, 100000f);
                }
                else
                    rangeMeters = 28f / Mathf.Max(viewScale, 0.0001f);
                if (rangeReadout != null)
                {
                    rangeReadout.style.display = DisplayStyle.Flex;
                    rangeReadout.style.left = local.x + 16f;
                    rangeReadout.style.top = local.y - 28f;
                    rangeReadout.text = CombatSim.RangeLabel(rangeMeters);
                }
            }

            float diameter = Mathf.Max(8f, rangeMeters * 2f * viewScale);
            PlaceCircle(rangeRing, enemy.position, diameter);
            rangeRing.style.display = DisplayStyle.Flex;
            rangeRing.BringToFront();
            if (rangeReadout != null)
                rangeReadout.BringToFront();
        }

        static bool TryRangePointer(out Vector2 local)
        {
            if (TryMouseFieldLocal(out local))
                return true;
            if (pointerOverField)
            {
                local = fieldPointerLocal;
                return true;
            }

            local = Vector2.zero;
            return false;
        }

        static void ArmChip(Button button, Action apply)
        {
            button.focusable = false;
            button.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                apply();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            button.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.LeftMouse)
                    return;
                apply();
                evt.StopPropagation();
            });
        }

        static VisualElement BuildFireDock()
        {
            var dock = new VisualElement { name = "fire-dock" };
            dock.AddToClassList("fire-dock");
            dock.Add(FireSwitch("全部开火", () => bound.SetAllFire(true), () => bound != null && bound.fireEnergy && bound.fireBallistic && bound.fireMissile));
            dock.Add(FireSwitch("全部停火", () => bound.SetAllFire(false), () => bound != null && !bound.fireEnergy && !bound.fireBallistic && !bound.fireMissile));
            dock.Add(FireSwitch("能量开火", () => bound.SetFamilyFire(WeaponFamily.Energy, true), () => bound != null && bound.fireEnergy));
            dock.Add(FireSwitch("能量停火", () => bound.SetFamilyFire(WeaponFamily.Energy, false), () => bound != null && !bound.fireEnergy));
            dock.Add(FireSwitch("火炮开火", () => bound.SetFamilyFire(WeaponFamily.Ballistic, true), () => bound != null && bound.fireBallistic));
            dock.Add(FireSwitch("火炮停火", () => bound.SetFamilyFire(WeaponFamily.Ballistic, false), () => bound != null && !bound.fireBallistic));
            dock.Add(FireSwitch("导弹开火", () => bound.SetFamilyFire(WeaponFamily.Missile, true), () => bound != null && bound.fireMissile));
            dock.Add(FireSwitch("导弹停火", () => bound.SetFamilyFire(WeaponFamily.Missile, false), () => bound != null && !bound.fireMissile));
            dock.Add(FireSwitch("优先能源", () => bound.SetAttackFocus(AttackFocus.Power), () => bound != null && bound.attackFocus == AttackFocus.Power));
            dock.Add(FireSwitch("优先武器", () => bound.SetAttackFocus(AttackFocus.Weapon), () => bound != null && bound.attackFocus == AttackFocus.Weapon));
            dock.Add(FireSwitch("优先动力", () => bound.SetAttackFocus(AttackFocus.Drive), () => bound != null && bound.attackFocus == AttackFocus.Drive));
            return dock;
        }

        static Button FireSwitch(string title, Action apply, Func<bool> selected)
        {
            var button = new Button(() =>
            {
                if (bound == null)
                    return;
                apply();
                RefreshFireDock();
            })
            { text = title };
            button.focusable = false;
            button.userData = selected;
            button.AddToClassList("fire-switch");
            return button;
        }

        static void RefreshFireDock()
        {
            if (fireDock == null)
                return;
            for (int i = 0; i < fireDock.childCount; i++)
            {
                if (!(fireDock[i] is Button button))
                    continue;
                var selected = button.userData as Func<bool>;
                button.EnableInClassList("order-on", selected != null && selected());
            }
        }

        static bool PointerOnFireDock()
        {
            if (fireDock == null || fireDock.panel == null)
                return false;
            Rect box = fireDock.worldBound;
            if (box.width < 2f || box.height < 2f)
                return false;
            Vector2 screen = Input.mousePosition;
            if (box.Contains(RuntimePanelUtils.ScreenToPanel(fireDock.panel, screen)))
                return true;
            return box.Contains(new Vector2(screen.x, Screen.height - screen.y));
        }

        static string HeatLine(CombatShip ship)
        {
            if (ship.build == null)
                return "";
            string text = "";
            for (int i = 0; i < ship.build.rooms.Count; i++)
            {
                RoomDef room = ship.build.rooms[i] != null ? ship.build.rooms[i].room : null;
                if (room == null || room.heatOutput <= 0f)
                    continue;
                text += room.displayName + " " + ship.heat[i].ToString("0.#") + "/" + room.heatCapacity.ToString("0.#");
                if (CombatSim.Overheated(ship, i))
                    text += " 过热";
                text += "   ";
            }

            return text;
        }
    }
}
