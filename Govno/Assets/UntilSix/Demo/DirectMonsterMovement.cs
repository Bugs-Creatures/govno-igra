using UnityEngine;

namespace UntilSix.Monster.Demo
{
    [DisallowMultipleComponent]
    public sealed class DirectMonsterMovement : MonsterMovement
    {
        [SerializeField, Min(0.01f)] private float speed = 3.5f;
        [SerializeField, Min(0.01f)] private float chaseSpeed = 4.8f;
        [SerializeField, Min(0f)] private float chaseStoppingDistance = 0.85f;

        public override MoveResult MoveTowards(Vector3 destination, float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * deltaTime);
            return (transform.position - destination).sqrMagnitude <= 0.0001f
                ? MoveResult.Arrived
                : MoveResult.Moving;
        }

        public override MoveResult ChaseTowards(Vector3 destination, float deltaTime)
        {
            Vector3 position = transform.position;
            destination.y = position.y;
            float remaining = Vector3.Distance(position, destination) - chaseStoppingDistance;
            if (remaining <= 0.01f)
                return MoveResult.Arrived;

            float step = Mathf.Min(chaseSpeed * deltaTime, remaining);
            transform.position = Vector3.MoveTowards(position, destination, step);
            return remaining - step <= 0.01f ? MoveResult.Arrived : MoveResult.Moving;
        }

        public override void Stop()
        {
            // The demo advances only when MoveTowards is called.
        }
    }
}
