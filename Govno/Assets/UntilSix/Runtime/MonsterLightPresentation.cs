using UnityEngine;
using UnityEngine.SceneManagement;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class MonsterLightPresentation : MonoBehaviour
    {
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");

        [SerializeField] private PlayerLightZones lightZones;
        [SerializeField] private GameObject body;
        [SerializeField] private Material footprintMaterial;
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.15f)] private float stepDistance = 0.60f;
        [SerializeField, Min(0.25f)] private float footprintLifetime = 4f;

        private readonly Footprint[] prints = new Footprint[16];
        private GameObject trail;
        private Mesh quad;
        private MaterialPropertyBlock properties;
        private Vector3 lastPosition;
        private float distanceSinceStep;
        private bool left = true;
        private int nextPrint;

        private void OnEnable()
        {
            lastPosition = transform.position;
            distanceSinceStep = 0f;
            RefreshVisibility();
        }

        private void LateUpdate()
        {
            bool inLight = RefreshVisibility();
            Vector3 position = transform.position;
            Vector3 delta = Vector3.ProjectOnPlane(position - lastPosition, Vector3.up);
            lastPosition = position;
            float distance = delta.magnitude;
            if (!inLight || distance > stepDistance * 3f)
            {
                distanceSinceStep = 0f;
            }
            else if (distance > 0.001f)
            {
                distanceSinceStep += distance;
                if (distanceSinceStep >= stepDistance)
                {
                    PlacePrint(position, delta / distance);
                    distanceSinceStep -= stepDistance;
                }
            }

            FadePrints();
        }

        private bool RefreshVisibility()
        {
            bool inLight = lightZones != null && lightZones.IsLitAt(transform.position);
            if (body != null && body.activeSelf == inLight)
                body.SetActive(!inLight);
            return inLight;
        }

        private void PlacePrint(Vector3 position, Vector3 forward)
        {
            if (footprintMaterial == null)
                return;

            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 foot = position + side * (left ? -0.14f : 0.14f);
            if (!Physics.Raycast(foot + Vector3.up * 0.52f, Vector3.down, out RaycastHit hit,
                    0.95f, groundLayers, QueryTriggerInteraction.Ignore) ||
                hit.normal.y < 0.55f || !lightZones.IsLitAt(hit.point + Vector3.up * 0.02f))
                return;

            EnsureTrail();
            Footprint print = prints[nextPrint];
            nextPrint = (nextPrint + 1) % prints.Length;
            Vector3 alongFloor = Vector3.ProjectOnPlane(forward, hit.normal).normalized;
            Transform marker = print.Renderer.transform;
            marker.SetPositionAndRotation(hit.point + hit.normal * 0.009f,
                Quaternion.LookRotation(hit.normal, alongFloor));
            marker.localScale = new Vector3(left ? -0.165f : 0.165f, 0.33f, 1f);
            print.ExpiresAt = Time.time + footprintLifetime;
            print.Renderer.enabled = true;
            left = !left;
        }

        private void FadePrints()
        {
            if (trail == null)
                return;

            float fadeDuration = footprintLifetime * 0.55f;
            foreach (Footprint print in prints)
            {
                if (!print.Renderer.enabled)
                    continue;

                float remaining = print.ExpiresAt - Time.time;
                if (remaining <= 0f)
                {
                    print.Renderer.enabled = false;
                    continue;
                }

                properties.SetFloat(Opacity, Mathf.Clamp01(remaining / fadeDuration));
                print.Renderer.SetPropertyBlock(properties);
            }
        }

        private void EnsureTrail()
        {
            if (trail != null)
                return;

            trail = new GameObject("Monster Footprints");
            SceneManager.MoveGameObjectToScene(trail, gameObject.scene);
            properties = new MaterialPropertyBlock();
            quad = new Mesh
            {
                name = "Footprint Quad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f)
                },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = new[] { 0, 2, 1, 0, 3, 2 }
            };

            for (int i = 0; i < prints.Length; i++)
            {
                var marker = new GameObject($"Footprint {i + 1}");
                marker.transform.SetParent(trail.transform, false);
                marker.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = marker.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = footprintMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                prints[i] = new Footprint(renderer);
            }
        }

        private void OnDisable()
        {
            if (body != null)
                body.SetActive(true);
            if (trail != null)
                Destroy(trail);
            if (quad != null)
                Destroy(quad);
            trail = null;
            quad = null;
            nextPrint = 0;
            left = true;
        }

        private sealed class Footprint
        {
            public readonly MeshRenderer Renderer;
            public float ExpiresAt;

            public Footprint(MeshRenderer renderer) => Renderer = renderer;
        }
    }
}
