using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cyverse.UI
{
    /// <summary>A batch of flat-coloured triangles in one UI mesh.</summary>
    public sealed class DiagramShapeMesh : MaskableGraphic
    {
        private readonly List<UIVertex> verts = new List<UIVertex>();
        private readonly List<int> tris = new List<int>();

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int i = verts.Count;
            Add(a, col); Add(b, col); Add(c, col); Add(d, col);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public void Fan(List<Vector2> pts, Color32 col)
        {
            int i = verts.Count;
            foreach (Vector2 p in pts) Add(p, col);
            for (int k = 1; k + 1 < pts.Count; k++)
            {
                tris.Add(i); tris.Add(i + k); tris.Add(i + k + 1);
            }
        }

        private void Add(Vector2 p, Color32 col)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            v.color = col;
            verts.Add(v);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            vh.AddUIVertexStream(verts, tris);
        }
    }
}
