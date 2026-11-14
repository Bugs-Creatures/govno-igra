using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class GameAudioSettings : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    [Header("Footsteps")]
    [SerializeField, Range(0f, 1f)] private float cleanFootstepsVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float creakyFootstepsVolume = 0.3f;

    [Header("Doors")]
    [SerializeField, Range(0f, 1f)] private float doorOpenVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float doorCloseVolume = 1f;

    [Header("Lamps")]
    [SerializeField, Range(0f, 1f)] private float lampHumVolume = 0.3675f;
    [SerializeField, Range(0f, 1f)] private float bulbTurningVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float bulbRemovedVolume = 0.9f;

    public static GameAudioSettings Current { get; private set; }

    public float FootstepsVolume(bool creaky) => masterVolume *
        (creaky ? creakyFootstepsVolume : cleanFootstepsVolume);
    public float DoorVolume(bool opening) => masterVolume * (opening ? doorOpenVolume : doorCloseVolume);
    public float LampHumVolume => masterVolume * lampHumVolume;
    public float BulbTurningVolume => masterVolume * bulbTurningVolume;
    public float BulbRemovedVolume => masterVolume * bulbRemovedVolume;

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
            Current = null;
    }
}
