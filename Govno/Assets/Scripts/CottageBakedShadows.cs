using UnityEngine;

[ExecuteAlways, DisallowMultipleComponent]
public sealed class CottageBakedShadows : MonoBehaviour
{
    private static readonly int ShadowDepth = Shader.PropertyToID("_CottageShadowDepth");
    private static readonly int LampCount = Shader.PropertyToID("_CottageShadowLampCount");

    [SerializeField] private Texture2DArray depth;

    private void OnEnable()
    {
        Shader.SetGlobalTexture(ShadowDepth, depth);
        Shader.SetGlobalFloat(LampCount, depth != null ? depth.depth / 6 : 0);
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(LampCount, 0);
        Shader.SetGlobalTexture(ShadowDepth, null);
    }
}
