using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UntilSix.Monster
{
    [DisallowMultipleComponent]
    public sealed class MonsterLightPresentation : MonoBehaviour
    {
        private const float DisappearanceDelay = 1f;
        private static readonly int Opacity = Shader.PropertyToID("_Opacity");

        [SerializeField] private PlayerLightZones lightZones;
        [SerializeField] private GameObject body;
        [SerializeField] private AudioSource stepAudio;
        [SerializeField] private AudioClip[] stepClips;
        [SerializeField] private Material footprintMaterial;
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.25f)] private float footprintLifetime = 4f;

        [Header("Disappearance Audio")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private AudioSource disappearanceAudioSource;
        [SerializeField] private AudioClip disappearanceSound;
        [SerializeField] private AudioSource playerBreathAudioSource;
        [SerializeField] private AudioClip playerBreathSound;

        private readonly Footprint[] prints = new Footprint[16];
        private GameObject trail;
        private Mesh quad;
        private MaterialPropertyBlock properties;
        private Renderer[] bodyRenderers;
        private readonly List<Footstep> pendingSteps = new List<Footstep>(4);
        private int nextPrint;
        private int lastClip = -1;
        private float enteredLightAt;
        private bool hidden;

        private Vector3 ZonePosition => transform.position + Vector3.up * 0.25f;
        private bool InLight => lightZones != null && lightZones.IsLitAt(ZonePosition);

        private void OnEnable()
        {
            bodyRenderers = body != null ? body.GetComponentsInChildren<Renderer>(true) : null;
            enteredLightAt = float.PositiveInfinity;
            SetHidden(InLight);
        }

        private void LateUpdate()
        {
            RefreshVisibility();
            foreach (Footstep step in pendingSteps)
                PlacePrint(step.Foot.position, transform.forward, step.IsLeft);

            pendingSteps.Clear();
            FadePrints();
        }

        public void PlayFootstep(Transform foot, bool isLeft)
        {
            if (!isActiveAndEnabled)
                return;

            if (foot != null)
                pendingSteps.Add(new Footstep(foot, isLeft));

            if (stepAudio == null || stepClips == null || stepClips.Length == 0)
                return;

            bool skipPrevious = stepClips.Length > 1 && lastClip >= 0;
            int index = Random.Range(0, stepClips.Length - (skipPrevious ? 1 : 0));
            if (skipPrevious && index >= lastClip)
                index++;
            lastClip = index;
            stepAudio.pitch = Random.Range(0.48f, 0.54f);
            stepAudio.PlayOneShot(stepClips[index]);
        }

        private void RefreshVisibility()
        {
            if (!InLight)
            {
                enteredLightAt = float.PositiveInfinity;
                if (hidden)
                    SetHidden(false);
                return;
            }

            if (hidden)
                return;

            enteredLightAt = Mathf.Min(enteredLightAt, Time.time);
            if (Time.time - enteredLightAt < DisappearanceDelay)
                return;

            if ((disappearanceSound != null || playerBreathSound != null) && PlayerCanSeeMonster())
            {
                if (disappearanceAudioSource != null && disappearanceSound != null)
                    disappearanceAudioSource.PlayOneShot(disappearanceSound);
                if (playerBreathAudioSource != null && playerBreathSound != null)
                    playerBreathAudioSource.PlayOneShot(playerBreathSound);
            }

            SetHidden(true);
        }

        private void SetHidden(bool value)
        {
            hidden = value;
            if (bodyRenderers == null)
                return;

            foreach (Renderer renderer in bodyRenderers)
                renderer.forceRenderingOff = value;
        }

        private bool PlayerCanSeeMonster()
        {
            if (playerCamera == null || !playerCamera.isActiveAndEnabled || bodyRenderers == null)
                return false;

            foreach (Renderer renderer in bodyRenderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    (playerCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                Bounds bounds = renderer.bounds;
                if (bounds.size.y < 0.01f)
                    continue;

                Vector3 offset = Vector3.up * (bounds.extents.y * 0.6f);
                if (CanSeePoint(bounds.center) || CanSeePoint(bounds.center + offset) ||
                    CanSeePoint(bounds.center - offset))
                    return true;
            }

            return false;
        }

        private bool CanSeePoint(Vector3 position)
        {
            Vector3 viewport = playerCamera.WorldToViewportPoint(position);
            if (viewport.z <= playerCamera.nearClipPlane || viewport.z >= playerCamera.farClipPlane ||
                viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return false;

            Vector3 eye = playerCamera.transform.position;
            Vector3 direction = position - eye;
            foreach (RaycastHit hit in Physics.RaycastAll(eye, direction.normalized, direction.magnitude,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform) &&
                    (lightZones == null || !hit.transform.IsChildOf(lightZones.transform)))
                    return false;
            }

            return true;
        }

        private void PlacePrint(Vector3 foot, Vector3 forward, bool left)
        {
            if (footprintMaterial == null || lightZones == null ||
                !Physics.Raycast(foot + Vector3.up * 0.42f, Vector3.down, out RaycastHit hit,
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
            // Mirror the left-boot texture for right-foot contacts.
            marker.localScale = new Vector3(left ? 0.24f : -0.24f, 0.33f, 1f);
            print.ExpiresAt = Time.time + footprintLifetime;
            print.Renderer.enabled = true;
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
            SetHidden(false);
            if (trail != null)
                Destroy(trail);
            if (quad != null)
                Destroy(quad);
            trail = null;
            quad = null;
            nextPrint = 0;
            pendingSteps.Clear();
        }

        private readonly struct Footstep
        {
            public readonly Transform Foot;
            public readonly bool IsLeft;

            public Footstep(Transform foot, bool isLeft)
            {
                Foot = foot;
                IsLeft = isLeft;
            }
        }

        private sealed class Footprint
        {
            public readonly MeshRenderer Renderer;
            public float ExpiresAt;

            public Footprint(MeshRenderer renderer) => Renderer = renderer;
        }
    }
}
