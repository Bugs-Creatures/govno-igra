using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class BulbBox : MonoBehaviour
{
    private const float ThrowLiftSpeed = 1.85f;
    private const float ExtraThrowGravity = 0.65f;
    private const float ObstacleClearance = 0.025f;

    [SerializeField] private GameObject[] bulbs;

    private Rigidbody body;
    private BoxCollider boxCollider;
    private Renderer[] boxRenderers;
    private int[] worldLayers;
    private RigidbodyInterpolation dropInterpolation;
    private int remaining;
    private bool inThrow;

    public bool HasBulbs => remaining > 0;
    public bool IsHeld { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        boxRenderers = GetComponentsInChildren<Renderer>(true);
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

    public void PickUp(Transform hand, int heldItemLayer = -1)
    {
        inThrow = false;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
        boxCollider.enabled = false;
        transform.SetParent(hand, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        if (heldItemLayer >= 0 && worldLayers == null)
        {
            worldLayers = new int[boxRenderers.Length];
            for (int i = 0; i < boxRenderers.Length; i++)
                worldLayers[i] = boxRenderers[i].gameObject.layer;
            foreach (Renderer renderer in boxRenderers)
                renderer.gameObject.layer = heldItemLayer;
        }
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
