using UnityEngine;

public sealed class CeilingLamp : MonoBehaviour
{
    [SerializeField] private bool isBroken = true;
    [SerializeField, Min(0.01f)] private float secondsToRepair = 2.5f;
    [SerializeField] private Light lampLight;
    [SerializeField] private AudioSource lampAudio;
    [SerializeField] private GameObject intactVisual;
    [SerializeField] private GameObject brokenVisual;
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
        UpdateLight();
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
            outline = new MeshOutline(GetComponentsInChildren<MeshFilter>(true), silhouetteMask);
        outline?.SetMaterial(material);
    }

    private void ApplyState()
    {
        UpdateLight();
        if (intactVisual != null)
            intactVisual.SetActive(!isBroken);
        if (brokenVisual != null)
            brokenVisual.SetActive(isBroken);
    }

    private void UpdateLight()
    {
        if (lampLight != null)
            lampLight.enabled = useLampForRendering && IsWorking;
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
