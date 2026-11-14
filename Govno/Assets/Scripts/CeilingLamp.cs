using UnityEngine;

public sealed class CeilingLamp : MonoBehaviour
{
    [SerializeField] private bool isBroken = true;
    [SerializeField, Min(0.01f)] private float secondsToRepair = 2.5f;
    [SerializeField] private Light lampLight;
    [SerializeField] private Renderer diffuser;
    [SerializeField] private Material workingMaterial;
    [SerializeField] private Material brokenMaterial;
    [SerializeField] private bool useLampForRendering = true;

    private MeshOutline outline;

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
    }

    private void OnDisable()
    {
        ApplyState();
    }

    private void OnValidate()
    {
        ApplyState();
    }

    public bool TryRepair(BulbBox box)
    {
        if (!isBroken || !box.TryUseBulb())
            return false;

        isBroken = false;
        ApplyState();
        return true;
    }

    public bool TryBreak()
    {
        if (isBroken)
            return false;

        isBroken = true;
        ApplyState();
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
}
