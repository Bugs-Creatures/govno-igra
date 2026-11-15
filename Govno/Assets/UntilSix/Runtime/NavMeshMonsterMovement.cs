using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshMonsterMovement : MonsterMovement
    {
        private const float DoorOpeningLeadTime = 0.45f;
        private const float DoorBendLeadTime = 0.60f;
        private const float DoorBendTransitionTime = 0.45f;
        private const float WaypointTolerance = 0.07f;
        private const float PlayerProjectionRadius = 1.8f;
        private const float LampProjectionRadius = 1.35f;

        [SerializeField, Min(0.1f)] private float walkSpeed = 0.90f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 2.55f;

        private NavMeshAgent agent;
        private MonsterDoorway[] doorways;
        private MonsterRoute planner;
        private readonly List<Vector3> route = new List<Vector3>();
        private int routeIndex;
        private bool lastChasing;
        private Vector3 lastDestination;
        private float nextRepathAt;
        private bool hasDestination;

        public float CrouchAmount { get; private set; }
        public float WalkSpeed => walkSpeed;
        public float TravelSpeed => agent != null && agent.isOnNavMesh && !agent.isStopped
            ? Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude
            : 0f;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            doorways = FindObjectsByType<MonsterDoorway>();
            planner = new MonsterRoute(agent, doorways, FindObjectsByType<MonsterCorridorWaypoint>());
        }

        private void OnDisable() => Stop();

        public override MoveResult MoveTowards(Vector3 destination, float deltaTime) =>
            Move(destination, chasing: false, stoppingDistance: 0.18f);

        public override MoveResult ChaseTowards(Vector3 destination, float deltaTime) =>
            Move(destination, chasing: true, stoppingDistance: 0.55f);

        private void Update()
        {
            Vector3 position = DoorProbePosition;
            Vector3 towardsCorner = hasDestination && agent.isOnNavMesh && !agent.isStopped
                ? agent.steeringTarget - transform.position
                : Vector3.zero;
            float bendLead = Mathf.Max(0.8f, agent.speed * DoorBendLeadTime);
            float lookAhead = Mathf.Min(towardsCorner.magnitude, bendLead);
            Vector3 direction = towardsCorner.normalized;
            float targetBend = 0f;
            foreach (MonsterDoorway doorway in doorways)
            {
                if (doorway != null && doorway.isActiveAndEnabled &&
                    doorway.TryGetEntryDistance(position, direction, lookAhead, out float entry))
                    targetBend = Mathf.Max(targetBend, Mathf.SmoothStep(1f, 0f, entry / bendLead));
            }

            CrouchAmount = Mathf.MoveTowards(CrouchAmount, targetBend, Time.deltaTime / DoorBendTransitionTime);
        }

        private Vector3 DoorProbePosition => transform.position + Vector3.up * (0.45f - agent.baseOffset);

        public override void Stop()
        {
            hasDestination = false;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        private MoveResult Move(Vector3 destination, bool chasing, float stoppingDistance)
        {
            if (!agent.isOnNavMesh)
                return MoveResult.Unreachable;

            agent.speed = chasing ? chaseSpeed : walkSpeed;
            bool destinationMoved = (destination - lastDestination).sqrMagnitude > 0.04f;
            if (!hasDestination || lastChasing != chasing || (destinationMoved && Time.time >= nextRepathAt))
            {
                routeIndex = 0;
                float projectionRadius = chasing ? PlayerProjectionRadius : LampProjectionRadius;
                if (!planner.Build(agent.nextPosition, destination, !chasing, projectionRadius, route) ||
                    !SetRouteDestination(stoppingDistance))
                {
                    Stop();
                    return MoveResult.Unreachable;
                }

                hasDestination = true;
                lastChasing = chasing;
                lastDestination = destination;
                nextRepathAt = Time.time + 0.15f;
            }

            if (agent.pathPending)
                return MoveResult.Moving;
            if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            {
                Stop();
                return MoveResult.Unreachable;
            }

            agent.isStopped = false;
            bool intermediate = routeIndex < route.Count - 1;
            if (intermediate && agent.remainingDistance <= WaypointTolerance)
            {
                routeIndex++;
                if (!SetRouteDestination(stoppingDistance))
                {
                    Stop();
                    return MoveResult.Unreachable;
                }

                return MoveResult.Moving;
            }

            Vector3 towardsCorner = agent.steeringTarget - transform.position;
            float lookAhead = Mathf.Min(towardsCorner.magnitude, agent.speed * DoorOpeningLeadTime);
            foreach (MonsterDoorway doorway in doorways)
            {
                if (doorway != null && doorway.isActiveAndEnabled)
                    doorway.OpenAlong(DoorProbePosition, towardsCorner.normalized, lookAhead);
            }

            return !intermediate && agent.remainingDistance <= stoppingDistance + 0.05f
                ? MoveResult.Arrived
                : MoveResult.Moving;
        }

        private bool SetRouteDestination(float stoppingDistance)
        {
            bool intermediate = routeIndex < route.Count - 1;
            agent.stoppingDistance = intermediate ? 0f : stoppingDistance;
            agent.autoBraking = !intermediate;
            return agent.SetDestination(route[routeIndex]);
        }
    }
}
