using UnityEngine;

public sealed class CeilingLamp : MonoBehaviour
{
    [SerializeField] private bool isBroken = true;
    [SerializeField, Min(0.01f)] private float secondsToRepair = 2.5f;
    [SerializeField] private Light lampLight;
    [SerializeField] private AudioSource lampAudio;
    [SerializeField] private Renderer diffuser;
    [SerializeField] private Material workingMaterial;
    [SerializeField] private Material brokenMaterial;
    [SerializeField] private bool useLampForRendering = true;

    private MeshOutline outline;
    private bool audioStateDirty;

    public bool IsBroken => isBroken;
    public bool IsWorking => isActiveAndEnabled && !isBroken;
    public float SecondsToRepair => Mathf.Max(0.01f, secondsToRepair);

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
        ApplyState();
        ApplyAudioState();
    }

    private void OnValidate()
    {
        ApplyState();
        audioStateDirty = true;
    }

    private void Update()
    {
        if (audioStateDirty)
            ApplyAudioState();
    }

    public bool TryRepair(BulbBox box)
    {
        if (!isBroken || !box.TryUseBulb())
            return false;

        isBroken = false;
        ApplyState();
        ApplyAudioState();
        return true;
    }

    public bool TryBreak()
    {
        if (isBroken)
            return false;

        isBroken = true;
        ApplyState();
        ApplyAudioState();
        return true;
    }

    public void SetOutline(Material material, Material silhouetteMask = null)
    {
        if (outline == null && material != null)
            outline = new MeshOutline(GetComponentsInChildren<MeshFilter>(), silhouetteMask);
        outline?.SetMaterial(material);
    }

    private void ApplyState()
    {
        if (lampLight != null)
            lampLight.enabled = useLampForRendering && IsWorking;
        if (diffuser != null)
            diffuser.sharedMaterial = isBroken ? brokenMaterial : workingMaterial;
    }

    private void ApplyAudioState()
    {
        audioStateDirty = false;
        if (lampAudio == null)
            return;

        if (!IsWorking)
            lampAudio.Stop();
        else if (lampAudio.clip != null && !lampAudio.isPlaying)
            lampAudio.Play();
    }
}
