using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class BulbBox : MonoBehaviour
{
    private const float ThrowLiftSpeed = 1.85f;
    private const float ExtraThrowGravity = 0.65f;
    private const float ObstacleClearance = 0.025f;
    private const float MaxLidSwing = 4f;
    private const float DoorSlideAcceleration = 5f;
    private const float DoorSlideSpeed = 0.9f;
    private const float FloorSlideSpeed = 0.12f;

    [SerializeField] private GameObject[] bulbs;
    [SerializeField] private Transform lid;
    [SerializeField, Tooltip("Opening angle in degrees from the closed pose.")]
    private float lidOpenAngle = 160f;
    [SerializeField] private Vector3 heldEulerAngles;
    [SerializeField, Tooltip("Hinge axis in the lid parent's local space, accounting for the imported model's orientation.")]
    private Vector3 lidHingeAxis = Vector3.right;

    private Rigidbody body;
    private BoxCollider boxCollider;
    private Renderer[] boxRenderers;
    private int[] worldLayers;
    private RigidbodyInterpolation dropInterpolation;
    private int remaining;
    private bool inThrow;
    private Quaternion closedLidRotation;
    private float lidSwing;
    private float lidVelocity;
    private MeshOutline outline;

    public bool HasBulbs => remaining > 0;
    public bool IsHeld { get; private set; }

    public void SetOutline(Material material)
    {
        if (outline == null && material != null)
            outline = MeshOutline.ForBox(boxCollider);
        outline?.SetMaterial(material);
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        boxRenderers = GetComponentsInChildren<Renderer>(true);
        dropInterpolation = body.interpolation;
        remaining = bulbs.Length;
        if (lid != null)
            closedLidRotation = lid.localRotation;
    }

    public bool TryUseBulb()
    {
        if (!IsHeld || !HasBulbs)
            return false;

        remaining--;
        bulbs[remaining].SetActive(false);
        return true;
    }

    public void PickUp(Transform hand, int heldItemLayer = -1)
    {
        inThrow = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
        boxCollider.enabled = false;
        transform.SetParent(hand, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(heldEulerAngles));
        if (heldItemLayer >= 0 && worldLayers == null)
        {
            worldLayers = new int[boxRenderers.Length];
            for (int i = 0; i < boxRenderers.Length; i++)
                worldLayers[i] = boxRenderers[i].gameObject.layer;
            foreach (Renderer renderer in boxRenderers)
                renderer.gameObject.layer = heldItemLayer;
        }
        IsHeld = true;
        lidSwing = 0f;
        lidVelocity = 0f;
        ApplyHeldLidRotation();
    }

    public void UpdateLidMotion(float stride, float turnSway, float pitchSway, float deltaTime)
    {
        if (lid == null)
            return;

        float target = stride * 2.8f + turnSway * 6.4f - pitchSway * 8f;
        lidSwing = Mathf.SmoothDamp(lidSwing, Mathf.Clamp(target, -MaxLidSwing, MaxLidSwing), ref lidVelocity,
            0.10f, Mathf.Infinity, deltaTime);
        ApplyHeldLidRotation();
    }

    private void ApplyHeldLidRotation()
    {
        if (lid != null)
        {
            lid.localRotation = Quaternion.AngleAxis(lidOpenAngle + lidSwing, lidHingeAxis) * closedLidRotation;
        }
    }

    public void Drop(CharacterController holder, float throwStrength)
    {
        Transform player = holder.transform;
        float tiltSide = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Quaternion rotation = player.rotation * Quaternion.Euler(0f, heldEulerAngles.y, 0f)
            * Quaternion.Euler(4f, 0f, tiltSide * UnityEngine.Random.Range(7f, 11f));
        Vector3 forward = player.forward;
        Vector3 releasePosition = GetReleasePosition(holder, rotation);

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(releasePosition, rotation);
        body.position = releasePosition;
        body.rotation = rotation;
        if (worldLayers != null)
        {
            for (int i = 0; i < boxRenderers.Length; i++)
                boxRenderers[i].gameObject.layer = worldLayers[i];
            worldLayers = null;
        }
        boxCollider.enabled = true;
        Physics.IgnoreCollision(boxCollider, holder);
        body.isKinematic = false;
        body.interpolation = dropInterpolation;

        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(holder.velocity, forward));
        body.linearVelocity = forward * ((3f + forwardSpeed) * Mathf.Max(0f, throwStrength))
            + Vector3.up * ThrowLiftSpeed - player.right * 0.6f;
        body.angularVelocity = rotation * new Vector3(0.16f, 0f, tiltSide * 0.22f);
        inThrow = true;
        IsHeld = false;
        if (lid != null)
            lid.localRotation = closedLidRotation;
    }

    private void FixedUpdate()
    {
        if (inThrow && body.useGravity)
            body.AddForce(Physics.gravity * ExtraThrowGravity, ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision)
    {
        inThrow = false;
        HandleSurfaceContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleSurfaceContact(collision);
    }

    private void HandleSurfaceContact(Collision collision)
    {
        if (IsHeld)
            return;

        // Keep the low-friction response on door leaves so the box can clear handles.
        if (collision.collider is MeshCollider leaf && leaf.name.EndsWith(" | leaf", StringComparison.Ordinal))
        {
            SlideOffDoor(collision, leaf);
            return;
        }

        if (collision.rigidbody != null)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y < 0.85f || contact.point.y >= body.worldCenterOfMass.y)
                continue;

            Vector3 velocity = body.linearVelocity;
            Vector3 normalVelocity = Vector3.Project(velocity, contact.normal);
            Vector3 slide = velocity - normalVelocity;
            if (slide.sqrMagnitude > FloorSlideSpeed * FloorSlideSpeed)
                body.linearVelocity = normalVelocity + slide.normalized * FloorSlideSpeed;
            return;
        }
    }

    private void SlideOffDoor(Collision collision, MeshCollider leaf)
    {
        // Cottage door leaves include the handle in their collision mesh.
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (contact.normal.y < 0.5f || contact.point.y >= body.worldCenterOfMass.y)
                continue;

            // Push along the thin axis of the leaf and away from its mid-plane.
            Bounds doorBounds = leaf.bounds;
            Vector3 centerOffset = body.worldCenterOfMass - doorBounds.center;
            bool thinAlongX = doorBounds.size.x < doorBounds.size.z;
            float side = thinAlongX ? centerOffset.x : centerOffset.z;
            Vector3 direction = (thinAlongX ? Vector3.right : Vector3.forward) * Mathf.Sign(side);
            if (Vector3.Dot(body.linearVelocity, direction) < DoorSlideSpeed)
                body.AddForce(direction * DoorSlideAcceleration, ForceMode.Acceleration);
            return;
        }
    }

    private Vector3 GetReleasePosition(CharacterController holder, Quaternion rotation)
    {
        Transform player = holder.transform;
        Vector3 origin = player.TransformPoint(holder.center);
        Vector3 offset = transform.position - origin;
        float distance = offset.magnitude;
        if (distance < 0.001f)
            return origin;

        Vector3 direction = offset / distance;
        Vector3 scale = transform.lossyScale;
        Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f,
            new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        Vector3 centerOffset = rotation * Vector3.Scale(boxCollider.center, scale);
        var hits = Physics.BoxCastAll(origin + centerOffset, halfExtents, direction, rotation, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.transform.IsChildOf(player))
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - ObstacleClearance));
        }
        return origin + direction * distance;
    }
}
