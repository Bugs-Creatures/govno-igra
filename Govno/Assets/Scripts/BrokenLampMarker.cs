using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class BrokenLampMarker : UnityEngine.UI.MaskableGraphic
{
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        AddPoint(0.55f, 1f);
        AddPoint(0.08f, 0.44f);
        AddPoint(0.42f, 0.44f);
        AddPoint(0.29f, 0f);
        AddPoint(0.92f, 0.62f);
        AddPoint(0.56f, 0.62f);
        mesh.AddTriangle(2, 1, 0);
        mesh.AddTriangle(2, 0, 5);
        mesh.AddTriangle(2, 5, 4);
        mesh.AddTriangle(2, 4, 3);

        void AddPoint(float x, float y)
        {
            Vector2 point = new(rect.xMin + x * rect.width, rect.yMin + y * rect.height);
            mesh.AddVert(point, color, Vector2.zero);
        }
    }
}
