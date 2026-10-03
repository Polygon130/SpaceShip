using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    [Serializable]
    public class RoomPlacement
    {
        public RoomDef room;
        public int x;
        public int y;

        [Tooltip("顺时针转过的直角数，0 到 3。奇数会交换宽高。")]
        public int rotation;

        public int SpanX
        {
            get
            {
                if (room == null)
                    return 1;
                int turns = ((rotation % 4) + 4) % 4;
                return (turns & 1) == 1 ? room.height : room.width;
            }
        }

        public int SpanY
        {
            get
            {
                if (room == null)
                    return 1;
                int turns = ((rotation % 4) + 4) % 4;
                return (turns & 1) == 1 ? room.width : room.height;
            }
        }
    }

    [CreateAssetMenu(menuName = "星际商船/拼船方案", fileName = "拼船方案")]
    public class ShipBuild : ScriptableObject
    {
        [Tooltip("使用的船体文档。")]
        public HullDef hull;

        [Tooltip("已放下的舱。供电按这个列表的顺序分配。")]
        public List<RoomPlacement> rooms = new List<RoomPlacement>();
    }
}
