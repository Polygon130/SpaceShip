using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip
{
    public static class AssemblyPanelView
    {
        const float CellStride = 38f;
        const float MinZoom = 0.75f;
        const float MaxZoom = 3f;
        const float TextSize = 20f;

        sealed class CellRef
        {
            public int x;
            public int y;
            public int floorIndex = -1;
            public int halfIndex = -1;
            public bool blocked;
            public VisualElement element;
        }

        sealed class Drag
        {
            public int pointerId;
            public RoomDef room;
            public bool moving;
            public int originX;
            public int originY;
            public int grabX;
            public int grabY;
            public int rotation;
            public int originRotation;
            public VisualElement handle;
            public Label ghost;
        }

        sealed class PanelState
        {
            public Drag drag;
            public bool rotateHooked;
            public bool zoomHooked;
            public bool labelRetried;
            public bool stampingLabels;
            public float zoom = 1f;
            public float cellStride = CellStride;
            public Vector2 lastPointer;
            public float lastRotate = -1f;
            public bool rightWasDown;
            public string notice;
            public VisualElement tree;
            public VisualElement board;
            public ShipBuild build;
            public FlightTuning flight;
            public IList<RoomDef> catalog;
            public bool writable;
            public Action beforeMutate;
            public readonly List<CellRef> cells = new List<CellRef>();
        }

        public static void Bind(VisualElement tree, ShipBuild build, FlightTuning flight, IList<RoomDef> catalog, bool writable, Action beforeMutate = null)
        {
            if (tree == null)
                return;

            VisualElement paletteHost = tree.Q("palette-list");
            VisualElement gridHost = tree.Q("grid-host");
            VisualElement stats = tree.Q<VisualElement>("stat-body");
            Label message = tree.Q<Label>("message");
            if (paletteHost == null || gridHost == null || stats == null || message == null)
                return;

            var state = tree.userData as PanelState ?? new PanelState();
            tree.userData = state;
            state.tree = tree;
            state.board = tree.Q("assembly-root") ?? tree;
            state.build = build;
            state.flight = flight;
            state.catalog = catalog;
            state.writable = writable;
            state.beforeMutate = beforeMutate;
            state.labelRetried = false;
            ClearDrag(state);
            EnsureRotateHook(state);
            EnsureZoomHook(state);

            VisualElement palette = paletteHost is ScrollView scroll ? scroll.contentContainer : paletteHost;
            palette.Clear();
            gridHost.Clear();
            state.cells.Clear();

            if (build == null || build.hull == null)
            {
                FillStats(stats, null, null, null);
                message.EnableInClassList("text-bad", false);
                message.text = "菜单：星际商船 / 拼船面板";
                UiFonts.Apply(tree);
                return;
            }

            ShipReport report = ShipEvaluator.Evaluate(build);
            CellMap roomAt = CellMap.Build(build);

            if (catalog == null || catalog.Count == 0)
                catalog = CollectRooms(build);
            state.catalog = catalog;
            for (int i = 0; i < catalog.Count; i++)
            {
                RoomDef room = catalog[i];
                var chip = new VisualElement();
                chip.AddToClassList("chip");
                chip.style.backgroundColor = room.swatch;
                string footprint = room.halfCell ? "半格" : room.width + "×" + room.height;
                var chipLabel = new Label(room.displayName + "  " + footprint);
                chipLabel.pickingMode = PickingMode.Ignore;
                chip.Add(chipLabel);
                RoomDef captured = room;
                ListenDrag(state, chip, evt => StartPaletteDrag(state, captured, evt));
                palette.Add(chip);
            }

            for (int y = build.hull.height - 1; y >= 0; y--)
            {
                var row = new VisualElement();
                row.AddToClassList("grid-row");
                int rowY = y;
                for (int x = 0; x < build.hull.width; x++)
                {
                    int cellX = x;
                    var cell = new VisualElement();
                    cell.AddToClassList("cell");
                    cell.pickingMode = PickingMode.Position;
                    var tag = new CellRef { x = cellX, y = rowY, element = cell };
                    cell.userData = tag;
                    state.cells.Add(tag);
                    int cellKey = rowY * build.hull.width + cellX;
                    if (!build.hull.IsOpen(cellX, rowY))
                    {
                        tag.blocked = true;
                        cell.AddToClassList("cell-blocked");
                    }
                    else
                    {
                        bool hasFloor = roomAt.floor.TryGetValue(cellKey, out int floorIndex);
                        bool hasHalf = roomAt.half.TryGetValue(cellKey, out int halfIndex);
                        if (hasFloor || hasHalf)
                        {
                            if (hasFloor)
                                tag.floorIndex = floorIndex;
                            if (hasHalf)
                                tag.halfIndex = halfIndex;
                            cell.tooltip = RoomTip(build, report, hasFloor ? floorIndex : -1, hasHalf ? halfIndex : -1);
                            CellRef capturedTag = tag;
                            ListenDrag(state, cell, evt => StartGridDrag(state, capturedTag, evt));
                        }
                        else
                        {
                            RoomDef bare = build.hull.defaultCabin;
                            if (bare != null)
                            {
                                cell.style.backgroundColor = bare.swatch;
                                cell.tooltip = bare.displayName;
                            }

                            cell.AddToClassList("cell-empty");
                        }
                    }

                    if (report.heatCells.Contains(cellKey))
                        cell.AddToClassList("cell-heat");
                    if (report.damageCells.Contains(cellKey))
                        cell.AddToClassList("cell-dc");

                    row.Add(cell);
                }

                gridHost.Add(row);
            }

            ScheduleRoomLabels(state, gridHost);
            ApplyZoom(state);
            FillStats(stats, build, report, flight);
            bool alarm = report.errors.Count > 0 || AlarmNotice(state.notice);
            message.EnableInClassList("text-bad", alarm);
            if (!string.IsNullOrEmpty(state.notice))
                message.text = state.notice;
            else if (report.errors.Count > 0)
                message.text = string.Join("\n", report.errors);
            else
                message.text = writable
                    ? "从左侧把舱室拖到船壳上。右键转动已放下的舱，拖动时右键也能转。白边是朝向；正方形占地不变，长条会横竖对调。气阀是半格，可以和通道放在同一格。功能模块挨着就能连通。滚轮缩放。拖出格子就拆掉。"
                    : "当前不能改布局。";
            state.notice = null;
            UiFonts.Apply(tree);
        }

        static void EnsureZoomHook(PanelState state)
        {
            if (state.zoomHooked)
                return;
            Button zoomOut = state.tree.Q<Button>("zoom-out");
            Button zoomIn = state.tree.Q<Button>("zoom-in");
            ScrollView scroll = state.tree.Q<ScrollView>("grid-scroll");
            if (zoomOut == null || zoomIn == null || scroll == null)
                return;
            state.zoomHooked = true;
            scroll.mode = ScrollViewMode.VerticalAndHorizontal;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            scroll.mouseWheelScrollSize = 0f;
            zoomOut.clicked += () => ZoomStep(state, -1);
            zoomIn.clicked += () => ZoomStep(state, 1);
            scroll.RegisterCallback<WheelEvent>(evt =>
            {
                ZoomStep(state, evt.delta.y < 0f ? 1 : -1);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }

        static void ZoomStep(PanelState state, int direction)
        {
            float next = Mathf.Clamp(state.zoom + direction * 0.25f, MinZoom, MaxZoom);
            next = Mathf.Round(next * 4f) / 4f;
            if (Mathf.Approximately(next, state.zoom))
                return;
            state.zoom = next;
            ApplyZoom(state);
        }

        static void ApplyZoom(PanelState state)
        {
            state.cellStride = CellStride * state.zoom;
            float size = 36f * state.zoom;
            float margin = state.zoom;
            for (int i = 0; i < state.cells.Count; i++)
            {
                VisualElement cell = state.cells[i].element;
                cell.style.width = size;
                cell.style.height = size;
                cell.style.marginLeft = margin;
                cell.style.marginRight = margin;
                cell.style.marginTop = margin;
                cell.style.marginBottom = margin;
            }

            Label zoomLabel = state.tree.Q<Label>("zoom-label");
            if (zoomLabel != null)
                zoomLabel.text = Mathf.RoundToInt(state.zoom * 100f) + "%";
            if (state.drag != null)
            {
                FitGhost(state.drag, state.cellStride);
                PlaceGhost(state, state.lastPointer);
                Highlight(state, state.lastPointer);
            }

            VisualElement gridHost = state.tree.Q("grid-host");
            if (gridHost != null)
                ScheduleRoomLabels(state, gridHost);
        }

        static void EnsureRotateHook(PanelState state)
        {
            if (state.rotateHooked)
                return;
            state.rotateHooked = true;
            state.tree.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!IsRightClick(evt))
                    return;
                OnRightClick(state, evt.position);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            state.tree.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != (int)MouseButton.RightMouse)
                    return;
                OnRightClick(state, evt.mousePosition);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            state.tree.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                OnRightClick(state, evt.mousePosition);
                evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            state.tree.schedule.Execute(() => PollRightClick(state)).Every(16);
        }

        static void PollRightClick(PanelState state)
        {
            if (!Application.isPlaying)
                return;
            bool down = Input.GetMouseButton(1);
            if (down && !state.rightWasDown)
            {
                if (state.drag != null)
                    TryRotate(state, state.lastPointer);
                else if (state.tree != null && state.tree.panel != null)
                    RotatePlaced(state, RuntimePanelUtils.ScreenToPanel(state.tree.panel, Input.mousePosition));
            }

            state.rightWasDown = down;
        }

        static void OnRightClick(PanelState state, Vector2 pointer)
        {
            if (state.drag != null)
                TryRotate(state, pointer);
            else
                RotatePlaced(state, pointer);
        }

        static bool IsRightClick(IPointerEvent evt)
        {
            return evt.button == 1 || (evt.pressedButtons & 2) != 0;
        }

        static void TryRotate(PanelState state, Vector2 pointer)
        {
            if (state.drag == null || !AcceptRotate(state))
                return;
            RotateDrag(state, PointerInPanel(state, pointer));
        }

        static bool AcceptRotate(PanelState state)
        {
            float now = Time.realtimeSinceStartup;
            if (now - state.lastRotate < 0.2f)
                return false;
            state.lastRotate = now;
            return true;
        }

        static void ListenDrag(PanelState state, VisualElement element, Action<PointerDownEvent> begin)
        {
            element.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                {
                    if (IsRightClick(evt) && state.drag != null && state.drag.handle == element)
                    {
                        TryRotate(state, evt.position);
                        evt.StopImmediatePropagation();
                    }
                    return;
                }
                begin(evt);
                if (state.drag == null)
                    return;
                state.drag.handle = element;
                element.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (state.drag == null || state.drag.handle != element || state.drag.pointerId != evt.pointerId)
                    return;
                Vector2 point = PointerInPanel(state, evt.position);
                state.lastPointer = point;
                PlaceGhost(state, point);
                Highlight(state, point);
                evt.StopPropagation();
            });
            element.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (evt.button != 0 || Time.realtimeSinceStartup - state.lastRotate < 0.2f)
                    return;
                if (state.drag == null || state.drag.handle != element || state.drag.pointerId != evt.pointerId)
                    return;
                if (element.HasPointerCapture(evt.pointerId))
                    element.ReleasePointer(evt.pointerId);
                FinishDrag(state, PointerInPanel(state, evt.position));
                evt.StopPropagation();
            });
        }

        static void StartPaletteDrag(PanelState state, RoomDef room, PointerDownEvent evt)
        {
            if (!state.writable || room == null)
                return;
            BeginDrag(state, evt.pointerId, room, false, 0, 0, 0, 0, 0, PointerInPanel(state, evt.position));
        }

        static void StartGridDrag(PanelState state, CellRef tag, PointerDownEvent evt)
        {
            int index = PickRoom(state, tag, evt.position);
            if (!state.writable || index < 0)
                return;
            RoomPlacement placement = state.build.rooms[index];
            if (placement == null || placement.room == null)
                return;
            BeginDrag(
                state,
                evt.pointerId,
                placement.room,
                true,
                placement.x,
                placement.y,
                tag.x - placement.x,
                tag.y - placement.y,
                placement.rotation,
                PointerInPanel(state, evt.position));
        }

        static void BeginDrag(PanelState state, int pointerId, RoomDef room, bool moving, int originX, int originY, int grabX, int grabY, int rotation, Vector2 pointer)
        {
            ClearDrag(state);
            var ghost = new Label { pickingMode = PickingMode.Ignore };
            ghost.AddToClassList("drag-ghost");
            ghost.style.backgroundColor = room.swatch;
            ghost.style.position = Position.Absolute;
            ghost.style.unityTextAlign = TextAnchor.MiddleCenter;
            state.board.Add(ghost);
            int turns = ((rotation % 4) + 4) % 4;
            state.drag = new Drag
            {
                pointerId = pointerId,
                room = room,
                moving = moving,
                originX = originX,
                originY = originY,
                grabX = grabX,
                grabY = grabY,
                rotation = turns,
                originRotation = turns,
                ghost = ghost
            };
            FitGhost(state.drag, state.cellStride);
            UpdateFacing(state.drag, state.cellStride);
            state.lastPointer = pointer;
            PlaceGhost(state, pointer);
            UiFonts.Apply(ghost);
        }

        static void RotateDrag(PanelState state, Vector2 pointer)
        {
            Drag drag = state.drag;
            if (drag == null || drag.room == null)
                return;
            int oldWidth = FootprintX(drag);
            int grabX = drag.grabX;
            int grabY = drag.grabY;
            drag.rotation = (drag.rotation + 1) % 4;
            drag.grabX = grabY;
            drag.grabY = oldWidth - 1 - grabX;
            int spanX = FootprintX(drag);
            int spanY = FootprintY(drag);
            drag.grabX = Mathf.Clamp(drag.grabX, 0, spanX - 1);
            drag.grabY = Mathf.Clamp(drag.grabY, 0, spanY - 1);
            FitGhost(drag, state.cellStride);
            UpdateFacing(drag, state.cellStride);
            PlaceGhost(state, pointer);
            Highlight(state, pointer);
        }

        static void FitGhost(Drag drag, float stride)
        {
            float width = FootprintX(drag) * stride;
            float height = FootprintY(drag) * stride;
            if (drag.room != null && drag.room.halfCell)
            {
                int turns = drag.rotation & 3;
                if (turns == 1 || turns == 3)
                    width *= 0.5f;
                else
                    height *= 0.5f;
            }

            drag.ghost.style.width = width;
            drag.ghost.style.height = height;
            drag.ghost.text = FitName(drag.room != null ? drag.room.displayName : "", width, height);
        }

        static void UpdateFacing(Drag drag, float stride)
        {
            if (drag.ghost == null)
                return;
            VisualElement mark = drag.ghost.Q(className: "face-mark");
            if (mark == null)
            {
                mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.AddToClassList("face-mark");
                mark.style.position = Position.Absolute;
                mark.style.backgroundColor = new Color(1f, 1f, 1f, 0.92f);
                drag.ghost.Add(mark);
            }

            float thickness = Mathf.Max(4f, stride * 0.12f);
            float width = FootprintX(drag) * stride;
            float height = FootprintY(drag) * stride;
            int turns = drag.rotation & 3;
            if (drag.room != null && drag.room.halfCell)
            {
                if (turns == 1 || turns == 3)
                    width *= 0.5f;
                else
                    height *= 0.5f;
            }
            mark.style.left = turns == 3 ? 0 : StyleKeyword.Auto;
            mark.style.top = turns == 0 ? 0 : StyleKeyword.Auto;
            mark.style.right = turns == 1 ? 0 : StyleKeyword.Auto;
            mark.style.bottom = turns == 2 ? 0 : StyleKeyword.Auto;
            if (turns == 0 || turns == 2)
            {
                mark.style.left = 0;
                mark.style.width = width;
                mark.style.height = thickness;
                mark.style.top = turns == 0 ? 0 : StyleKeyword.Auto;
                mark.style.bottom = turns == 2 ? 0 : StyleKeyword.Auto;
            }
            else
            {
                mark.style.top = 0;
                mark.style.width = thickness;
                mark.style.height = height;
                mark.style.left = turns == 3 ? 0 : StyleKeyword.Auto;
                mark.style.right = turns == 1 ? 0 : StyleKeyword.Auto;
            }
        }

        static int FootprintX(Drag drag)
        {
            return (drag.rotation & 1) == 1 ? drag.room.height : drag.room.width;
        }

        static int FootprintY(Drag drag)
        {
            return (drag.rotation & 1) == 1 ? drag.room.width : drag.room.height;
        }

        static bool StampRoomCover(PanelState state, VisualElement gridHost, RoomPlacement placement, ShipReport report, int index)
        {
            CellRef origin = FindCell(state, placement.x, placement.y);
            CellRef far = FindCell(state, placement.x + placement.SpanX - 1, placement.y + placement.SpanY - 1);
            if (origin == null || far == null)
                return true;
            Rect low = origin.element.worldBound;
            Rect high = far.element.worldBound;
            if (low.width <= 0f || high.width <= 0f)
                return false;

            float xMin = Mathf.Min(low.xMin, high.xMin);
            float xMax = Mathf.Max(low.xMax, high.xMax);
            float yMin = Mathf.Min(low.yMin, high.yMin);
            float yMax = Mathf.Max(low.yMax, high.yMax);
            Rect area = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            if (placement.room.halfCell)
                area = HalfRect(area, placement.rotation);
            Vector2 local = gridHost.WorldToLocal(new Vector2(area.xMin, area.yMin));
            var cover = new VisualElement { pickingMode = PickingMode.Ignore };
            cover.AddToClassList("room-cover");
            cover.style.position = Position.Absolute;
            cover.style.left = local.x;
            cover.style.top = local.y;
            cover.style.width = area.width;
            cover.style.height = area.height;
            cover.style.backgroundColor = placement.room.swatch;
            bool offline = ShipEvaluator.NeedsEnergy(placement.room) && (!report.crewed[index] || !report.online[index]);
            if (offline)
                cover.style.opacity = 0.45f;
            PaintPlateEdge(cover, placement.rotation);
            gridHost.Add(cover);
            cover.BringToFront();
            return true;
        }

        static void PaintPlateEdge(VisualElement plate, int rotation)
        {
            int turns = ((rotation % 4) + 4) % 4;
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.position = Position.Absolute;
            bar.style.backgroundColor = new Color(1f, 1f, 1f, 0.95f);
            if (turns == 0 || turns == 2)
            {
                bar.style.left = 0;
                bar.style.right = 0;
                bar.style.height = 6;
                if (turns == 0)
                    bar.style.top = 0;
                else
                    bar.style.bottom = 0;
            }
            else
            {
                bar.style.top = 0;
                bar.style.bottom = 0;
                bar.style.width = 6;
                if (turns == 3)
                    bar.style.left = 0;
                else
                    bar.style.right = 0;
            }

            plate.Add(bar);
        }

        static void ScheduleRoomLabels(PanelState state, VisualElement gridHost)
        {
            gridHost.schedule.Execute(() => StampRoomLabels(state, gridHost)).StartingIn(16);
        }

        static void StampRoomLabels(PanelState state, VisualElement gridHost)
        {
            if (state.stampingLabels || state.build == null || gridHost == null || gridHost.panel == null)
                return;
            state.stampingLabels = true;
            var stale = new List<VisualElement>();
            gridHost.Query(className: "room-label").ForEach(stale.Add);
            gridHost.Query(className: "room-cover").ForEach(stale.Add);
            for (int i = 0; i < stale.Count; i++)
                stale[i].RemoveFromHierarchy();

            bool waiting = false;
            ShipReport report = ShipEvaluator.Evaluate(state.build);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < state.build.rooms.Count; i++)
                {
                    if (i >= report.valid.Count || !report.valid[i])
                        continue;
                    RoomPlacement placement = state.build.rooms[i];
                    if (placement == null || placement.room == null)
                        continue;
                    bool half = placement.room.halfCell;
                    if (pass == 0 && half)
                        continue;
                    if (pass == 1 && !half)
                        continue;
                    if (!StampRoomCover(state, gridHost, placement, report, i))
                        waiting = true;
                }
            }

            for (int i = 0; i < state.build.rooms.Count; i++)
            {
                if (i >= report.valid.Count || !report.valid[i])
                    continue;
                RoomPlacement placement = state.build.rooms[i];
                if (placement == null || placement.room == null)
                    continue;
                CellRef origin = FindCell(state, placement.x, placement.y);
                CellRef far = FindCell(state, placement.x + placement.SpanX - 1, placement.y + placement.SpanY - 1);
                if (origin == null || far == null)
                    continue;
                Rect low = origin.element.worldBound;
                Rect high = far.element.worldBound;
                if (low.width <= 0f || high.width <= 0f)
                {
                    waiting = true;
                    continue;
                }

                float xMin = Mathf.Min(low.xMin, high.xMin);
                float xMax = Mathf.Max(low.xMax, high.xMax);
                float yMin = Mathf.Min(low.yMin, high.yMin);
                float yMax = Mathf.Max(low.yMax, high.yMax);
                Rect area = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                area = LabelArea(state.build, placement, area);
                Vector2 local = gridHost.WorldToLocal(new Vector2(area.xMin, area.yMin));
                var label = new Label(FitName(placement.room.displayName, area.width, area.height)) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("room-label");
                label.style.position = Position.Absolute;
                label.style.left = local.x;
                label.style.top = local.y;
                label.style.width = area.width;
                label.style.height = area.height;
                label.style.fontSize = TextSize;
                gridHost.Add(label);
                label.BringToFront();
            }

            UiFonts.Apply(gridHost);
            state.stampingLabels = false;
            if (waiting && !state.labelRetried)
            {
                state.labelRetried = true;
                gridHost.schedule.Execute(() => StampRoomLabels(state, gridHost)).StartingIn(32);
            }
        }

        static void FinishDrag(PanelState state, Vector2 pointer)
        {
            Drag drag = state.drag;
            if (drag == null)
                return;
            CellRef cell = CellUnder(state, pointer);
            RoomDef room = drag.room;
            bool moving = drag.moving;
            int originX = drag.originX;
            int originY = drag.originY;
            int grabX = drag.grabX;
            int grabY = drag.grabY;
            int rotation = drag.rotation;
            int originRotation = drag.originRotation;
            ClearDrag(state);

            if (!state.writable || state.build == null)
            {
                Bind(state.tree, state.build, state.flight, state.catalog, state.writable, state.beforeMutate);
                return;
            }

            if (cell != null && !cell.blocked)
            {
                DropRoom(state, room, moving, originX, originY, cell.x - grabX, cell.y - grabY, rotation, originRotation);
            }
            else if (moving && cell == null)
            {
                state.beforeMutate?.Invoke();
                ShipEdit.TryRemove(state.build, room, originX, originY);
                state.notice = "已拆掉 " + room.displayName;
            }

            Bind(state.tree, state.build, state.flight, state.catalog, state.writable, state.beforeMutate);
        }

        static void DropRoom(PanelState state, RoomDef room, bool moving, int originX, int originY, int placeX, int placeY, int rotation, int originRotation)
        {
            CellMap occupied = CellMap.Build(state.build);
            if (moving)
                ReleaseFootprint(state.build, room, originX, originY, occupied);

            var placement = new RoomPlacement { room = room, x = placeX, y = placeY, rotation = rotation };
            string error = null;
            if (!ShipEvaluator.Fits(state.build, placement, occupied, out error))
            {
                state.notice = error ?? "放不下。";
                return;
            }

            state.beforeMutate?.Invoke();
            if (moving && !ShipEdit.TryRemove(state.build, room, originX, originY))
            {
                state.notice = "没能挪走原来的舱室。";
                return;
            }

            if (!ShipEdit.TryPlace(state.build, room, placeX, placeY, rotation, out string placeError))
            {
                if (moving)
                    ShipEdit.TryPlace(state.build, room, originX, originY, originRotation, out _);
                state.notice = placeError ?? "放不下。";
                return;
            }

            state.notice = null;
        }

        static void PlaceGhost(PanelState state, Vector2 pointer)
        {
            Drag drag = state.drag;
            if (drag == null || drag.ghost == null)
                return;
            Vector2 local = state.board.WorldToLocal(pointer);
            float stride = state.cellStride;
            float left = local.x - (drag.grabX * stride + stride * 0.5f);
            float top = local.y - ((FootprintY(drag) - 1 - drag.grabY) * stride + stride * 0.5f);
            if (drag.room != null && drag.room.halfCell)
            {
                int turns = drag.rotation & 3;
                if (turns == 1)
                    left += stride * 0.5f;
                if (turns == 2)
                    top += stride * 0.5f;
            }

            drag.ghost.style.left = left;
            drag.ghost.style.top = top;
        }

        static void Highlight(PanelState state, Vector2 pointer)
        {
            Drag drag = state.drag;
            for (int i = 0; i < state.cells.Count; i++)
            {
                state.cells[i].element.RemoveFromClassList("cell-drop-ok");
                state.cells[i].element.RemoveFromClassList("cell-drop-bad");
                state.cells[i].element.RemoveFromClassList("cell-range");
            }

            CellRef cell = CellUnder(state, pointer);
            if (cell == null || cell.blocked || drag.room == null)
                return;

            int placeX = cell.x - drag.grabX;
            int placeY = cell.y - drag.grabY;
            CellMap occupied = CellMap.Build(state.build);
            if (drag.moving)
                ReleaseFootprint(state.build, drag.room, drag.originX, drag.originY, occupied);
            var placement = new RoomPlacement { room = drag.room, x = placeX, y = placeY, rotation = drag.rotation };
            bool fits = ShipEvaluator.Fits(state.build, placement, occupied, out _);
            string mark = fits ? "cell-drop-ok" : "cell-drop-bad";
            for (int y = 0; y < placement.SpanY; y++)
            {
                for (int x = 0; x < placement.SpanX; x++)
                {
                    CellRef covered = FindCell(state, placeX + x, placeY + y);
                    if (covered != null)
                        covered.element.AddToClassList(mark);
                }
            }

            if (drag.room.effectRange <= 0)
                return;
            if (drag.room.kind != RoomKind.Radiator && drag.room.kind != RoomKind.DamageControl)
                return;
            var rangeCells = new HashSet<int>();
            ShipEvaluator.CollectRange(state.build.hull, placement, rangeCells);
            for (int i = 0; i < state.cells.Count; i++)
            {
                CellRef candidate = state.cells[i];
                if (candidate.blocked)
                    continue;
                int key = candidate.y * state.build.hull.width + candidate.x;
                if (rangeCells.Contains(key))
                    candidate.element.AddToClassList("cell-range");
            }
        }

        static Vector2 PointerInPanel(PanelState state, Vector2 pointer)
        {
            if (state.tree == null)
                return pointer;
            if (state.tree.worldBound.Contains(pointer))
                return pointer;
            if (state.tree.panel == null)
                return pointer;
            Vector2 converted = RuntimePanelUtils.ScreenToPanel(state.tree.panel, pointer);
            if (state.tree.worldBound.Contains(converted))
                return converted;
            return pointer;
        }

        static CellRef CellUnder(PanelState state, Vector2 pointer, bool allowNearest = true)
        {
            CellRef nearest = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < state.cells.Count; i++)
            {
                Rect bound = state.cells[i].element.worldBound;
                if (bound.width <= 0f || bound.height <= 0f)
                    continue;
                if (bound.Contains(pointer))
                    return state.cells[i];
                float dx = pointer.x - bound.center.x;
                float dy = pointer.y - bound.center.y;
                float distance = dx * dx + dy * dy;
                float limit = bound.width * 0.75f;
                if (allowNearest && distance < nearestDistance && distance <= limit * limit)
                {
                    nearestDistance = distance;
                    nearest = state.cells[i];
                }
            }

            return nearest;
        }

        static CellRef FindCell(PanelState state, int x, int y)
        {
            for (int i = 0; i < state.cells.Count; i++)
            {
                CellRef cell = state.cells[i];
                if (cell.x == x && cell.y == y)
                    return cell;
            }

            return null;
        }

        static void ClearDrag(PanelState state)
        {
            if (state.drag == null)
                return;
            if (state.drag.ghost != null && state.drag.ghost.parent != null)
                state.drag.ghost.RemoveFromHierarchy();
            state.drag = null;
        }

        static void ReleaseFootprint(ShipBuild build, RoomDef room, int originX, int originY, CellMap occupied)
        {
            if (build == null || room == null || occupied == null)
                return;
            for (int i = 0; i < build.rooms.Count; i++)
            {
                RoomPlacement placement = build.rooms[i];
                if (placement == null || placement.room != room || placement.x != originX || placement.y != originY)
                    continue;
                Dictionary<int, int> slot = room.halfCell ? occupied.half : occupied.floor;
                for (int y = 0; y < placement.SpanY; y++)
                {
                    for (int x = 0; x < placement.SpanX; x++)
                    {
                        int key = (placement.y + y) * build.hull.width + (placement.x + x);
                        if (slot.TryGetValue(key, out int held) && held == i)
                            slot.Remove(key);
                    }
                }

                return;
            }
        }

        static List<RoomDef> CollectRooms(ShipBuild build)
        {
            var list = new List<RoomDef>();
            var seen = new HashSet<RoomDef>();
            for (int i = 0; i < build.rooms.Count; i++)
            {
                RoomDef room = build.rooms[i].room;
                if (room != null && seen.Add(room))
                    list.Add(room);
            }

            return list;
        }

        static bool AlarmNotice(string notice)
        {
            return !string.IsNullOrEmpty(notice) && notice.IndexOf("已拆掉") < 0;
        }

        static void FillStats(VisualElement host, ShipBuild build, ShipReport report, FlightTuning flight)
        {
            host.Clear();
            if (build == null || report == null)
            {
                AddStat(host, "把一份拼船方案放到这个面板上。", false);
                return;
            }

            for (int i = 0; i < report.errors.Count; i++)
                AddStat(host, report.errors[i], true);
            AddStat(host, build.name, false);
            AddStat(host, "质量    " + report.mass.ToString("0.#") + " 吨", false);
            AddStat(host, "推力    " + report.thrust.ToString("0.#"), false);
            AddStat(host, "供电    " + report.powerDraw.ToString("0.#") + " / " + report.powerOutput.ToString("0.#"), report.powerDraw > report.powerOutput + 0.01f);
            AddStat(host, "热容    " + report.heatCapacity.ToString("0.#"), false);
            AddStat(host, "产热    " + report.heatOutput.ToString("0.#") + " / 秒，未覆盖 " + report.heatUncovered.ToString("0.#"), report.heatUncovered > 0.01f);
            AddStat(host, "散热    " + report.heatDissipation.ToString("0.#") + " / 秒，覆盖 " + report.heatCovered + " 舱", false);
            AddStat(host, "武器    " + report.weapons, false);
            AddStat(host, "导弹    " + report.missiles, false);
            AddStat(host, "通道    " + report.corridors, false);
            AddStat(host, "气阀    " + report.valves, false);
            AddStat(host, "损管    覆盖 " + report.damageCovered + " 舱", false);
            if (report.missiles > 0)
                AddStat(host, "追踪    " + report.homingTurnRate.ToString("0") + " 度/秒", false);
            if (flight == null)
                return;
            float accel = flight.Acceleration(report.thrust, report.mass);
            AddStat(host, "", false);
            AddStat(host, "加速度  " + accel.ToString("0.#") + " 米/秒²", false);
            float adjust = 0f;
            float adjustWeight = 0f;
            for (int i = 0; i < build.rooms.Count; i++)
            {
                if (i >= report.online.Count || !report.online[i])
                    continue;
                RoomDef room = build.rooms[i].room;
                if (room == null || room.thrust <= 0f || room.speedAdjustTime <= 0.01f)
                    continue;
                adjust += room.speedAdjustTime * room.thrust;
                adjustWeight += room.thrust;
            }

            if (adjustWeight > 0.01f)
            {
                float seconds = adjust / adjustWeight;
                float rated = accel * seconds;
                AddStat(host, "变速    " + seconds.ToString("0.#") + " 秒    满出力 " + rated.ToString("0") + " 米/秒", false);
            }
            HullDef hull = build != null ? build.hull : null;
            float turnRate = hull != null && hull.turnRateAtRest > 0.01f ? hull.turnRateAtRest : 20f;
            float inertia = hull != null ? hull.turnInertia : 0.4f;
            float speedInertia = hull != null ? hull.speedInertia : 0f;
            float side = hull != null ? hull.sideThrust : 0f;
            AddStat(host, "侧推    左 " + side.ToString("0.#") + "    右 " + side.ToString("0.#"), false);
            AddStat(host, "转向    " + turnRate.ToString("0") + " 度/秒", false);
            AddStat(host, "惯性    " + inertia.ToString("0.##") + " 秒", false);
            AddStat(host, "速度惯性  " + speedInertia.ToString("0.###") + " 秒/米每秒", false);
            AddStat(host, "跟转    " + flight.velocityFollowTurn.ToString("0.##"), false);
        }

        static void AddStat(VisualElement host, string text, bool bad)
        {
            var line = new Label(text);
            line.AddToClassList("stat-line");
            if (bad)
                line.AddToClassList("text-bad");
            host.Add(line);
        }

        static int PickRoom(PanelState state, CellRef tag, Vector2 pointer)
        {
            if (tag.halfIndex >= 0 && tag.floorIndex >= 0)
            {
                int rotation = state.build.rooms[tag.halfIndex].rotation;
                return PointerInHalf(tag.element, pointer, rotation) ? tag.halfIndex : tag.floorIndex;
            }

            if (tag.halfIndex >= 0)
                return tag.halfIndex;
            return tag.floorIndex;
        }

        static string RoomTip(ShipBuild build, ShipReport report, int floorIndex, int halfIndex)
        {
            string tip = "";
            if (floorIndex >= 0)
                tip = StatusTip(build.rooms[floorIndex], report, floorIndex);
            if (halfIndex >= 0)
            {
                string half = StatusTip(build.rooms[halfIndex], report, halfIndex);
                tip = string.IsNullOrEmpty(tip) ? half : tip + "\n" + half;
            }

            return tip;
        }

        static string StatusTip(RoomPlacement placement, ShipReport report, int index)
        {
            string tip = placement.room.displayName;
            if (ShipEvaluator.NeedsEnergy(placement.room) && !report.crewed[index])
                tip += "（没有能源）";
            else if (ShipEvaluator.NeedsEnergy(placement.room) && !report.online[index])
                tip += "（断电）";
            if (report.cooled[index])
                tip += "（散热）";
            else if (placement.room.heatOutput > 0f && report.online[index])
                tip += "（产热没盖住）";
            if (report.salvaged[index])
                tip += "（损管）";
            return tip;
        }

        static void RotatePlaced(PanelState state, Vector2 pointer)
        {
            if (!state.writable || state.build == null || state.drag != null || !AcceptRotate(state))
                return;
            Vector2 point = PointerInPanel(state, pointer);
            CellRef cell = CellUnder(state, point, false);
            if (cell == null || cell.blocked)
                return;
            int index = PickRoom(state, cell, point);
            if (index < 0 || index >= state.build.rooms.Count)
                return;
            RoomPlacement placement = state.build.rooms[index];
            if (placement == null || placement.room == null)
                return;

            int grabX = cell.x - placement.x;
            int grabY = cell.y - placement.y;
            int oldWidth = placement.SpanX;
            int rotation = (placement.rotation + 1) & 3;
            int newGrabX = grabY;
            int newGrabY = oldWidth - 1 - grabX;
            int spanX = (rotation & 1) == 1 ? placement.room.height : placement.room.width;
            int spanY = (rotation & 1) == 1 ? placement.room.width : placement.room.height;
            newGrabX = Mathf.Clamp(newGrabX, 0, Mathf.Max(0, spanX - 1));
            newGrabY = Mathf.Clamp(newGrabY, 0, Mathf.Max(0, spanY - 1));
            int placeX = cell.x - newGrabX;
            int placeY = cell.y - newGrabY;

            CellMap occupied = CellMap.Build(state.build);
            ReleaseFootprint(state.build, placement.room, placement.x, placement.y, occupied);
            var trial = new RoomPlacement { room = placement.room, x = placeX, y = placeY, rotation = rotation };
            if (!ShipEvaluator.Fits(state.build, trial, occupied, out string error))
            {
                state.notice = error ?? "放不下。";
                Bind(state.tree, state.build, state.flight, state.catalog, state.writable, state.beforeMutate);
                return;
            }

            state.beforeMutate?.Invoke();
            placement.x = placeX;
            placement.y = placeY;
            placement.rotation = rotation;
            state.notice = null;
            Bind(state.tree, state.build, state.flight, state.catalog, state.writable, state.beforeMutate);
        }

        static bool PointerInHalf(VisualElement cell, Vector2 pointer, int rotation)
        {
            Rect bound = cell.worldBound;
            if (bound.width <= 0f || bound.height <= 0f)
                return false;
            float u = (pointer.x - bound.xMin) / bound.width;
            float v = (pointer.y - bound.yMin) / bound.height;
            int turns = ((rotation % 4) + 4) % 4;
            if (turns == 0)
                return v < 0.5f;
            if (turns == 1)
                return u >= 0.5f;
            if (turns == 2)
                return v >= 0.5f;
            return u < 0.5f;
        }

        static Rect LabelArea(ShipBuild build, RoomPlacement placement, Rect area)
        {
            if (placement.room.halfCell)
                return HalfRect(area, placement.rotation);
            if (placement.room.kind != RoomKind.Corridor || placement.SpanX != 1 || placement.SpanY != 1)
                return area;
            for (int i = 0; i < build.rooms.Count; i++)
            {
                RoomPlacement other = build.rooms[i];
                if (other == null || other.room == null || !other.room.halfCell)
                    continue;
                if (other.x == placement.x && other.y == placement.y)
                    return HalfRect(area, (other.rotation + 2) % 4);
            }

            return area;
        }

        static Rect HalfRect(Rect area, int rotation)
        {
            int turns = ((rotation % 4) + 4) % 4;
            if (turns == 0)
                return new Rect(area.xMin, area.yMin, area.width, area.height * 0.5f);
            if (turns == 1)
                return new Rect(area.xMin + area.width * 0.5f, area.yMin, area.width * 0.5f, area.height);
            if (turns == 2)
                return new Rect(area.xMin, area.yMin + area.height * 0.5f, area.width, area.height * 0.5f);
            return new Rect(area.xMin, area.yMin, area.width * 0.5f, area.height);
        }

        static string FitName(string name, float width, float height)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            if (height >= TextSize && width >= name.Length * TextSize)
                return name;
            return name.Substring(0, 1);
        }
    }
}
