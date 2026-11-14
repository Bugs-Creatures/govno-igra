using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(FirstPersonController), typeof(CharacterController))]
public sealed class LampInteraction : MonoBehaviour
{
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Transform boxHoldPoint;
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
    [SerializeField, Min(0f)] private float lampOutlineWidth = 0.018f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int OutlineCullId = Shader.PropertyToID("_OutlineCull");
    private static readonly int OutlineZTestId = Shader.PropertyToID("_OutlineZTest");
    private static readonly int OutlineColorMaskId = Shader.PropertyToID("_OutlineColorMask");
    private static readonly int OutlineStencilRefId = Shader.PropertyToID("_OutlineStencilRef");
    private static readonly int OutlineStencilWriteMaskId = Shader.PropertyToID("_OutlineStencilWriteMask");
    private static readonly int OutlineStencilCompareId = Shader.PropertyToID("_OutlineStencilCompare");
    private static readonly int OutlineStencilPassId = Shader.PropertyToID("_OutlineStencilPass");
    private readonly RaycastHit[] aimHits = new RaycastHit[8];
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

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
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
            outlineSilhouetteMask.SetFloat(OutlineCullId, (float)UnityEngine.Rendering.CullMode.Off);
            outlineSilhouetteMask.SetFloat(OutlineZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
            outlineSilhouetteMask.SetFloat(OutlineColorMaskId, 0f);
            outlineSilhouetteMask.SetFloat(OutlineStencilRefId, 1f);
            outlineSilhouetteMask.SetFloat(OutlineStencilWriteMaskId, 1f);
            outlineSilhouetteMask.SetFloat(OutlineStencilPassId, (float)UnityEngine.Rendering.StencilOp.Replace);
            outlineMaterial.SetFloat(OutlineStencilRefId, 1f);
            outlineMaterial.SetFloat(OutlineStencilCompareId, (float)UnityEngine.Rendering.CompareFunction.NotEqual);
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
        UpdateBoxMotion();

        var primary = Mouse.current?.leftButton;
        bool canAim = player.HasGameplayInput && primary != null;
        bool wantsToDrop = Application.isFocused && Keyboard.current?.gKey.wasPressedThisFrame == true;
        UpdateInteraction(canAim, primary?.wasPressedThisFrame == true, primary?.isPressed == true, wantsToDrop);
        if (heldItemCamera != null)
            heldItemCamera.enabled = heldBox != null;
    }

    private void UpdateInteraction(bool canAim, bool primaryPressed, bool primaryHeld, bool wantsToDrop)
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
        CeilingLamp aimedLamp = null;
        CottageDoor door = null;
        if (canAim)
        {
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Collider targetCollider = FindAimCollider(ray);
            if (targetCollider != null)
            {
                aimedLamp = targetCollider.GetComponentInParent<CeilingLamp>();
                door = targetCollider.GetComponentInParent<CottageDoor>();
                if (door != null && !door.isActiveAndEnabled)
                    door = null;
                if (heldBox == null)
                    box = targetCollider.GetComponentInParent<BulbBox>();
            }
        }

        CeilingLamp brokenLamp = aimedLamp != null && aimedLamp.IsBroken ? aimedLamp : null;
        CeilingLamp lamp = heldBox != null && heldBox.HasBulbs ? brokenLamp : null;
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
                float progress = repairElapsed / repairTarget.SecondsToRepair;
                repairProgress.ShowProgress(progress);
                if (progress >= 1f)
                {
                    bool repaired = repairTarget.TryRepair(heldBox);
                    StopRepair(repaired);
                    if (repaired)
                        SetOutline(null, null);
                    reticle.Filled = restingReticleFilled;
                }
                return;
            }
        }

        if (!canAim || !primaryPressed)
            return;

        if (box != null)
        {
            SetOutline(null, null);
            heldBox = box;
            heldBox.PickUp(boxHoldPoint, heldItemLayer);
            reticle.Filled = restingReticleFilled;
        }
        else if (lamp != null)
        {
            repairTarget = lamp;
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
        {
            lamp = null;
            box = null;
        }
        if (lamp != null)
        {
            outlineMaterial.SetColor(OutlineColorId, lampOutlineColor);
            outlineMaterial.SetFloat(OutlineWidthId, lampOutlineWidth);
        }
        if (box != null)
        {
            boxOutlineMaterial.SetColor(OutlineColorId, lampOutlineColor);
            boxOutlineMaterial.SetFloat(OutlineWidthId, lampOutlineWidth);
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

    private Collider FindAimCollider(Ray ray)
    {
        int count = Physics.RaycastNonAlloc(ray, aimHits, interactionDistance, Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        RaycastHit[] hits = aimHits;
        if (count == aimHits.Length)
        {
            hits = Physics.RaycastAll(ray, interactionDistance, Physics.DefaultRaycastLayers,
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

    private void OnDisable()
    {
        SetOutline(null, null);
        if (reticle != null)
            reticle.Filled = restingReticleFilled;

        repairTarget = null;
        repairElapsed = 0f;
        if (repairProgress != null)
            repairProgress.Clear();
    }

    private void OnDestroy()
    {
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

        repairTarget = null;
        repairElapsed = 0f;
        repairProgress.Fade(completed);
    }

    private void DropBox()
    {
        heldBox.Drop(playerCollider, throwStrength);
        heldBox = null;
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
