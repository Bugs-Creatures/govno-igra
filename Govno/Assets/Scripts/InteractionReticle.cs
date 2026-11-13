using UnityEngine;

// Vector uGUI geometry: an antialiased ring or disc without a raster texture.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class InteractionReticle : UnityEngine.UI.MaskableGraphic
{
    private const int Segments = 64;
    private const float MaxFeather = 0.4f;
    private static readonly Vector2[] Directions = CreateDirections();

    [SerializeField] private bool filled;
    [SerializeField, Min(0.01f)] private float thickness = 1f;

    public bool Filled
    {
        get => filled;
        set
        {
            if (filled == value)
                return;

            filled = value;
            SetVerticesDirty();
        }
    }

    public float Thickness
    {
        get => thickness;
        set
        {
            value = Mathf.Max(0.01f, value);
            if (Mathf.Approximately(thickness, value))
                return;

            thickness = value;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f - MaxFeather;
        if (radius <= 0f)
            return;

        float feather = Mathf.Min(radius, Mathf.Min(MaxFeather, thickness * 0.4f));
        float innerRadius = radius - thickness;
        Vector2 center = rect.center;
        Color32 opaque = color;
        Color32 transparent = opaque;
        transparent.a = 0;

        if (filled || innerRadius <= 0f)
        {
            mesh.AddVert(center, opaque, Vector2.zero);
            AddRing(mesh, center, radius - feather, opaque);
            for (int i = 0; i < Segments; i++)
                mesh.AddTriangle(0, 1 + (i + 1) % Segments, 1 + i);
        }
        else
        {
            float innerFeather = Mathf.Min(feather, innerRadius);
            AddRing(mesh, center, innerRadius - innerFeather, transparent);
            AddRing(mesh, center, innerRadius + innerFeather, opaque);
            AddRing(mesh, center, radius - feather, opaque);
        }

        AddRing(mesh, center, radius + feather, transparent);
    }

    private static void AddRing(UnityEngine.UI.VertexHelper mesh, Vector2 center, float radius, Color32 tint)
    {
        int start = mesh.currentVertCount;
        foreach (Vector2 direction in Directions)
            mesh.AddVert(center + direction * radius, tint, Vector2.zero);

        // A first ring or a ring directly after the disc's center has no inner ring.
        if (start < Segments)
            return;

        for (int i = 0; i < Segments; i++)
        {
            int next = (i + 1) % Segments;
            int inner = start - Segments;
            mesh.AddTriangle(inner + i, inner + next, start + i);
            mesh.AddTriangle(inner + next, start + next, start + i);
        }
    }

    private static Vector2[] CreateDirections()
    {
        var directions = new Vector2[Segments];
        for (int i = 0; i < directions.Length; i++)
        {
            float angle = i * (2f * Mathf.PI / Segments);
            directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return directions;
    }
}
