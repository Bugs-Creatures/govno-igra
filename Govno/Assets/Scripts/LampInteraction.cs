using UnityEngine;
using UnityEngine.InputSystem;

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
    [SerializeField] private Shader lampOutlineShader;
    [SerializeField, ColorUsage(false)] private Color lampOutlineColor = new(1f, 0.86f, 0.1f);
    [SerializeField, Min(0f)] private float lampOutlineWidth = 0.018f;

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private readonly RaycastHit[] aimHits = new RaycastHit[8];
    private FirstPersonController player;
    private CharacterController playerCollider;
    private BulbBox heldBox;
    private CeilingLamp outlinedLamp;
    private Material outlineMaterial;
    private CeilingLamp repairTarget;
    private float repairElapsed;
    private float bobPhase;
    private float bobAmount;
    private Vector3 turnSway;
    private Vector3 holdPosition;
    private Quaternion holdRotation;
    private Quaternion previousViewRotation;
    private bool restingReticleFilled;

    private void Awake()
    {
        player = GetComponent<FirstPersonController>();
        playerCollider = GetComponent<CharacterController>();
        holdPosition = boxHoldPoint.localPosition;
        holdRotation = boxHoldPoint.localRotation;
        previousViewRotation = viewCamera.transform.rotation;
        restingReticleFilled = reticle.Filled;
        if (lampOutlineShader != null)
            outlineMaterial = new Material(lampOutlineShader);
    }

    private void LateUpdate()
    {
        UpdateBoxMotion();

        var primary = Mouse.current?.leftButton;
        bool canAim = player.HasGameplayInput && primary != null;
        bool wantsToDrop = Application.isFocused && Keyboard.current?.gKey.wasPressedThisFrame == true;
        UpdateInteraction(canAim, primary?.wasPressedThisFrame == true, primary?.isPressed == true, wantsToDrop);
    }

    private void UpdateInteraction(bool canAim, bool primaryPressed, bool primaryHeld, bool wantsToDrop)
    {
        if (heldBox != null && wantsToDrop)
        {
            DropBox();
            StopRepair(false);
            SetLampOutline(null);
            reticle.Filled = restingReticleFilled;
            return;
        }

        BulbBox box = null;
        CeilingLamp aimedLamp = null;
        if (canAim)
        {
            Ray ray = viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            Collider targetCollider = FindAimCollider(ray);
            if (targetCollider != null)
            {
                aimedLamp = targetCollider.GetComponentInParent<CeilingLamp>();
                if (heldBox == null)
                    box = targetCollider.GetComponentInParent<BulbBox>();
            }
        }

        CeilingLamp brokenLamp = aimedLamp != null && aimedLamp.IsBroken ? aimedLamp : null;
        CeilingLamp lamp = heldBox != null && heldBox.HasBulbs ? brokenLamp : null;
        SetLampOutline(lamp);
        reticle.Filled = restingReticleFilled || box != null || lamp != null;
        if (repairTarget != null)
        {
            if (lamp != repairTarget || !primaryHeld)
            {
                StopRepair(false);
                return;
            }

            repairElapsed += Time.deltaTime;
            float progress = repairElapsed / repairTarget.SecondsToRepair;
            repairProgress.ShowProgress(progress);
            if (progress >= 1f)
            {
                bool repaired = repairTarget.TryRepair(heldBox);
                StopRepair(repaired);
                if (repaired)
                    SetLampOutline(null);
                reticle.Filled = restingReticleFilled;
            }
            return;
        }

        if (!canAim || !primaryPressed)
            return;

        if (box != null)
        {
            heldBox = box;
            heldBox.PickUp(boxHoldPoint);
            reticle.Filled = restingReticleFilled;
        }
        else if (lamp != null)
        {
            repairTarget = lamp;
            repairProgress.ShowProgress(0f);
        }
    }

    private void SetLampOutline(CeilingLamp lamp)
    {
        if (outlineMaterial == null)
            return;

        if (lampOutlineWidth <= 0f)
            lamp = null;
        if (lamp != null)
        {
            outlineMaterial.SetColor(OutlineColorId, lampOutlineColor);
            outlineMaterial.SetFloat(OutlineWidthId, lampOutlineWidth);
        }

        if (outlinedLamp == lamp)
            return;
        if (outlinedLamp != null)
            outlinedLamp.SetOutline(null);
        outlinedLamp = lamp;
        if (outlinedLamp != null)
            outlinedLamp.SetOutline(outlineMaterial);
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
        SetLampOutline(null);
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
            Destroy(outlineMaterial);
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
        bobPhase += speed * dt * 2.8f;

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
    }
}
