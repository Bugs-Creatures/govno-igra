using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class MonsterCorridorWaypoint : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float width = 2f;

        public bool Crosses(Vector3 start, Vector3 end, out float fraction)
        {
            start = transform.InverseTransformPoint(start);
            end = transform.InverseTransformPoint(end);
            fraction = 0f;
            if ((start.z >= 0f) == (end.z >= 0f))
                return false;

            fraction = -start.z / (end.z - start.z);
            Vector3 crossing = Vector3.Lerp(start, end, fraction);
            return Mathf.Abs(crossing.x) <= width * 0.5f && Mathf.Abs(crossing.y) <= 0.55f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.09f);
            Vector3 halfWidth = transform.right * (width * 0.5f);
            Gizmos.DrawLine(transform.position - halfWidth, transform.position + halfWidth);
        }
    }
}
