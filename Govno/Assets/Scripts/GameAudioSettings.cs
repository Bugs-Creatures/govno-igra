using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class GameAudioSettings : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;

    [Header("Footsteps")]
    [SerializeField, Range(0f, 1f)] private float cleanFootstepsVolume = 0.3f;
    [SerializeField, Range(0f, 1f)] private float creakyFootstepsVolume = 0.3f;

    [Header("Jump")]
    [SerializeField] private AudioClip jumpSound;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.3f;
    [SerializeField] private AudioClip landingSound;
    [SerializeField, Range(0f, 1f)] private float landingVolume = 0.3f;

    [Header("Bulb Box")]
    [SerializeField] private AudioClip boxPickupSound;
    [SerializeField, Range(0f, 1f)] private float boxPickupVolume = 0.5f;
    [SerializeField] private AudioClip boxImpactSound;
    [SerializeField, Range(0f, 1f)] private float boxImpactVolume = 0.55f;

    [Header("Doors")]
    [SerializeField, Range(0f, 1f)] private float doorOpenVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float doorCloseVolume = 1f;

    [Header("Lamps")]
    [SerializeField, Range(0f, 1f)] private float lampHumVolume = 0.3675f;
    [SerializeField, Range(0f, 1f)] private float bulbTurningVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float bulbRemovedVolume = 0.9f;

    [Header("Rain")]
    [SerializeField] private AudioClip rainClip;
    [SerializeField, Range(0f, 1f)] private float rainFirstFloorVolume = 0.018f;
    [SerializeField, Range(0f, 1f)] private float rainUpperFloorVolume = 0.035f;
    [SerializeField, Range(0f, 1f)] private float rainWindowBoost = 0.025f;
    [SerializeField, Min(0.1f)] private float rainWindowRange = 2.25f;
    [SerializeField, Min(0f)] private float rainFadeSeconds = 0.6f;
    [SerializeField, Tooltip("World height of the player's feet where the stair transition starts.")]
    private float rainFloorFadeStart = 1.65f;
    [SerializeField, Tooltip("World height of the player's feet where the upper floor volume is reached.")]
    private float rainFloorFadeEnd = 3.15f;
    [SerializeField] private Transform rainListener;
    [SerializeField] private GameObject rainWindowRoot;
    [SerializeField] private Material rainWindowGlass;

    private AudioSource rainSource;
    private readonly List<Renderer> rainWindows = new();

    public static GameAudioSettings Current { get; private set; }

    public float FootstepsVolume(bool creaky) => masterVolume *
        (creaky ? creakyFootstepsVolume : cleanFootstepsVolume);
    public AudioClip JumpSound => jumpSound;
    public float JumpVolume => masterVolume * jumpVolume;
    public AudioClip LandingSound => landingSound;
    public float LandingVolume => masterVolume * landingVolume;
    public AudioClip BoxPickupSound => boxPickupSound;
    public float BoxPickupVolume => masterVolume * boxPickupVolume;
    public AudioClip BoxImpactSound => boxImpactSound;
    public float BoxImpactVolume => masterVolume * boxImpactVolume;
    public float DoorVolume(bool opening) => masterVolume * (opening ? doorOpenVolume : doorCloseVolume);
    public float LampHumVolume => masterVolume * lampHumVolume;
    public float BulbTurningVolume => masterVolume * bulbTurningVolume;
    public float BulbRemovedVolume => masterVolume * bulbRemovedVolume;

    private void OnEnable()
    {
        Current = this;
        rainWindows.Clear();
        if (rainWindowRoot != null && rainWindowGlass != null)
        {
            foreach (Renderer window in rainWindowRoot.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in window.sharedMaterials)
                {
                    if (material != rainWindowGlass)
                        continue;

                    rainWindows.Add(window);
                    break;
                }
            }
        }
    }

    private void OnDisable()
    {
        if (Current == this)
            Current = null;
        if (rainSource != null)
            rainSource.Stop();
    }

    private void LateUpdate()
    {
        if (rainClip == null)
        {
            if (rainSource != null && rainSource.isPlaying)
                rainSource.Stop();
            return;
        }

        if (rainSource == null)
        {
            rainSource = gameObject.AddComponent<AudioSource>();
            rainSource.playOnAwake = false;
            rainSource.loop = true;
            rainSource.spatialBlend = 0f;
            rainSource.volume = 0f;
        }

        if (rainSource.clip != rainClip)
        {
            rainSource.Stop();
            rainSource.clip = rainClip;
        }

        if (!rainSource.isPlaying)
            rainSource.Play();

        float upperFloor = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(rainFloorFadeStart, rainFloorFadeEnd, transform.position.y));
        float floorVolume = Mathf.Lerp(rainFirstFloorVolume, rainUpperFloorVolume, upperFloor);
        Vector3 listenerPosition = rainListener != null ? rainListener.position : transform.position;
        float windowRange = Mathf.Max(0.1f, rainWindowRange);
        float nearestWindowSquared = windowRange * windowRange;
        foreach (Renderer window in rainWindows)
        {
            if (window != null && window.enabled && window.gameObject.activeInHierarchy)
                nearestWindowSquared = Mathf.Min(nearestWindowSquared, window.bounds.SqrDistance(listenerPosition));
        }

        float nearWindow = 1f - Mathf.Sqrt(nearestWindowSquared) / windowRange;
        float volume = Mathf.Clamp01(masterVolume * (floorVolume +
            rainWindowBoost * Mathf.SmoothStep(0f, 1f, nearWindow)));
        float blend = rainFadeSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / rainFadeSeconds);
        rainSource.volume = Mathf.Lerp(rainSource.volume, volume, blend);
    }
}
