using UnityEngine;

namespace UntilSix.Monster.Demo
{
    [DisallowMultipleComponent]
    public sealed class DirectMonsterMovement : MonsterMovement
    {
        [SerializeField, Min(0.01f)] private float speed = 3.5f;

        public override MoveResult MoveTowards(Vector3 destination, float deltaTime)
        {
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * deltaTime);
            return (transform.position - destination).sqrMagnitude <= 0.0001f
                ? MoveResult.Arrived
                : MoveResult.Moving;
        }

        public override void Stop()
        {
            // The demo advances only when MoveTowards is called.
        }
    }
}
