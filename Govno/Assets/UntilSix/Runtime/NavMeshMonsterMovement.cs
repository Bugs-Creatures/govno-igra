using UnityEngine;
using UnityEngine.AI;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshMonsterMovement : MonsterMovement
    {
        private const float DoorLookAhead = 0.65f;
        private const float FootClearance = 0.1f;

        [SerializeField, Min(0.1f)] private float walkSpeed = 2.3f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4.8f;

        private readonly RaycastHit[] doorHits = new RaycastHit[16];
        private NavMeshAgent agent;
        private CottageDoor waitingForDoor;
        private Vector3 lastDestination;
        private float nextRepathAt;
        private bool hasDestination;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            // Apply each navigation step only after checking the moving door colliders.
            agent.updatePosition = false;
        }

        private void OnDisable() => Stop();

        public override MoveResult MoveTowards(Vector3 destination, float deltaTime) =>
            Move(destination, walkSpeed, 0.18f);

        public override MoveResult ChaseTowards(Vector3 destination, float deltaTime) =>
            Move(destination, chaseSpeed, 0.85f);

        public override void Stop()
        {
            hasDestination = false;
            waitingForDoor = null;
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private MoveResult Move(Vector3 destination, float speed, float stoppingDistance)
        {
            if (!agent.isOnNavMesh)
                return MoveResult.Unreachable;

            agent.speed = speed;
            agent.stoppingDistance = stoppingDistance;
            bool destinationMoved = (destination - lastDestination).sqrMagnitude > 0.04f;
            if (!hasDestination || (destinationMoved && Time.time >= nextRepathAt))
            {
                if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 1.35f, agent.areaMask) ||
                    !agent.SetDestination(hit.position))
                {
                    Stop();
                    return MoveResult.Unreachable;
                }

                hasDestination = true;
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

            Vector3 towardsCorner = agent.steeringTarget - transform.position;
            CottageDoor door = FindDoor(towardsCorner, Mathf.Max(DoorLookAhead, stoppingDistance + 0.05f), out _);
            if (WaitForDoor(door))
                return MoveResult.Moving;

            agent.isStopped = false;
            return agent.remainingDistance <= stoppingDistance + 0.05f
                ? MoveResult.Arrived
                : MoveResult.Moving;
        }

        private void LateUpdate()
        {
            if (!agent.isOnNavMesh)
                return;

            Vector3 nextPosition = agent.nextPosition;
            Vector3 step = nextPosition - transform.position;
            if (waitingForDoor != null)
            {
                agent.nextPosition = transform.position;
                return;
            }

            CottageDoor door = FindDoor(step, step.magnitude, out float hitDistance);
            if (door != null)
            {
                agent.isStopped = true;
                WaitForDoor(door);
                nextPosition = transform.position + step.normalized * Mathf.Max(0f, hitDistance - 0.01f);
                agent.nextPosition = nextPosition;
            }

            transform.position = nextPosition;
        }

        private bool WaitForDoor(CottageDoor door)
        {
            if (waitingForDoor == null)
                waitingForDoor = door;

            if (waitingForDoor == null || !waitingForDoor.isActiveAndEnabled || waitingForDoor.IsFullyOpen)
            {
                waitingForDoor = null;
                return false;
            }

            agent.isStopped = true;
            waitingForDoor.Open();
            return true;
        }

        private CottageDoor FindDoor(Vector3 offset, float maxDistance, out float nearestDistance)
        {
            nearestDistance = float.PositiveInfinity;
            float length = offset.magnitude;
            if (length <= 0.0001f)
                return null;

            float radius = agent.radius;
            Vector3 feet = transform.position - Vector3.up * agent.baseOffset;
            Vector3 bottom = feet + Vector3.up * (radius + FootClearance);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius + FootClearance, agent.height - radius);
            Vector3 direction = offset / length;
            float distance = Mathf.Min(length, maxDistance);
            RaycastHit[] hits = doorHits;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.CapsuleCastAll(bottom, top, radius, direction, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            CottageDoor nearest = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance >= nearestDistance || !hit.collider.TryGetComponent(out CottageDoor door))
                    continue;

                nearest = door;
                nearestDistance = hit.distance;
            }

            return nearest;
        }
    }
}
