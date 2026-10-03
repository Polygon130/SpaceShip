namespace SpaceShip
{
    public static class ShipEdit
    {
        public static bool TryPlace(ShipBuild build, RoomDef room, int x, int y, int rotation, out string error)
        {
            error = null;
            if (build == null || build.hull == null)
            {
                error = "没有船体。";
                return false;
            }

            if (room == null)
            {
                error = "先在左侧选一个舱室。";
                return false;
            }

            var placement = new RoomPlacement { room = room, x = x, y = y, rotation = rotation };
            if (!ShipEvaluator.Fits(build, placement, CellMap.Build(build), out error))
                return false;

            build.rooms.Add(placement);
            return true;
        }

        public static bool TryRemove(ShipBuild build, RoomDef room, int x, int y)
        {
            if (build == null || room == null)
                return false;
            for (int i = 0; i < build.rooms.Count; i++)
            {
                RoomPlacement placement = build.rooms[i];
                if (placement == null || placement.room != room || placement.x != x || placement.y != y)
                    continue;
                build.rooms.RemoveAt(i);
                return true;
            }

            return false;
        }
    }
}
