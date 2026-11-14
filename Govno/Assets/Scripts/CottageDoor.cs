using UnityEngine;

[RequireComponent(typeof(MeshCollider), typeof(Rigidbody))]
public sealed class CottageDoor : MonoBehaviour
{
    [SerializeField] private bool startsOpen = true;
    [SerializeField] private float openAngle = 89f;
    [SerializeField, Min(1f)] private float degreesPerSecond = 210f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;

    private Rigidbody body;
    private Quaternion closedRotation;
    private float angle;
    private bool wantsOpen;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        angle = startsOpen ? openAngle : 0f;
        wantsOpen = startsOpen;
        closedRotation = Quaternion.AngleAxis(-angle, Vector3.up) * transform.localRotation;
    }

    public void Toggle()
    {
        wantsOpen = !wantsOpen;
        AudioClip sound = wantsOpen ? openSound : closeSound;
        if (audioSource != null && sound != null)
        {
            audioSource.Stop();
            audioSource.PlayOneShot(sound);
        }
    }

    private void FixedUpdate()
    {
        float target = wantsOpen ? openAngle : 0f;
        if (angle == target)
            return;

        angle = Mathf.MoveTowards(angle, target, degreesPerSecond * Time.fixedDeltaTime);
        Quaternion localRotation = Quaternion.AngleAxis(angle, Vector3.up) * closedRotation;
        body.MoveRotation(transform.parent != null ? transform.parent.rotation * localRotation : localRotation);
    }
}
