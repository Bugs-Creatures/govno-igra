using UnityEngine;

public sealed class CeilingLamp : MonoBehaviour
{
    [SerializeField] private bool isBroken = true;
    [SerializeField, Min(0.01f)] private float secondsToRemove = 1f;
    [SerializeField, Min(0.01f)] private float secondsToRepair = 1f;
    [SerializeField] private Light lampLight;
    [SerializeField] private AudioSource lampAudio;
    [SerializeField] private AudioClip turningSound;
    [SerializeField] private AudioClip removedSound;
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private GameObject brokenVisual;
    [SerializeField] private bool useLampForRendering = true;

    private MeshOutline outline;
    private AudioSource repairAudio;
    private AudioSource removalAudio;
    private bool audioStateDirty;
    private bool brokenBulbRemoved;

    public bool IsBroken => isBroken;
    public bool IsWorking => isActiveAndEnabled && !isBroken;
    public bool HasBrokenBulb => isBroken && !brokenBulbRemoved && brokenVisual != null;
    public float SecondsForCurrentStage => Mathf.Max(0.01f, HasBrokenBulb ? secondsToRemove : secondsToRepair);

    private void Awake()
    {
        ApplyState();
    }

    private void OnEnable()
    {
        ApplyState();
        ApplyAudioState();
    }

    private void OnDisable()
    {
        if (repairAudio != null)
            repairAudio.Stop();
        if (removalAudio != null)
            removalAudio.Stop();
        ApplyState();
        ApplyAudioState();
    }

    private void OnValidate()
    {
        UpdateLight();
        audioStateDirty = true;
    }

    private void Update()
    {
        ApplyAudioLevels();
        if (audioStateDirty)
            ApplyAudioState();
    }

    public bool TryRemoveBrokenBulb()
    {
        if (!isActiveAndEnabled || !HasBrokenBulb)
            return false;

        brokenBulbRemoved = true;
        ApplyState();
        StopTurningSound();
        if (removedSound != null)
        {
            if (removalAudio == null)
                removalAudio = CreateRepairAudio();
            removalAudio.volume = GameAudioSettings.Current?.BulbRemovedVolume ?? 0.9f;
            removalAudio.PlayOneShot(removedSound);
        }
        return true;
    }

    public bool TryRepair(BulbBox box)
    {
        if (!isActiveAndEnabled || !isBroken || HasBrokenBulb || box == null || !box.TryUseBulb())
            return false;

        isBroken = false;
        brokenBulbRemoved = false;
        ApplyState();
        ApplyAudioState();
        return true;
    }

    public bool TryBreak()
    {
        if (isBroken)
            return false;

        isBroken = true;
        brokenBulbRemoved = false;
        ApplyState();
        ApplyAudioState();
        return true;
    }

    public void SetOutline(Material material, Material silhouetteMask = null)
    {
        if (outline == null && material != null)
            outline = new MeshOutline(GetComponentsInChildren<MeshFilter>(true), silhouetteMask);
        outline?.SetMaterial(material);
    }

    private void ApplyState()
    {
        UpdateLight();
        if (intactVisual != null)
            intactVisual.SetActive(!isBroken);
        if (brokenVisual != null)
            brokenVisual.SetActive(HasBrokenBulb);
    }

    public void StartTurningSound()
    {
        if (turningSound == null)
            return;

        if (repairAudio == null)
            repairAudio = CreateRepairAudio();
        repairAudio.volume = GameAudioSettings.Current?.BulbTurningVolume ?? 0.5f;
        repairAudio.clip = turningSound;
        repairAudio.loop = true;
        repairAudio.Play();
    }

    public void StopTurningSound()
    {
        if (repairAudio == null || !repairAudio.loop)
            return;

        repairAudio.Stop();
        repairAudio.loop = false;
    }

    private AudioSource CreateRepairAudio()
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1.5f;
        source.maxDistance = 6f;
        return source;
    }

    private void ApplyAudioLevels()
    {
        GameAudioSettings settings = GameAudioSettings.Current;
        if (settings == null)
            return;

        if (lampAudio != null && lampAudio.volume != settings.LampHumVolume)
            lampAudio.volume = settings.LampHumVolume;
        if (repairAudio != null && repairAudio.volume != settings.BulbTurningVolume)
            repairAudio.volume = settings.BulbTurningVolume;
        if (removalAudio != null && removalAudio.volume != settings.BulbRemovedVolume)
            removalAudio.volume = settings.BulbRemovedVolume;
    }

    private void UpdateLight()
    {
        if (lampLight != null)
            lampLight.enabled = useLampForRendering && IsWorking;
    }

    private void ApplyAudioState()
    {
        ApplyAudioLevels();
        audioStateDirty = false;
        if (lampAudio == null)
            return;

        if (!IsWorking)
            lampAudio.Stop();
        else if (lampAudio.clip != null && !lampAudio.isPlaying)
            lampAudio.Play();
    }
}
