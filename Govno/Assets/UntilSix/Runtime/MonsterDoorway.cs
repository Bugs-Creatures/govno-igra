using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class MonsterDoorway : MonoBehaviour
    {
        [SerializeField] private CottageDoor door;

        private BoxCollider trigger;
        private Bounds Volume => new Bounds(trigger.center, trigger.size);
        public Vector3 Threshold => transform.TransformPoint(trigger.center +
            Vector3.down * (trigger.size.y * 0.5f - 0.08f));

        private void Awake() => trigger = GetComponent<BoxCollider>();

        public bool TryGetCrossing(Vector3 start, Vector3 end, out float fraction)
        {
            Vector3 localStart = transform.InverseTransformPoint(start) - trigger.center;
            Vector3 localEnd = transform.InverseTransformPoint(end) - trigger.center;
            fraction = 0f;
            if (!trigger.enabled || (localStart.z >= 0f) == (localEnd.z >= 0f))
                return false;

            fraction = -localStart.z / (localEnd.z - localStart.z);
            Vector3 crossing = Vector3.Lerp(localStart, localEnd, fraction);
            return Mathf.Abs(crossing.x) <= trigger.size.x * 0.5f &&
                Mathf.Abs(crossing.y) <= trigger.size.y * 0.5f;
        }

        public bool TryGetEntryDistance(Vector3 position, Vector3 direction, float distance, out float entry)
        {
            entry = 0f;
            if (!trigger.enabled)
                return false;
            Vector3 localPosition = transform.InverseTransformPoint(position);
            Bounds volume = Volume;
            if (volume.Contains(localPosition))
                return true;

            Vector3 localDirection = transform.InverseTransformVector(direction);
            float scale = localDirection.magnitude;
            if (distance <= 0f || scale <= Mathf.Epsilon ||
                !volume.IntersectRay(new Ray(localPosition, localDirection / scale), out float localEntry))
                return false;

            entry = localEntry / scale;
            return entry <= distance;
        }

        public void OpenAlong(Vector3 position, Vector3 direction, float distance)
        {
            if (door != null && door.isActiveAndEnabled &&
                TryGetEntryDistance(position, direction, distance, out _))
                door.Open();
        }
    }
}
