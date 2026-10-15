using System.Collections.Generic;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Small building blocks for the blocky, chamfered props (the MFA area,
    /// Data Triage): bevelled blocks and boxes, arbitrary mesh parts, and
    /// batching many small pieces into one mesh so a detailed prop stays a
    /// handful of renderers on WebGL.
    /// </summary>
    public static class BlockKit
    {
        /// <summary>A plain finish. The house body, trim, recess and chrome
        /// colours come from their shared palette file (see Palette), so an
        /// artist can recolour them; any other colour gets its own material.</summary>
        public static Material Mat(Color c, float smoothness, float metallic)
        {
            string finish = Palette.FinishFor(c);
            return finish != null ? Palette.Get(finish, c, smoothness, metallic) : BuildKit.MakeStandard(c, smoothness, metallic);
        }

        /// <summary>A flat-shaded bevelled block (see CharacterMesh.BevelBlock).</summary>
        public static Transform Block(string name, Transform parent, Vector3 pos, Vector3 euler, float y0, float y1,
            Vector2 bottom, Vector2 top, float bevel, Material mat, bool solid = false)
        {
            return MeshPart(name, parent, CharacterMesh.BevelBlock(y0, y1, bottom, top, bevel, 0f, name), pos, euler, mat, solid);
        }

        /// <summary>A bevelled box of <paramref name="size"/> centred on <paramref name="center"/>.</summary>
        public static Transform Slab(string name, Transform parent, Vector3 center, Vector3 size, float bevel,
            Material mat, bool solid = false)
        {
            Vector2 half = new Vector2(size.x * 0.5f, size.z * 0.5f);
            return Block(name, parent, center, Vector3.zero, -size.y * 0.5f, size.y * 0.5f, half, half, bevel, mat, solid);
        }

        public static Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat) =>
            MeshPart(name, parent, CubeMesh, pos, Vector3.zero, mat, false, scale: size);

        public static Transform MeshPart(string name, Transform parent, Mesh mesh, Vector3 pos, Vector3 euler,
            Material mat, bool solid = false, Vector3? scale = null)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale ?? Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (solid) go.AddComponent<BoxCollider>();
            return go.transform;
        }

        public static CombineInstance Piece(Mesh mesh, Vector3 pos, Vector3 euler, Vector3 scale) =>
            new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale) };

        /// <summary>Many small pieces in one material as one mesh, one renderer.</summary>
        public static Transform Batch(string name, Transform parent, Material mat, List<CombineInstance> parts)
        {
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            Transform t = MeshPart(name, parent, mesh, Vector3.zero, Vector3.zero, mat);
            t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        /// <summary>Four thin bars outlining a <paramref name="sizeX"/> by
        /// <paramref name="sizeZ"/> rectangle lying flat at <paramref name="center"/>.</summary>
        public static List<CombineInstance> Outline(Vector3 center, float sizeX, float sizeZ, float bar)
        {
            float hx = sizeX * 0.5f, hz = sizeZ * 0.5f;
            return new List<CombineInstance>
            {
                Piece(CubeMesh, center + new Vector3(0f, 0f, -hz), Vector3.zero, new Vector3(sizeX + bar, bar * 0.5f, bar)),
                Piece(CubeMesh, center + new Vector3(0f, 0f, hz), Vector3.zero, new Vector3(sizeX + bar, bar * 0.5f, bar)),
                Piece(CubeMesh, center + new Vector3(-hx, 0f, 0f), Vector3.zero, new Vector3(bar, bar * 0.5f, sizeZ)),
                Piece(CubeMesh, center + new Vector3(hx, 0f, 0f), Vector3.zero, new Vector3(bar, bar * 0.5f, sizeZ)),
            };
        }

        /// <summary>UVs projected flat across the mesh's X/Y bounds (0..1), for
        /// shaders that draw from UVs — the hologram's grid, scanlines and edge
        /// fade — on meshes that have none (bevelled blocks).</summary>
        public static void PlanarUVs(Mesh mesh)
        {
            Bounds b = mesh.bounds;
            Vector3[] verts = mesh.vertices;
            var uvs = new Vector2[verts.Length];
            float w = Mathf.Max(b.size.x, 0.0001f), h = Mathf.Max(b.size.y, 0.0001f);
            for (int i = 0; i < verts.Length; i++)
                uvs[i] = new Vector2((verts[i].x - b.min.x) / w, (verts[i].y - b.min.y) / h);
            mesh.uv = uvs;
        }

        /// <summary>Hides then destroys the named children, so a restyled prop
        /// never shows (or collides with) its old primitives for a frame.</summary>
        public static void Remove(Transform parent, params string[] names)
        {
            foreach (string name in names)
            {
                Transform part = parent.Find(name);
                if (part == null) continue;
                part.gameObject.SetActive(false);
                if (Application.isPlaying) Object.Destroy(part.gameObject);
                else Object.DestroyImmediate(part.gameObject);
            }
        }

        private static Mesh cubeMesh, quadMesh;

        /// <summary>Unity's unit cube, borrowed from a throwaway primitive.</summary>
        public static Mesh CubeMesh => cubeMesh != null ? cubeMesh : (cubeMesh = PrimitiveMesh(PrimitiveType.Cube));

        /// <summary>Unity's unit quad (facing -Z, with 0..1 UVs), for glass panes.</summary>
        public static Mesh QuadMesh => quadMesh != null ? quadMesh : (quadMesh = PrimitiveMesh(PrimitiveType.Quad));

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject probe = GameObject.CreatePrimitive(type);
            Mesh mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(probe);
            return mesh;
        }
    }
}
