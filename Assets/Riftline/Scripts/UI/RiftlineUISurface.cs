using UnityEngine;
using UnityEngine.UI;

namespace Riftline
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RiftlineUISurface : UnityEngine.UI.Image
    {
        private const int CornerSegments = 6;
        private const int PointsPerCorner = CornerSegments + 1;
        private const int PerimeterPointCount = PointsPerCorner * 4;
        private const float ThinSurfaceThreshold = 8f;
        private const float ShadowExpansion = 4f;

        private static readonly Vector2[] CornerPoints = CreateCornerPoints();

        private readonly Vector2[] outerPoints = new Vector2[PerimeterPointCount];
        private readonly Vector2[] innerPoints = new Vector2[PerimeterPointCount];
        private readonly Vector2[] shadowOuterPoints = new Vector2[PerimeterPointCount];
        private readonly Vector2[] shadowInnerPoints = new Vector2[PerimeterPointCount];

        private float cornerRadius;
        private float borderWidth;
        private Color borderColor;
        private bool hasShadow;

        public void Configure(float radius, float borderWidth, Color borderColor, bool shadow)
        {
            radius = Mathf.Max(0f, radius);
            borderWidth = Mathf.Max(0f, borderWidth);
            if (Mathf.Approximately(cornerRadius, radius) &&
                Mathf.Approximately(this.borderWidth, borderWidth) &&
                this.borderColor == borderColor &&
                hasShadow == shadow)
            {
                return;
            }

            cornerRadius = radius;
            this.borderWidth = borderWidth;
            this.borderColor = borderColor;
            hasShadow = shadow;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            if (!IsFinite(rect.xMin) || !IsFinite(rect.yMin) ||
                !IsFinite(rect.width) || !IsFinite(rect.height) ||
                rect.width <= Mathf.Epsilon || rect.height <= Mathf.Epsilon ||
                color.a <= 0f)
            {
                return;
            }

            float shortAxis = Mathf.Min(rect.width, rect.height);
            bool thinSurface = shortAxis < ThinSurfaceThreshold;
            bool compactSurface = shortAxis < cornerRadius * 2f;
            float radius = thinSurface ? 0f : Mathf.Min(cornerRadius, shortAxis * .25f);
            float edgeWidth = thinSurface ? 0f : Mathf.Min(borderWidth, shortAxis * .25f);

            PopulatePerimeter(outerPoints, rect, radius, Vector2.zero);

            if (hasShadow && !thinSurface && !compactSurface)
            {
                Rect expanded = new Rect(
                    rect.xMin - ShadowExpansion,
                    rect.yMin - ShadowExpansion,
                    rect.width + ShadowExpansion * 2f,
                    rect.height + ShadowExpansion * 2f);
                Vector2 offset = new Vector2(0f, -3f);
                PopulatePerimeter(shadowOuterPoints, expanded, radius + ShadowExpansion, offset);
                PopulatePerimeter(shadowInnerPoints, rect, radius, offset);
                Color shadowColor = new Color(0f, 0f, 0f, .18f * color.a);
                AddRing(vertices, shadowOuterPoints, shadowInnerPoints, Color.clear, shadowColor);
                AddFan(vertices, shadowInnerPoints, rect.center + offset, shadowColor, false, rect);
            }

            if (edgeWidth > Mathf.Epsilon)
            {
                Rect inset = new Rect(
                    rect.xMin + edgeWidth,
                    rect.yMin + edgeWidth,
                    rect.width - edgeWidth * 2f,
                    rect.height - edgeWidth * 2f);
                float innerRadius = Mathf.Max(0f, radius - edgeWidth);
                PopulatePerimeter(innerPoints, inset, innerRadius, Vector2.zero);

                Color edge = borderColor;
                edge.a *= color.a;
                AddRing(vertices, outerPoints, innerPoints, edge, edge);
                AddFan(vertices, innerPoints, inset.center, color, true, rect);
            }
            else
            {
                AddFan(vertices, outerPoints, rect.center, color, true, rect);
            }
        }

        private static Vector2[] CreateCornerPoints()
        {
            var points = new Vector2[PerimeterPointCount];
            float[] startAngles = { -90f, 180f, 90f, 0f };
            for (int corner = 0; corner < 4; corner++)
            {
                for (int segment = 0; segment <= CornerSegments; segment++)
                {
                    float angle = (startAngles[corner] - 90f * segment / CornerSegments) * Mathf.Deg2Rad;
                    points[corner * PointsPerCorner + segment] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }
            return points;
        }

        private static void PopulatePerimeter(Vector2[] points, Rect rect, float radius, Vector2 offset)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center;
                if (corner == 0)
                {
                    center = new Vector2(rect.xMin + radius, rect.yMin + radius);
                }
                else if (corner == 1)
                {
                    center = new Vector2(rect.xMin + radius, rect.yMax - radius);
                }
                else if (corner == 2)
                {
                    center = new Vector2(rect.xMax - radius, rect.yMax - radius);
                }
                else
                {
                    center = new Vector2(rect.xMax - radius, rect.yMin + radius);
                }

                int first = corner * PointsPerCorner;
                for (int segment = 0; segment < PointsPerCorner; segment++)
                {
                    points[first + segment] = center + CornerPoints[first + segment] * radius + offset;
                }
            }
        }

        private static void AddFan(VertexHelper vertices, Vector2[] perimeter, Vector2 center, Color tint,
            bool shadeVertically, Rect shadingRect)
        {
            int centerIndex = vertices.currentVertCount;
            AddVertex(vertices, center, shadeVertically ? Shade(tint, center.y, shadingRect) : tint);
            for (int i = 0; i < perimeter.Length; i++)
            {
                Color vertexColor = shadeVertically ? Shade(tint, perimeter[i].y, shadingRect) : tint;
                AddVertex(vertices, perimeter[i], vertexColor);
            }

            for (int i = 0; i < perimeter.Length; i++)
            {
                vertices.AddTriangle(centerIndex, centerIndex + 1 + i,
                    centerIndex + 1 + (i + 1) % perimeter.Length);
            }
        }

        private static void AddRing(VertexHelper vertices, Vector2[] outer, Vector2[] inner,
            Color outerColor, Color innerColor)
        {
            for (int i = 0; i < outer.Length; i++)
            {
                int next = (i + 1) % outer.Length;
                int first = vertices.currentVertCount;
                AddVertex(vertices, outer[i], outerColor);
                AddVertex(vertices, outer[next], outerColor);
                AddVertex(vertices, inner[next], innerColor);
                AddVertex(vertices, inner[i], innerColor);
                vertices.AddTriangle(first, first + 1, first + 2);
                vertices.AddTriangle(first + 2, first + 3, first);
            }
        }

        private static Color Shade(Color tint, float y, Rect rect)
        {
            float position = Mathf.InverseLerp(rect.yMin, rect.yMax, y);
            float brightness = Mathf.Lerp(.88f, 1.06f, position);
            return new Color(
                Mathf.Clamp01(tint.r * brightness),
                Mathf.Clamp01(tint.g * brightness),
                Mathf.Clamp01(tint.b * brightness),
                tint.a);
        }

        private static void AddVertex(VertexHelper vertices, Vector2 position, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = tint;
            vertex.uv0 = Vector2.zero;
            vertices.AddVert(vertex);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
