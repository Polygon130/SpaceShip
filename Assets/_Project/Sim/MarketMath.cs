using UnityEngine;

namespace SpaceShip
{
    public static class MarketMath
    {
        public const float MinFactor = 0.05f;

        public static float StockPressure(float stock, float capacity, float target)
        {
            if (capacity <= 0.01f)
                return 1f;
            float fill = Mathf.Clamp01(stock / capacity);
            float aim = Mathf.Clamp01(target / capacity);
            return Mathf.Max(MinFactor, 1f + (aim - fill));
        }

        public static float PhaseFactor(float angleRadians)
        {
            return 1f + 0.05f * Mathf.Sin(angleRadians);
        }

        public static float Price(float basePrice, float stockPressure, float tax, float eventMul, float phaseMul)
        {
            return Mathf.Max(0f, basePrice)
                * Mathf.Max(MinFactor, stockPressure)
                * Mathf.Max(MinFactor, tax)
                * Mathf.Max(MinFactor, eventMul)
                * Mathf.Max(MinFactor, phaseMul);
        }

        public static float OrbitAngle(float phaseTurns, float periodSeconds, float time)
        {
            float turns = phaseTurns;
            if (periodSeconds > 0.01f)
                turns += time / periodSeconds;
            return turns * Mathf.PI * 2f;
        }

        public static Vector2 OrbitOffset(float phaseTurns, float periodSeconds, float radius, float time)
        {
            float angle = OrbitAngle(phaseTurns, periodSeconds, time);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        public static float Quote(GalaxyRules rules, StockLine line, float tax, RouteKind route, float phaseAngle)
        {
            if (line == null || line.good == null)
                return 0f;
            float pressure = StockPressure(line.stock, line.capacity, line.target);
            float phase = PhaseFactor(phaseAngle);
            float routeTax = RouteDef.TaxMultiplier(rules, route);
            float events = line.eventMultiplier <= 0f ? 1f : line.eventMultiplier;
            return Price(line.good.basePrice, pressure, tax * routeTax, events, phase);
        }
    }
}
