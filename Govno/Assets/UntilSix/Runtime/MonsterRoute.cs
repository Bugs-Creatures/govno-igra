using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UntilSix.Monster
{
    internal sealed class MonsterRoute
    {
        private readonly NavMeshQueryFilter filter;
        private readonly MonsterDoorway[] doors;
        private readonly MonsterCorridorWaypoint[] corridors;
        private readonly NavMeshPath path = new NavMeshPath();
        private readonly List<Vector3> corridorStops = new List<Vector3>();
        private readonly List<Crossing> crossings = new List<Crossing>();
        private readonly HashSet<MonsterDoorway> usedDoors = new HashSet<MonsterDoorway>();

        public MonsterRoute(NavMeshAgent agent, MonsterDoorway[] doors, MonsterCorridorWaypoint[] corridors)
        {
            filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            this.doors = doors;
            this.corridors = corridors;
        }

        public bool Build(Vector3 start, Vector3 goal, bool useCorridors, float projectionRadius, List<Vector3> route)
        {
            route.Clear();
            if (!Project(goal, projectionRadius, out goal) || !Calculate(start, goal))
                return false;

            Vector3 origin = start;
            corridorStops.Clear();
            if (useCorridors && path.corners.Length > 1)
                AddCorridorStops(path.corners, goal);
            corridorStops.Add(goal);

            foreach (Vector3 stop in corridorStops)
            {
                if (!Calculate(start, stop))
                    continue;

                AddDoorStops(path.corners, route);
                route.Add(stop);
                start = stop;
            }

            // A guide is optional if the baked mesh cannot connect it to the goal.
            if (route.Count > 0 && route[route.Count - 1] == goal)
                return true;

            route.Clear();
            if (!Calculate(origin, goal))
                return false;
            AddDoorStops(path.corners, route);
            route.Add(goal);
            return true;
        }

        private void AddCorridorStops(Vector3[] corners, Vector3 goal)
        {
            Vector3 start = corners[0];
            for (int i = 1; i < corners.Length; i++)
            {
                crossings.Clear();
                foreach (MonsterCorridorWaypoint waypoint in corridors)
                {
                    if (waypoint != null && waypoint.isActiveAndEnabled &&
                        waypoint.Crosses(corners[i - 1], corners[i], out float fraction))
                        crossings.Add(new Crossing(fraction, waypoint.transform.position));
                }

                crossings.Sort(Crossing.Compare);
                foreach (Crossing crossing in crossings)
                {
                    if (Project(crossing.Point, 0.3f, out Vector3 point) &&
                        (point - start).sqrMagnitude >= 0.35f * 0.35f &&
                        (point - goal).sqrMagnitude >= 0.4f * 0.4f)
                    {
                        corridorStops.Add(point);
                        start = point;
                    }
                }
            }
        }

        private void AddDoorStops(Vector3[] corners, List<Vector3> route)
        {
            usedDoors.Clear();
            for (int i = 1; i < corners.Length; i++)
            {
                crossings.Clear();
                foreach (MonsterDoorway door in doors)
                {
                    if (door != null && door.isActiveAndEnabled &&
                        door.TryGetCrossing(corners[i - 1], corners[i], out float fraction) && usedDoors.Add(door))
                        crossings.Add(new Crossing(fraction, door.Threshold));
                }

                crossings.Sort(Crossing.Compare);
                foreach (Crossing crossing in crossings)
                {
                    if (Project(crossing.Point, 0.25f, out Vector3 point) &&
                        (point - corners[0]).sqrMagnitude > 0.06f * 0.06f &&
                        (point - corners[corners.Length - 1]).sqrMagnitude > 0.06f * 0.06f)
                        route.Add(point);
                }
            }
        }

        private bool Calculate(Vector3 start, Vector3 end) =>
            NavMesh.CalculatePath(start, end, filter, path) && path.status == NavMeshPathStatus.PathComplete;

        private bool Project(Vector3 point, float radius, out Vector3 projected)
        {
            bool found = NavMesh.SamplePosition(point, out NavMeshHit hit, radius, filter);
            projected = hit.position;
            return found;
        }

        private readonly struct Crossing
        {
            public readonly float Fraction;
            public readonly Vector3 Point;

            public Crossing(float fraction, Vector3 point)
            {
                Fraction = fraction;
                Point = point;
            }

            public static int Compare(Crossing first, Crossing second) => first.Fraction.CompareTo(second.Fraction);
        }
    }
}
