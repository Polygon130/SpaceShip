using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceShip
{
    [Serializable]
    public class StarRecord
    {
        public string id;
        public string displayName;
        public Vector2 position;
        public bool factionCore;
        public string factionId;
        public bool unlocked;
        public bool landable;
    }

    [Serializable]
    public class PlanetRecord
    {
        public string id;
        public string starId;
        public string displayName;
        public string factionId;
        public GravityWellDef well;
        public float orbitRadius;
        public float orbitPeriod;
        public float orbitPhase;
        public PlanetRole role;
        public bool hasStation;
    }

    [Serializable]
    public class StockLine
    {
        public GoodDef good;
        public float stock = 50f;
        public float capacity = 100f;
        public float target = 50f;
        public float eventMultiplier = 1f;
    }

    [Serializable]
    public class StationRecord
    {
        public string id;
        public string displayName;
        public string starId;
        public string planetId;
        public bool stellar;
        public List<StockLine> stock = new List<StockLine>();
    }

    [Serializable]
    public class RouteRecord
    {
        public string id;
        public RouteKind kind;
        public string fromStarId;
        public string toStarId;
        public bool discovered;
    }

    [Serializable]
    public class ChangeEntry
    {
        public int order;
        public string summary;
    }

    [CreateAssetMenu(menuName = "星际商船/星图快照", fileName = "星图快照")]
    public class WorldSnapshot : ScriptableObject
    {
        [Tooltip("生成时从规则抄下的种子。改规则不会改这里。")]
        public int seed;

        [Tooltip("规则版本。和当前规则不一致时，快照仍保持原样。")]
        public int rulesVersion;

        [Tooltip("勾上之后，打开快照不会重新生成。")]
        public bool generated;

        [Tooltip("生成这份快照时用的规则。")]
        public GalaxyRules rules;

        public string startStarId;
        public string fingerprint;
        public string summary;

        public List<StarRecord> stars = new List<StarRecord>();
        public List<PlanetRecord> planets = new List<PlanetRecord>();
        public List<StationRecord> stations = new List<StationRecord>();
        public List<RouteRecord> routes = new List<RouteRecord>();
        public List<ChangeEntry> changelog = new List<ChangeEntry>();
        public List<string> audit = new List<string>();

        public void AppendChange(string text)
        {
            if (changelog == null)
                changelog = new List<ChangeEntry>();
            changelog.Add(new ChangeEntry { order = changelog.Count + 1, summary = text });
        }

        public StarRecord FindStar(string id)
        {
            if (stars == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i] != null && stars[i].id == id)
                    return stars[i];
            }

            return null;
        }

        public PlanetRecord FindPlanet(string id)
        {
            if (planets == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < planets.Count; i++)
            {
                if (planets[i] != null && planets[i].id == id)
                    return planets[i];
            }

            return null;
        }

        public StationRecord FindStation(string id)
        {
            if (stations == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] != null && stations[i].id == id)
                    return stations[i];
            }

            return null;
        }

        public StationRecord StellarStation(string starId)
        {
            if (stations == null)
                return null;
            for (int i = 0; i < stations.Count; i++)
            {
                StationRecord station = stations[i];
                if (station != null && station.stellar && station.starId == starId)
                    return station;
            }

            return null;
        }

        public StationRecord PlanetStation(string planetId)
        {
            if (stations == null)
                return null;
            for (int i = 0; i < stations.Count; i++)
            {
                StationRecord station = stations[i];
                if (station != null && !station.stellar && station.planetId == planetId)
                    return station;
            }

            return null;
        }

        public int CountPlanets(string starId, List<PlanetRecord> into)
        {
            if (into != null)
                into.Clear();
            int count = 0;
            if (planets == null)
                return 0;
            for (int i = 0; i < planets.Count; i++)
            {
                PlanetRecord planet = planets[i];
                if (planet == null || planet.starId != starId)
                    continue;
                count++;
                if (into != null)
                    into.Add(planet);
            }

            return count;
        }

        public RouteRecord FindRoute(string id)
        {
            if (routes == null || string.IsNullOrEmpty(id))
                return null;
            for (int i = 0; i < routes.Count; i++)
            {
                if (routes[i] != null && routes[i].id == id)
                    return routes[i];
            }

            return null;
        }

        public bool RouteTouches(RouteRecord route, string starId)
        {
            return route != null && (route.fromStarId == starId || route.toStarId == starId);
        }

        public string OtherEnd(RouteRecord route, string starId)
        {
            if (route == null)
                return "";
            if (route.fromStarId == starId)
                return route.toStarId;
            if (route.toStarId == starId)
                return route.fromStarId;
            return "";
        }
    }
}
