using UnityEngine;

namespace UntilSix.Monster
{
    public enum MoveResult
    {
        Moving,
        Arrived,
        Unreachable
    }

    public abstract class MonsterMovement : MonoBehaviour
    {
        // Arrived means the monster is within reach of the supplied approach point.
        public abstract MoveResult MoveTowards(Vector3 destination, float deltaTime);
        public abstract void Stop();
    }
}
