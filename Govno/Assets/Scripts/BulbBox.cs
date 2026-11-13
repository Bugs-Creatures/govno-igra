using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class BulbBox : MonoBehaviour
{
    private const float ThrowLiftSpeed = 1.85f;
    private const float ExtraThrowGravity = 0.65f;

    [SerializeField] private GameObject[] bulbs;

    private Rigidbody body;
    private BoxCollider boxCollider;
    private RigidbodyInterpolation dropInterpolation;
    private int remaining;
    private bool inThrow;

    public bool HasBulbs => remaining > 0;
    public bool IsHeld { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        dropInterpolation = body.interpolation;
        remaining = bulbs.Length;
    }

    public bool TryUseBulb()
    {
        if (!IsHeld || !HasBulbs)
            return false;

        remaining--;
        bulbs[remaining].SetActive(false);
        return true;
    }

    public void PickUp(Transform hand)
    {
        inThrow = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
        boxCollider.enabled = false;
        transform.SetParent(hand, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        IsHeld = true;
    }

    public void Drop(CharacterController holder, float throwStrength)
    {
        Transform player = holder.transform;
        Quaternion rotation = player.rotation;
        Vector3 forward = player.forward;
        Vector3 releasePosition = GetReleasePosition(holder, rotation);

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(releasePosition, rotation);
        body.position = releasePosition;
        body.rotation = rotation;
        boxCollider.enabled = true;
        Physics.IgnoreCollision(boxCollider, holder);
        body.isKinematic = false;
        body.interpolation = dropInterpolation;

        float forwardSpeed = Mathf.Max(0f, Vector3.Dot(holder.velocity, forward));
        body.linearVelocity = forward * ((3f + forwardSpeed) * Mathf.Max(0f, throwStrength))
            + Vector3.up * ThrowLiftSpeed - player.right * 0.6f;
        inThrow = true;
        IsHeld = false;
    }

    private void FixedUpdate()
    {
        if (inThrow && body.useGravity)
            body.AddForce(Physics.gravity * ExtraThrowGravity, ForceMode.Acceleration);
    }

    private void OnCollisionEnter()
    {
        inThrow = false;
    }

    private Vector3 GetReleasePosition(CharacterController holder, Quaternion rotation)
    {
        Transform player = holder.transform;
        Vector3 origin = player.TransformPoint(holder.center);
        Vector3 toHand = transform.position - origin;
        float distance = toHand.magnitude;
        if (distance < 0.001f)
            return origin;

        Vector3 direction = toHand / distance;
        Vector3 extents = Vector3.Scale(boxCollider.size * 0.5f, transform.lossyScale);
        var hits = Physics.BoxCastAll(origin + rotation * boxCollider.center, extents, direction,
            rotation, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (!hit.collider.transform.IsChildOf(player))
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.025f));
        }
        return origin + direction * distance;
    }
}
