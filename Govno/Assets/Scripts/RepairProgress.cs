using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class RepairProgress : UnityEngine.UI.MaskableGraphic
{
    [SerializeField, Min(1f)] private float radius = 20f;
    [SerializeField, Min(0.1f)] private float width = 3f;
    [SerializeField] private Color startColor = new(1f, 0.67f, 0.24f);
    [SerializeField] private Color endColor = new(0.46f, 1f, 0.74f);
    [SerializeField, Min(0f)] private float cancelFadeSeconds = 0.25f;
    [SerializeField, Min(0f)] private float completeFadeSeconds = 0.45f;

    private const int Segments = 96;
    private float progress;
    private float opacity;
    private float fadeSeconds;
    private float fadeRemaining;

    public void ShowProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        opacity = 1f;
        fadeRemaining = 0f;
        SetVerticesDirty();
    }

    public void Fade(bool completed)
    {
        fadeSeconds = completed ? completeFadeSeconds : cancelFadeSeconds;
        fadeRemaining = fadeSeconds;
        if (fadeRemaining <= 0f)
            Clear();
    }

    public void Clear()
    {
        progress = 0f;
        opacity = 0f;
        fadeRemaining = 0f;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (fadeRemaining <= 0f)
            return;

        fadeRemaining = Mathf.Max(0f, fadeRemaining - Time.unscaledDeltaTime);
        opacity = fadeRemaining / fadeSeconds;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        if (progress <= 0f || opacity <= 0f)
            return;

        Color tint = Color.Lerp(startColor, endColor, progress);
        tint.a *= opacity;
        Color transparent = tint;
        transparent.a = 0f;
        float inner = Mathf.Max(0f, radius - width * 0.5f);
        float outer = radius + width * 0.5f;
        float feather = Mathf.Min(0.5f, width * 0.25f);
        Vector2 center = rectTransform.rect.center;
        int steps = Mathf.CeilToInt(progress * Segments);
        for (int i = 0; i <= steps; i++)
        {
            float angle = (2f * Mathf.PI) * Mathf.Min(i / (float)Segments, progress);
            Vector2 direction = new(Mathf.Sin(angle), Mathf.Cos(angle));
            mesh.AddVert(center + direction * Mathf.Max(0f, inner - feather), transparent, Vector2.zero);
            mesh.AddVert(center + direction * inner, tint, Vector2.zero);
            mesh.AddVert(center + direction * outer, tint, Vector2.zero);
            mesh.AddVert(center + direction * (outer + feather), transparent, Vector2.zero);

            if (i == 0)
                continue;

            for (int band = 0; band < 3; band++)
            {
                int a = (i - 1) * 4 + band;
                int b = a + 4;
                mesh.AddTriangle(a, a + 1, b);
                mesh.AddTriangle(a + 1, b + 1, b);
            }
        }
    }
}
