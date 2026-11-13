using UnityEngine;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class LampLightZone : MonoBehaviour
    {
        [SerializeField] private Light source;
        [SerializeField, Min(0.1f)] private float radius = 2.65f;
        [SerializeField, Min(0.1f)] private float height = 3f;

        public bool ContainsLight(Vector3 worldPosition)
        {
            if (!isActiveAndEnabled || source == null || !source.isActiveAndEnabled || source.intensity <= 0f)
                return false;

            Vector3 offset = transform.InverseTransformPoint(worldPosition);
            return offset.y >= -0.15f && offset.y <= height &&
                   offset.x * offset.x + offset.z * offset.z <= radius * radius;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = ContainsLight(transform.position) ? Color.yellow : Color.gray;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            DrawRing(0f);
            DrawRing(height);
            Gizmos.DrawLine(Vector3.right * radius, Vector3.right * radius + Vector3.up * height);
            Gizmos.DrawLine(Vector3.left * radius, Vector3.left * radius + Vector3.up * height);
            Gizmos.matrix = previous;
        }

        private void DrawRing(float y)
        {
            const int segments = 32;
            Vector3 previous = new Vector3(radius, y, 0f);
            for (int index = 1; index <= segments; index++)
            {
                float angle = index * (2f * Mathf.PI / segments);
                Vector3 point = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
