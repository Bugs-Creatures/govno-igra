using UnityEngine;
using UnityEngine.AI;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshMonsterMovement : MonsterMovement
    {
        [SerializeField, Min(0.1f)] private float walkSpeed = 2.3f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4.8f;

        private NavMeshAgent agent;
        private Vector3 lastDestination;
        private float nextRepathAt;
        private bool hasDestination;

        private void Awake() => agent = GetComponent<NavMeshAgent>();

        public override MoveResult MoveTowards(Vector3 destination, float deltaTime) =>
            Move(destination, walkSpeed, 0.18f);

        public override MoveResult ChaseTowards(Vector3 destination, float deltaTime) =>
            Move(destination, chaseSpeed, 0.85f);

        public override void Stop()
        {
            hasDestination = false;
            if (agent != null && agent.isOnNavMesh)
                agent.ResetPath();
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

            return agent.remainingDistance <= stoppingDistance + 0.05f
                ? MoveResult.Arrived
                : MoveResult.Moving;
        }
    }
}
