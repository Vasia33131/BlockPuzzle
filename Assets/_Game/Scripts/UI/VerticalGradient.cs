using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlockPuzzle.UI
{
    /// <summary>
    /// Paints a uGUI graphic with a top-to-bottom colour ramp through vertex colours,
    /// which avoids shipping a gradient texture.
    /// </summary>
    [AddComponentMenu("UI/Effects/Vertical Gradient")]
    [DisallowMultipleComponent]
    public class VerticalGradient : BaseMeshEffect
    {
        [SerializeField] private Color topColor = Color.white;
        [SerializeField] private Color bottomColor = Color.black;

        private readonly List<UIVertex> vertices = new List<UIVertex>();

        public void SetColors(Color top, Color bottom)
        {
            topColor = top;
            bottomColor = bottom;

            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive() || helper.currentVertCount == 0)
            {
                return;
            }

            vertices.Clear();
            helper.GetUIVertexStream(vertices);

            float minY = float.MaxValue;
            float maxY = float.MinValue;
            for (int i = 0; i < vertices.Count; i++)
            {
                float y = vertices[i].position.y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            float height = Mathf.Max(0.0001f, maxY - minY);
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex vertex = vertices[i];
                float t = (vertex.position.y - minY) / height;
                vertex.color = Color.Lerp(bottomColor, topColor, t);
                vertices[i] = vertex;
            }

            helper.Clear();
            helper.AddUIVertexTriangleStream(vertices);
        }
    }
}
