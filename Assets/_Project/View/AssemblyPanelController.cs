using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip
{
    [RequireComponent(typeof(UIDocument))]
    public class AssemblyPanelController : MonoBehaviour
    {
        [Tooltip("这场战斗的数据文档。播放时拖拽修改其中的玩家拼船方案。")]
        public CombatArenaDef arena;

        void Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            UiFonts.Install(document);
            if (arena == null || arena.playerBuild == null)
                return;
            AssemblyPanelView.Bind(
                document.rootVisualElement,
                arena.playerBuild,
                arena.flight,
                Palette(),
                true,
                MarkDirty);
            Button fight = document.rootVisualElement.Q<Button>("fight");
            if (fight != null)
                fight.clicked += StartFight;
        }

        CombatSim sim;
        bool fighting;
        int gear;
        int gearHold;
        float gearWait;

        void Update()
        {
            if (!fighting || sim == null)
                return;
            float turn = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                turn -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                turn += 1f;
            float strafe = 0f;
            if (Input.GetKey(KeyCode.Q))
                strafe -= 1f;
            if (Input.GetKey(KeyCode.E))
                strafe += 1f;
            int span = sim.flight != null && sim.flight.gearCount > 0 ? sim.flight.gearCount : 4;
            float repeat = sim.flight != null && sim.flight.gearRepeat > 0.05f ? sim.flight.gearRepeat : 0.5f;
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            int want = 0;
            if (up && !down)
                want = 1;
            else if (down && !up)
                want = -1;
            bool stick = want != 0 || Mathf.Abs(turn) > 0.01f || Mathf.Abs(strafe) > 0.01f;
            if (stick && sim.order != ShipOrder.Manual)
            {
                sim.ReleaseMoveOrder();
                gear = 0;
                gearHold = 0;
            }

            if (want == 0)
                gearHold = 0;
            else if (want != gearHold)
            {
                gearHold = want;
                gear = Mathf.Clamp(gear + want, 0, span);
                gearWait = repeat;
            }
            else
            {
                gearWait -= Time.deltaTime;
                if (gearWait <= 0f)
                {
                    gear = Mathf.Clamp(gear + want, 0, span);
                    gearWait = repeat;
                }
            }

            if (sim.order == ShipOrder.Manual)
            {
                sim.gear = gear;
                sim.gearSpan = span;
                float thrustLevel = gear > 0 ? gear / (float)span : 0f;
                sim.Step(Time.deltaTime, thrustLevel, 0f, turn, strafe);
            }
            else
            {
                sim.StepOrdered(Time.deltaTime);
            }

            CombatView.PollInput();
            CombatView.Sync(sim);
        }

        void StartFight()
        {
            UIDocument document = GetComponent<UIDocument>();
            if (fighting || arena == null || arena.playerBuild == null || arena.enemyBuild == null || document == null)
                return;
            sim = CombatSim.Start(arena);
            fighting = true;
            gear = 0;
            gearHold = 0;
            VisualElement assembly = document.rootVisualElement.Q("assembly-root");
            if (assembly != null)
                assembly.style.display = DisplayStyle.None;
            CombatView.Open(document.rootVisualElement, sim, ReturnToYard);
        }

        void ReturnToYard()
        {
            UIDocument document = GetComponent<UIDocument>();
            fighting = false;
            gear = 0;
            gearHold = 0;
            sim = null;
            CombatView.Close();
            if (document == null)
                return;
            VisualElement assembly = document.rootVisualElement.Q("assembly-root");
            if (assembly != null)
                assembly.style.display = DisplayStyle.Flex;
            AssemblyPanelView.Bind(
                document.rootVisualElement,
                arena.playerBuild,
                arena.flight,
                Palette(),
                true,
                MarkDirty);
        }

        List<RoomDef> Palette()
        {
            if (arena.palette != null && arena.palette.Count > 0)
                return arena.palette;
            return null;
        }

        void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(arena.playerBuild, "拼船");
            UnityEditor.EditorUtility.SetDirty(arena.playerBuild);
#endif
        }
    }
}
