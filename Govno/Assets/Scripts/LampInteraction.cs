using System.Collections.Generic;
using UntilSix.Monster;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
public sealed class LampInteraction : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform boxHoldPoint;
    [SerializeField] private BulbBox recoveryBoxPrefab;
    [SerializeField] private InteractionReticle reticle;
    [SerializeField] private RepairProgress repairProgress;
    [SerializeField, Min(0.1f)] private float interactionDistance = 3.2f;
    [SerializeField, Min(0f), Tooltip("Multiplies the forward speed of the throw, including movement momentum. The upward arc is fixed.")]
    private float throwStrength = 0.75f;
    [SerializeField, Tooltip("Box tilt in degrees relative to the camera. Negative angles tip the opening toward the player.")]
    private float heldBoxTilt = -34f;
    [SerializeField, Min(0.1f), Tooltip("Distance in front of the camera, in meters.")]
    private float heldBoxDistance = 0.92f;
    [SerializeField, Tooltip("Vertical offset from the camera, in meters. Negative values move the box down.")]
    private float heldBoxHeight = -0.29f;
    [SerializeField] private Shader lampOutlineShader;
    [SerializeField, ColorUsage(false)] private Color lampOutlineColor = new(1f, 0.86f, 0.1f);
    [SerializeField, Min(0f), Tooltip("Lamp outline width in pixels.")] private float lampOutlineWidth = 2f;
    [SerializeField, Min(0f)] private float boxOutlineWidth = 0.014f;

    [Header("Broken Lamp Hint")]
    [SerializeField, Min(0f), Tooltip("Height of the lightning icon in pixels. Zero hides it.")]
    private float brokenLampHintSize = 27.6f;
    [SerializeField] private Color brokenLampHintColor = new(1f, 0.83f, 0.12f, 0.15f);
    [SerializeField, Range(0f, 0.5f)] private float brokenLampHintPulseAmount = 0.3f;
    [SerializeField, Min(0.1f)] private float brokenLampHintPulseSeconds = 1.25f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineCullId = Shader.PropertyToID("_OutlineCull");
    private static readonly int OutlineColorMaskId = Shader.PropertyToID("_OutlineColorMask");
    private static readonly int OutlineStencilRefId = Shader.PropertyToID("_OutlineStencilRef");
    private static readonly int OutlineStencilWriteMaskId = Shader.PropertyToID("_OutlineStencilWriteMask");
    private static readonly int OutlineStencilCompareId = Shader.PropertyToID("_OutlineStencilCompare");
    private static readonly int OutlineStencilPassId = Shader.PropertyToID("_OutlineStencilPass");
    private static readonly float[] RecoveryAngles = { 0f, -22.5f, 22.5f, -45f, 45f, -67.5f, 67.5f, -90f, 90f, -135f, 135f, 180f };
    private static readonly float[] RecoveryDistances = { 3.25f, 2.8f, 2.4f, 1.9f, 1.35f, 0.9f };
    private readonly RaycastHit[] aimHits = new RaycastHit[8];
    private readonly HashSet<CeilingLamp> nearbyBrokenLamps = new();
    private readonly Dictionary<CeilingLamp, BrokenLampMarker> lampMarkers = new();
    private PlayerLightZones lightZones;
    private RectTransform hintCanvas;
    private FirstPersonController player;
    private CharacterController playerCollider;
    private BulbBox heldBox;
    private Camera heldItemCamera;
    private List<Camera> cameraStack;
    private int heldItemLayer = -1;
    private int originalCameraMask;
    private CeilingLamp outlinedLamp;
    private BulbBox outlinedBox;
    private Material outlineMaterial;
    private Material boxOutlineMaterial;
    private Material outlineSilhouetteMask;
    private CeilingLamp repairTarget;
    private float repairElapsed;
    private float bobPhase;
    private float bobAmount;
    private Vector3 turnSway;
    private float holdSideOffset;
    private Quaternion previousViewRotation;
    private bool restingReticleFilled;
    private bool reticleWasEnabled;
    private bool jumpscareActive;

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
        lightZones = GetComponent<PlayerLightZones>();
        if (reticle.canvas != null)
            hintCanvas = reticle.canvas.rootCanvas.transform as RectTransform;
        playerCollider = GetComponent<CharacterController>();
        holdSideOffset = boxHoldPoint.localPosition.x;
        previousViewRotation = viewCamera.transform.rotation;
        restingReticleFilled = reticle.Filled;
        if (lampOutlineShader != null)
        {
            outlineMaterial = new Material(lampOutlineShader);
            boxOutlineMaterial = new Material(outlineMaterial);
            boxOutlineMaterial.EnableKeyword("BOX_SILHOUETTE");
            boxOutlineMaterial.SetFloat(OutlineCullId, (float)UnityEngine.Rendering.CullMode.Back);
            outlineSilhouetteMask = new Material(outlineMaterial) { renderQueue = outlineMaterial.renderQueue - 1 };
            outlineSilhouetteMask.EnableKeyword("SILHOUETTE_MASK");
            outlineSilhouetteMask.SetFloat(OutlineCullId, (float)UnityEngine.Rendering.CullMode.Back);
            outlineSilhouetteMask.SetFloat(OutlineColorMaskId, 0f);
            outlineSilhouetteMask.SetFloat(OutlineStencilRefId, 1f);
            outlineSilhouetteMask.SetFloat(OutlineStencilWriteMaskId, 1f);
            outlineSilhouetteMask.SetFloat(OutlineStencilPassId, (float)UnityEngine.Rendering.StencilOp.Replace);
            outlineMaterial.SetFloat(OutlineStencilRefId, 1f);
            outlineMaterial.SetFloat(OutlineStencilCompareId, (float)UnityEngine.Rendering.CompareFunction.NotEqual);
            outlineMaterial.SetFloat(OutlineCullId, (float)UnityEngine.Rendering.CullMode.Back);
            outlineMaterial.EnableKeyword("OBJECT_SILHOUETTE");
        }

        ConfigureHeldItemCamera();
    }

    private void ConfigureHeldItemCamera()
    {
        int layer = LayerMask.NameToLayer("Held Items");
        var cameraData = viewCamera.GetUniversalAdditionalCameraData();
        cameraStack = cameraData.cameraStack;
        if (layer < 0 || cameraStack == null)
        {
            Debug.LogError("Held items need the Held Items layer and a URP base camera with stacking.", this);
            return;
        }

        var cameraObject = new GameObject("Held Item Camera");
        cameraObject.SetActive(false);
        cameraObject.transform.SetParent(viewCamera.transform, false);
        heldItemCamera = cameraObject.AddComponent<Camera>();
        heldItemCamera.CopyFrom(viewCamera);
        heldItemCamera.cullingMask = 1 << layer;
        heldItemCamera.useOcclusionCulling = false;
        heldItemCamera.enabled = false;

        var overlayData = heldItemCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        // The overlay camera clears depth so only the item's own geometry can occlude it.
        overlayData.renderPostProcessing = false;
        overlayData.requiresColorOption = CameraOverrideOption.Off;
        overlayData.requiresDepthOption = CameraOverrideOption.Off;
        cameraStack.Add(heldItemCamera);
        originalCameraMask = viewCamera.cullingMask;
        viewCamera.cullingMask &= ~heldItemCamera.cullingMask;
        heldItemLayer = layer;
        cameraObject.SetActive(true);
    }

    private void LateUpdate()
    {
        if (player.IsPaused)
            return;

        UpdateBoxMotion();

        var primary = Mouse.current?.leftButton;
        bool canAim = player.HasGameplayInput && primary != null;
        Ray aimRay = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        Collider targetCollider = canAim
            ? FindAimCollider(aimRay, interactionDistance)
            : null;
        CeilingLamp aimedLamp = targetCollider != null ? targetCollider.GetComponentInParent<CeilingLamp>() : null;
        bool wantsToDrop = player.HasGameplayInput && Keyboard.current?.gKey.wasPressedThisFrame == true;
        UpdateInteraction(targetCollider, aimedLamp, primary?.wasPressedThisFrame == true, primary?.isPressed == true, wantsToDrop);
        UpdateLampHints(aimRay, targetCollider, aimedLamp, canAim);
        if (heldItemCamera != null)
            heldItemCamera.enabled = heldBox != null && !player.IsCinematicLocked;
    }

    private void UpdateInteraction(Collider targetCollider, CeilingLamp aimedLamp, bool primaryPressed,
        bool primaryHeld, bool wantsToDrop)
    {
        if (heldBox != null && wantsToDrop)
        {
            DropBox();
            StopRepair(false);
            SetOutline(null, null);
            reticle.Filled = restingReticleFilled;
            return;
        }

        BulbBox box = null;
        CottageDoor door = null;
        if (targetCollider != null)
        {
            door = targetCollider.GetComponentInParent<CottageDoor>();
            if (door != null && !door.isActiveAndEnabled)
                door = null;
            box = targetCollider.GetComponentInParent<BulbBox>();
            if (box != null && (!box.isActiveAndEnabled || box.IsHeld))
                box = null;
        }

        CeilingLamp lamp = CanWorkOnLamp(aimedLamp) ? aimedLamp : null;
        SetOutline(lamp, box);
        reticle.Filled = restingReticleFilled || box != null || lamp != null || door != null;
        if (repairTarget != null)
        {
            if (lamp != repairTarget || !primaryHeld)
            {
                StopRepair(false);
            }
            else
            {
                repairElapsed += Time.deltaTime;
                float progress = repairElapsed / repairTarget.SecondsForCurrentStage;
                repairProgress.ShowProgress(progress);
                if (progress >= 1f)
                {
                    bool removing = repairTarget.HasBrokenBulb;
                    bool completed = removing ? repairTarget.TryRemoveBrokenBulb() : repairTarget.TryRepair(heldBox);
                    StopRepair(completed);
                    if (completed && !CanWorkOnLamp(aimedLamp))
                    {
                        SetOutline(null, null);
                        reticle.Filled = restingReticleFilled;
                    }
                }
                return;
            }
        }

        if (!primaryPressed)
            return;

        if (box != null)
        {
            SetOutline(null, null);
            BulbBox oldBox = heldBox;
            heldBox = box;
            heldBox.PickUp(boxHoldPoint, heldItemLayer);
            // The new box's collider is now clear of the old box's release path.
            if (oldBox != null)
                oldBox.Drop(playerCollider, throwStrength);
            reticle.Filled = restingReticleFilled;
        }
        else if (lamp != null)
        {
            repairTarget = lamp;
            repairTarget.StartTurningSound();
            repairProgress.ShowProgress(0f);
        }
        else if (door != null)
        {
            door.Toggle();
        }
    }

    private void SetOutline(CeilingLamp lamp, BulbBox box)
    {
        if (outlineMaterial == null)
            return;

        if (lampOutlineWidth <= 0f)
            lamp = null;
        if (boxOutlineWidth <= 0f)
            box = null;
        if (lamp != null)
        {
            outlineMaterial.SetColor(OutlineColorId, lampOutlineColor);
            outlineMaterial.SetFloat(OutlineWidthId, lampOutlineWidth);
        }
        if (box != null)
        {
            boxOutlineMaterial.SetColor(OutlineColorId, lampOutlineColor);
            boxOutlineMaterial.SetFloat(OutlineWidthId, boxOutlineWidth);
        }

        if (outlinedLamp != lamp)
        {
            if (outlinedLamp != null)
                outlinedLamp.SetOutline(null);
            outlinedLamp = lamp;
            if (outlinedLamp != null)
                outlinedLamp.SetOutline(outlineMaterial, outlineSilhouetteMask);
        }
        if (outlinedBox != box)
        {
            if (outlinedBox != null)
                outlinedBox.SetOutline(null);
            outlinedBox = box;
            if (outlinedBox != null)
                outlinedBox.SetOutline(boxOutlineMaterial);
        }
    }

    private bool CanWorkOnLamp(CeilingLamp lamp) =>
        lamp != null && lamp.isActiveAndEnabled && lamp.IsBroken &&
        (lamp.HasBrokenBulb || (heldBox != null && heldBox.HasBulbs));

    private void UpdateLampHints(Ray aimRay, Collider targetCollider, CeilingLamp aimedLamp, bool canAim)
    {
        if (jumpscareActive)
            return;

        if (lightZones != null && hintCanvas != null && brokenLampHintSize > 0f)
            lightZones.FindBrokenLamps(nearbyBrokenLamps);
        else
            nearbyBrokenLamps.Clear();

        nearbyBrokenLamps.RemoveWhere(lamp => !CanWorkOnLamp(lamp));
        if (nearbyBrokenLamps.Count > 0 && canAim && targetCollider == null)
        {
            Collider distantTarget = FindAimCollider(aimRay, viewCamera.farClipPlane);
            if (distantTarget != null)
                aimedLamp = distantTarget.GetComponentInParent<CeilingLamp>();
        }

        nearbyBrokenLamps.Remove(aimedLamp);
        foreach (var entry in lampMarkers)
        {
            if (entry.Value != null && !nearbyBrokenLamps.Contains(entry.Key))
                entry.Value.enabled = false;
        }

        float pulsePhase = Time.unscaledTime / Mathf.Max(0.1f, brokenLampHintPulseSeconds);
        float pulse = 1f + brokenLampHintPulseAmount * (0.5f - 0.5f * Mathf.Cos(pulsePhase * 2f * Mathf.PI));
        foreach (CeilingLamp lamp in nearbyBrokenLamps)
        {
            lampMarkers.TryGetValue(lamp, out BrokenLampMarker marker);
            if (!TryGetHintPosition(lamp, out Vector2 position))
            {
                if (marker != null)
                    marker.enabled = false;
                continue;
            }

            if (marker == null)
            {
                var markerObject = new GameObject("Broken Lamp Hint", typeof(RectTransform))
                {
                    layer = hintCanvas.gameObject.layer
                };
                markerObject.transform.SetParent(hintCanvas, false);
                markerObject.transform.SetAsFirstSibling();
                marker = markerObject.AddComponent<BrokenLampMarker>();
                marker.raycastTarget = false;
                lampMarkers[lamp] = marker;
            }

            marker.color = brokenLampHintColor;
            marker.rectTransform.sizeDelta = new Vector2(brokenLampHintSize * 0.68f, brokenLampHintSize);
            marker.rectTransform.localPosition = position;
            marker.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            marker.enabled = true;
        }
    }

    private bool TryGetHintPosition(CeilingLamp lamp, out Vector2 position)
    {
        position = default;
        if (!lamp.TryGetComponent(out BoxCollider lampCollider))
            return false;

        Vector3 anchor = lamp.transform.TransformPoint(lampCollider.center) + Vector3.down * 0.1f;
        Vector3 screen = viewCamera.WorldToScreenPoint(anchor);
        if (screen.z <= viewCamera.nearClipPlane || !viewCamera.pixelRect.Contains(screen))
            return false;

        Ray ray = viewCamera.ScreenPointToRay(screen);
        Collider hit = FindAimCollider(ray, Vector3.Distance(ray.origin, anchor));
        if (hit != null && hit.GetComponentInParent<CeilingLamp>() != lamp)
            return false;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(hintCanvas, screen, null, out position);
    }

    private Collider FindAimCollider(Ray ray, float distanceLimit)
    {
        int count = Physics.RaycastNonAlloc(ray, aimHits, distanceLimit, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = aimHits;
        if (count == aimHits.Length)
        {
            hits = Physics.RaycastAll(ray, distanceLimit, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }

        Collider closest = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= distance)
                continue;

            closest = hit.collider;
            distance = hit.distance;
        }
        return closest;
    }

    private void HideLampHints()
    {
        foreach (BrokenLampMarker marker in lampMarkers.Values)
            if (marker != null)
                marker.enabled = false;
    }

    private void OnDisable()
    {
        HideLampHints();
        SetOutline(null, null);
        if (reticle != null)
            reticle.Filled = restingReticleFilled;

        if (repairTarget != null)
            repairTarget.StopTurningSound();
        repairTarget = null;
        repairElapsed = 0f;
        if (repairProgress != null)
            repairProgress.Clear();
    }

    private void OnDestroy()
    {
        foreach (BrokenLampMarker marker in lampMarkers.Values)
            if (marker != null)
                Destroy(marker.gameObject);
        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
            Destroy(boxOutlineMaterial);
            Destroy(outlineSilhouetteMask);
        }

        if (heldItemCamera != null)
        {
            cameraStack.Remove(heldItemCamera);
            if (viewCamera != null)
                viewCamera.cullingMask = originalCameraMask;
            Destroy(heldItemCamera.gameObject);
        }
    }

    private void StopRepair(bool completed)
    {
        if (repairTarget == null)
            return;
        repairTarget.StopTurningSound();

        repairTarget = null;
        repairElapsed = 0f;
        repairProgress.Fade(completed);
    }

    internal void BeginJumpscare()
    {
        jumpscareActive = true;
        HideLampHints();
        reticleWasEnabled = reticle.enabled;
        reticle.enabled = false;
        StopRepair(false);
        SetOutline(null, null);
        if (heldItemCamera != null)
            heldItemCamera.enabled = false;
    }

    internal void RestoreReticle() => reticle.enabled = reticleWasEnabled;

    internal void EndJumpscare() => jumpscareActive = false;

    internal BulbBox DropJumpscareBox()
    {
        BulbBox carried = heldBox;
        heldBox = null;
        bool keepCarried = carried != null && (carried.HasBulbs || recoveryBoxPrefab == null);
        BulbBox source = keepCarried ? carried : recoveryBoxPrefab;
        if (source == null)
            return null;

        if (!keepCarried && carried != null)
            Destroy(carried.gameObject);

        var shape = source.GetComponent<BoxCollider>();
        Vector3 scale = keepCarried ? source.transform.lossyScale : source.transform.localScale;
        Vector3 centerOffset = Vector3.Scale(shape.center, scale);
        Vector3 size = Vector3.Scale(shape.size, scale);
        Vector3 halfSize = new(Mathf.Abs(size.x) * 0.5f, Mathf.Abs(size.y) * 0.5f, Mathf.Abs(size.z) * 0.5f);
        Quaternion rotation = transform.rotation;
        Physics.SyncTransforms();
        Vector3 position = FindRecoveryBoxCenter(halfSize, rotation) - rotation * centerOffset;
        if (keepCarried)
        {
            carried.DropAt(playerCollider, position, rotation);
            return carried;
        }

        BulbBox replacement = Instantiate(recoveryBoxPrefab, position, rotation);
        Physics.IgnoreCollision(replacement.GetComponent<BoxCollider>(), playerCollider);
        return replacement;
    }

    private Vector3 FindRecoveryBoxCenter(Vector3 halfSize, Quaternion rotation)
    {
        Vector3 feet = playerCollider.bounds.center;
        feet.y = playerCollider.bounds.min.y;
        Vector3 origin = feet + Vector3.up * 0.65f;
        Vector3 eye = viewCamera.transform.position;
        float verticalSlope = Mathf.Tan(viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        Vector3? firstSafeCenter = null;
        foreach (float angle in RecoveryAngles)
        {
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            foreach (float distance in RecoveryDistances)
            {
                Vector3 candidate = origin + direction * distance;
                if (!Physics.Raycast(candidate, Vector3.down, out RaycastHit floor, 2.5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.7f)
                    continue;

                Vector3 center = floor.point + Vector3.up * (halfSize.y + 0.03f);
                Vector3 offset = center - origin;
                if (FindAimCollider(new Ray(origin, offset.normalized), offset.magnitude) != null ||
                    Physics.CheckBox(center, halfSize, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;

                firstSafeCenter ??= center;
                Vector3 fromEye = center - eye;
                float horizontalDistance = new Vector2(fromEye.x, fromEye.z).magnitude;
                // The camera will be level, so leave some space below the box in the viewport.
                if (fromEye.y < 0f && -fromEye.y <= horizontalDistance * verticalSlope * 0.88f &&
                    FindAimCollider(new Ray(eye, fromEye.normalized), fromEye.magnitude) == null)
                    return center;
            }
        }

        // A narrow doorway may have room only underneath the player.
        return firstSafeCenter ?? (feet + Vector3.up * (halfSize.y + 0.03f));
    }

    private BulbBox DropBox()
    {
        BulbBox dropped = heldBox;
        if (dropped != null)
            dropped.Drop(playerCollider, throwStrength);
        heldBox = null;
        return dropped;
    }

    private void UpdateBoxMotion()
    {
        Quaternion currentRotation = viewCamera.transform.rotation;
        Vector3 delta = (Quaternion.Inverse(previousViewRotation) * currentRotation).eulerAngles;
        previousViewRotation = currentRotation;
        Vector3 holdPosition = new(holdSideOffset, heldBoxHeight, heldBoxDistance);
        Quaternion holdRotation = Quaternion.Euler(heldBoxTilt, 0f, 0f);
        if (heldBox == null)
        {
            bobAmount = 0f;
            turnSway = Vector3.zero;
            boxHoldPoint.SetLocalPositionAndRotation(holdPosition, holdRotation);
            return;
        }

        float dt = Time.deltaTime;
        float blend = 1f - Mathf.Exp(-14f * dt);
        Vector3 velocity = player.Velocity;
        velocity.y = 0f;
        float speed = player.IsGrounded ? velocity.magnitude : 0f;
        bobAmount = Mathf.Lerp(bobAmount, Mathf.Clamp01(speed / 4f) * 0.5f, blend);
        bobPhase += speed * dt * 1.4f;

        float yawRate = Mathf.DeltaAngle(0f, delta.y) / Mathf.Max(0.001f, dt);
        float pitchRate = Mathf.DeltaAngle(0f, delta.x) / Mathf.Max(0.001f, dt);
        Vector3 swayTarget = new(
            Mathf.Clamp(-yawRate * 0.0015f, -0.33f, 0.33f),
            Mathf.Clamp(pitchRate * 0.0015f, -0.33f, 0.33f),
            0f);
        turnSway = Vector3.Lerp(turnSway, swayTarget, blend);

        Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.022f,
            Mathf.Sin(2f * bobPhase) * 0.018f, 0f) * bobAmount;
        boxHoldPoint.localPosition = holdPosition + bob + turnSway * 0.28f;
        boxHoldPoint.localRotation = holdRotation * Quaternion.Euler(
            -turnSway.y * 28f, turnSway.x * 25f,
            turnSway.x * 15f + Mathf.Sin(bobPhase) * 2f * bobAmount);
        heldBox.UpdateLidMotion(Mathf.Sin(2f * bobPhase) * bobAmount * 2f,
            turnSway.x, turnSway.y, dt);
    }
}
