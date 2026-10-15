using UnityEngine;
using UnityEngine.UIElements;

namespace SpaceShip
{
    [RequireComponent(typeof(UIDocument))]
    public class CampaignMapController : MonoBehaviour
    {
        [Tooltip("已经生成的星图快照。播放时只读这份，不会按种子重做。")]
        public WorldSnapshot snapshot;

        [Tooltip("进入战斗时用的战场。飞行手感、玩家船和自动驾驶都从这里来。")]
        public CombatArenaDef arena;

        CampaignTravel travel;
        CampaignMapView view;
        CombatSim sim;
        bool fighting;
        int gear;
        int gearHold;
        float gearWait;

        void Start()
        {
            UIDocument document = GetComponent<UIDocument>();
            UiFonts.Install(document);
            travel = new CampaignTravel();
            FlightTuning flight = arena != null ? arena.flight : null;
            ShipBuild ship = arena != null ? arena.playerBuild : null;
            travel.Begin(snapshot, flight, ship);
            view = new CampaignMapView(document.rootVisualElement, travel, false, null, null, null, EnterCombat);
            view.Live();
        }

        void Update()
        {
            if (fighting)
            {
                StepCombat();
                return;
            }

            if (travel == null || view == null)
                return;
            travel.Tick(Time.deltaTime);
            view.Live();
        }

        void EnterCombat()
        {
            UIDocument document = GetComponent<UIDocument>();
            if (fighting || document == null || travel == null)
                return;
            if (arena == null || arena.playerBuild == null || arena.enemyBuild == null)
            {
                travel.status = "没有战场文档。常规引擎速度仍按飞行手感估算，战斗场景没接上。";
                travel.revision++;
                view.Live();
                return;
            }

            travel.EnterConventional();
            sim = CombatSim.Start(arena);
            fighting = true;
            gear = 0;
            gearHold = 0;
            view.Hide();
            CombatView.Open(document.rootVisualElement, sim, ExitCombat, "返回星图");
        }

        void ExitCombat()
        {
            fighting = false;
            gear = 0;
            gearHold = 0;
            sim = null;
            CombatView.Close();
            if (travel != null)
                travel.ExitConventional();
            if (view != null)
                view.Show();
        }

        void StepCombat()
        {
            if (sim == null)
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
                sim.StepOrdered(Time.deltaTime);

            CombatView.PollInput();
            CombatView.Sync(sim);
        }
    }
}
