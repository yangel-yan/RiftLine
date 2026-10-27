using UnityEngine;
using UnityEngine.UI;

namespace Riftline
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RiftlineUIGradient : MaskableGraphic
    {
        private Color from;
        private Color to;
        private bool vertical;

        public void SetColors(Color first, Color last, bool isVertical)
        {
            from = first;
            to = last;
            vertical = isVertical;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            vertices.AddVert(new Vector3(rect.xMin, rect.yMin), to, Vector2.zero);
            vertices.AddVert(new Vector3(rect.xMin, rect.yMax), vertical ? from : to, Vector2.up);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMax), from, Vector2.one);
            vertices.AddVert(new Vector3(rect.xMax, rect.yMin), vertical ? to : from, Vector2.right);
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(2, 3, 0);
        }
    }
}
