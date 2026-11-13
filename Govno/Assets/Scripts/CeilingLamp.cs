using UnityEngine;

public sealed class CeilingLamp : MonoBehaviour
{
    [SerializeField] private bool isBroken = true;
    [SerializeField, Min(0.01f)] private float secondsToRepair = 2.5f;
    [SerializeField] private Light lampLight;
    [SerializeField] private Renderer diffuser;
    [SerializeField] private Material workingMaterial;
    [SerializeField] private Material brokenMaterial;

    private MeshRenderer[] outlineRenderers;

    public bool IsBroken => isBroken;
    public float SecondsToRepair => Mathf.Max(0.01f, secondsToRepair);

    private void Awake()
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

    public void SetOutline(Material material)
    {
        if (outlineRenderers == null)
        {
            if (material == null)
                return;

            MeshFilter[] meshes = GetComponentsInChildren<MeshFilter>();
            outlineRenderers = new MeshRenderer[meshes.Length];
            for (int i = 0; i < meshes.Length; i++)
            {
                var mesh = meshes[i];
                var outline = new GameObject("Outline");
                outline.layer = mesh.gameObject.layer;
                outline.transform.SetParent(mesh.transform, false);
                outline.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var renderer = outline.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                outlineRenderers[i] = renderer;
            }
        }

        foreach (MeshRenderer renderer in outlineRenderers)
        {
            renderer.enabled = material != null;
            if (material != null)
                renderer.sharedMaterial = material;
        }
    }

    private void ApplyState()
    {
        if (lampLight != null)
            lampLight.enabled = !isBroken;
        if (diffuser != null)
            diffuser.sharedMaterial = isBroken ? brokenMaterial : workingMaterial;
    }
}
