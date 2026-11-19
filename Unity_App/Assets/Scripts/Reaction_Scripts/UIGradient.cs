using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Graphic))]
public class UIGradient : BaseMeshEffect
{
    public Color startColor = Color.white;
    public Color endColor = Color.black;

    public enum Direction
    {
        Vertical,
        Horizontal
    }

    public Direction direction = Direction.Horizontal;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive())
            return;

        int count = vh.currentVertCount;
        if (count == 0)
            return;

        UIVertex vertex = new UIVertex();

        float min = float.MaxValue;
        float max = float.MinValue;

        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            float value = direction == Direction.Horizontal
                ? vertex.position.x
                : vertex.position.y;

            if (value < min) min = value;
            if (value > max) max = value;
        }

        float range = max - min;
        if (range <= 0f)
            range = 1f;

        for (int i = 0; i < count; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            float value = direction == Direction.Horizontal
                ? vertex.position.x
                : vertex.position.y;

            float t = (value - min) / range;
            vertex.color = Color.Lerp(startColor, endColor, t);

            vh.SetUIVertex(vertex, i);
        }
    }
}