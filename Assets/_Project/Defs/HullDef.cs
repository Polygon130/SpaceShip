using UnityEngine;

namespace SpaceShip
{
    [CreateAssetMenu(menuName = "星际商船/船体", fileName = "船体")]
    public class HullDef : ScriptableObject
    {
        [Tooltip("面板和星图上显示的名字。")]
        public string displayName = "船体";

        [Tooltip("格子宽度。x 从左到右，从 0 计。")]
        public int width = 10;

        [Tooltip("格子高度。y 从下到上，从 0 计。面板上 y 较大的一行画在上面。")]
        public int height = 6;

        [Tooltip("空船质量，吨。舱室质量另外相加。")]
        public float baseMass = 40f;

        [Tooltip("空船结构。日常战斗先打舱室，结构只在多舱打穿后才走向解体。")]
        public float baseStructure = 200f;

        [Tooltip("按住转向时的船头角速度，度/秒。这是这具船体的最快转速，和船的质量、航速无关。")]
        public float turnRateAtRest = 20f;

        [Tooltip("转向惯性，秒。从静止加到满转、以及松开后停下来，各用这么长时间。0 是立刻跟上。战斗时间流速会把这段时间一并放慢。")]
        public float turnInertia = 0.4f;

        [Tooltip("速度带来的转向惯性，秒 /（米/秒）。航速每多 1 米/秒，转向惯性就再多这么多秒。0 是航速不影响转向。")]
        public float speedInertia = 0f;

        [Tooltip("左右各一台侧向推进器的推力。不是舱室，跟着船体走。按住时沿船的左右平移。0 是没有侧推。")]
        public float sideThrust = 16f;

        [Tooltip("没放下舱室的空格子按这个舱计算质量和装甲。穿甲、伤害和爆炸都会打到这些格子。")]
        public RoomDef defaultCabin;

        [HideInInspector]
        [Tooltip("true 表示这一格被切掉，不能放舱。下标 = y * 宽 + x。")]
        public bool[] blocked = System.Array.Empty<bool>();

        [HideInInspector] public int blockedWidth;
        [HideInInspector] public int blockedHeight;

        void OnValidate()
        {
            RemapBlocked(Mathf.Max(1, width), Mathf.Max(1, height));
        }

        public bool IsOpen(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return false;
            int index = y * width + x;
            if (blocked == null || index < 0 || index >= blocked.Length)
                return true;
            return !blocked[index];
        }

        public void RemapBlocked(int newWidth, int newHeight)
        {
            newWidth = Mathf.Max(1, newWidth);
            newHeight = Mathf.Max(1, newHeight);
            if (blocked != null
                && blockedWidth == newWidth
                && blockedHeight == newHeight
                && width == newWidth
                && height == newHeight
                && blocked.Length == newWidth * newHeight)
                return;
            int oldWidth = blockedWidth > 0 ? blockedWidth : newWidth;
            int oldHeight = blockedHeight > 0 ? blockedHeight : newHeight;
            var next = new bool[newWidth * newHeight];
            if (blocked != null)
            {
                int copyHeight = Mathf.Min(oldHeight, newHeight);
                int copyWidth = Mathf.Min(oldWidth, newWidth);
                for (int y = 0; y < copyHeight; y++)
                {
                    for (int x = 0; x < copyWidth; x++)
                    {
                        int oldIndex = y * oldWidth + x;
                        if (oldIndex >= 0 && oldIndex < blocked.Length)
                            next[y * newWidth + x] = blocked[oldIndex];
                    }
                }
            }

            width = newWidth;
            height = newHeight;
            blocked = next;
            blockedWidth = newWidth;
            blockedHeight = newHeight;
        }
    }
}
