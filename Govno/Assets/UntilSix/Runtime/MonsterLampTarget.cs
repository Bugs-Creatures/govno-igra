using UnityEngine;

namespace UntilSix.Monster
{
    public abstract class MonsterLampTarget : MonoBehaviour
    {
        public abstract bool IsAvailable { get; }
        public abstract Vector3 ApproachPosition { get; }
        public abstract bool TryBreak();
    }
}
