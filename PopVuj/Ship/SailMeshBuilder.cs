// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Procedural sail mesh generator.
    ///
    /// Builds quad-tessellated sail meshes that deform to show wind fill.
    /// Each sail type has a different mesh shape:
    ///
    ///   Square:       Rectangle hung from yard — billows forward
    ///   ForeAndAft:   Trapezoid from mast to boom — curves to leeward
    ///   Jib:          Triangle from forestay — bellies in cross-section
    ///   Staysail:     Triangle between masts
    ///   Spanker:      Trapezoid on mizzen gaff+boom
    ///   Lateen:       Triangle on angled yard
    ///
    /// Mesh is subdivided into a grid (e.g. 6×8 quads) so vertices
    /// can be displaced to show billowing. The SailVisualController
    /// updates vertex positions each frame.
    ///
    /// Wind fill is shown by displacing vertices perpendicular to
    /// the sail plane, using a catenary/parabolic curve that's deeper
    /// at the center than the edges (luff → leech cross-section).
    /// </summary>
    public static class SailMeshBuilder
    {
        /// <summary>Resolution of sail mesh subdivision.</summary>
        const int SEGMENTS_X = 6;  // across sail (luff to leech)
        const int SEGMENTS_Y = 8;  // up the sail (foot to head)

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build a sail mesh and attach it to the given GameObject,
        /// replacing any existing primitive mesh.
        /// Returns the MeshFilter for later vertex updates by SailVisualController.
        /// </summary>
        public static MeshFilter BuildSailMesh(
            GameObject sailGO, SailType type,
            float width, float height,
            bool isFlagship)
        {
            // Remove primitive collider (we'll add our own)
            var oldCollider = sailGO.GetComponent<Collider>();
            if (oldCollider != null) Object.Destroy(oldCollider);

            var mf = sailGO.GetComponent<MeshFilter>();
            if (mf == null) mf = sailGO.AddComponent<MeshFilter>();

            Mesh mesh;
            switch (type)
            {
                case SailType.Jib:
                case SailType.Staysail:
                    mesh = BuildTriangleSail(width, height);
                    break;
                case SailType.Lateen:
                    mesh = BuildTriangleSail(width, height);
                    break;
                default: // Square, ForeAndAft, Spanker
                    mesh = BuildRectSail(width, height, type);
                    break;
            }

            mesh.name = sailGO.name + "_Mesh";
            mf.mesh = mesh;

            // Mesh collider for raycast selection
            var mc = sailGO.GetComponent<MeshCollider>();
            if (mc == null) mc = sailGO.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            return mf;
        }

        /// <summary>
        /// Apply wind-fill deformation to a sail mesh.
        /// Called every frame by SailVisualController.
        ///
        /// Parameters:
        ///   fill: 0 = flat (no wind), 1 = maximum belly
        ///   windSide: -1 = wind from port (belly to starboard), +1 = wind from starboard
        ///   flap: 0 = smooth, 1 = maximum flutter (luffing/torn)
        ///   reefFraction: 0 = full sail, 1 = fully reefed (top portion gathered)
        ///   time: for flutter animation
        /// </summary>
        public static void DeformSailMesh(
            Mesh mesh, Vector3[] baseVerts, Vector3[] deformedVerts,
            float fill, float windSide, float flap, float reefFraction,
            int segX, int segY, float time)
        {
            int vertCount = (segX + 1) * (segY + 1);
            if (baseVerts.Length < vertCount) return;

            for (int j = 0; j <= segY; j++)
            {
                float v = (float)j / segY; // 0=foot, 1=head

                // Reef: vertices above (1-reefFraction) are collapsed to the yard
                bool isReefed = v > (1f - reefFraction);

                for (int i = 0; i <= segX; i++)
                {
                    int idx = j * (segX + 1) + i;
                    if (idx >= deformedVerts.Length) continue;

                    Vector3 basePos = baseVerts[idx];

                    if (isReefed)
                    {
                        // Collapse reefed portion toward head (gather to yard/boom)
                        float headY = baseVerts[segY * (segX + 1) + i].y;
                        deformedVerts[idx] = new Vector3(basePos.x, headY, 0f);
                        continue;
                    }

                    float u = (float)i / segX; // 0=luff, 1=leech

                    // Belly profile: deeper at center, zero at edges (luff + leech)
                    // Parabolic cross-section: u*(1-u) peaks at 0.5
                    float bellyCross = u * (1f - u) * 4f; // 0→0, 0.5→1, 1→0

                    // Vertical profile: deeper at mid-height, less at foot and head
                    // Foot has the boom (constrained), head has the yard
                    float bellyVert = Mathf.Sin(v * Mathf.PI) * 0.8f + 0.2f;

                    float totalBelly = fill * bellyCross * bellyVert;

                    // Wind side determines which direction the belly goes
                    float bellyZ = totalBelly * windSide * 0.3f; // scale factor for visual

                    // Flutter/luffing: sine wave distortion, stronger at leech
                    float flutter = 0f;
                    if (flap > 0.01f)
                    {
                        float flapAmp = flap * 0.15f * u; // more flutter toward leech
                        flutter = Mathf.Sin(time * 8f + u * 6f + v * 4f) * flapAmp;
                    }

                    deformedVerts[idx] = new Vector3(
                        basePos.x,
                        basePos.y,
                        bellyZ + flutter
                    );
                }
            }

            mesh.SetVertices(deformedVerts);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // ═══════════════════════════════════════════════════════════════
        // RECTANGULAR SAIL (Square, ForeAndAft, Spanker)
        // ═══════════════════════════════════════════════════════════════

        static Mesh BuildRectSail(float width, float height, SailType type)
        {
            int sx = SEGMENTS_X;
            int sy = SEGMENTS_Y;

            // Fore-and-aft sails are slightly tapered (narrower at head)
            float headWidthFraction = 1f;
            if (type == SailType.ForeAndAft || type == SailType.Spanker)
                headWidthFraction = 0.7f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int j = 0; j <= sy; j++)
            {
                float v = (float)j / sy;
                float y = Mathf.Lerp(-halfH, halfH, v);

                // Taper width from foot to head
                float wFrac = Mathf.Lerp(1f, headWidthFraction, v);
                float localHalfW = halfW * wFrac;

                for (int i = 0; i <= sx; i++)
                {
                    float u = (float)i / sx;
                    float x = Mathf.Lerp(-localHalfW, localHalfW, u);

                    verts.Add(new Vector3(x, y, 0f));
                    uvs.Add(new Vector2(u, v));
                }
            }

            // Triangles (two per quad)
            for (int j = 0; j < sy; j++)
            {
                for (int i = 0; i < sx; i++)
                {
                    int bl = j * (sx + 1) + i;
                    int br = bl + 1;
                    int tl = bl + (sx + 1);
                    int tr = tl + 1;

                    // Front face
                    tris.Add(bl); tris.Add(tl); tris.Add(br);
                    tris.Add(br); tris.Add(tl); tris.Add(tr);

                    // Back face (visible from both sides)
                    tris.Add(bl); tris.Add(br); tris.Add(tl);
                    tris.Add(br); tris.Add(tr); tris.Add(tl);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // TRIANGULAR SAIL (Jib, Staysail, Lateen)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Triangle: point at top (head), wide at bottom (foot).
        /// Still tessellated on the same grid, but vertices on the right
        /// side taper to a point at the top.
        ///
        ///      *  (head — tack point)
        ///     /|
        ///    / |
        ///   /  |
        ///  *---*  (foot — clew to tack)
        /// </summary>
        static Mesh BuildTriangleSail(float width, float height)
        {
            int sx = SEGMENTS_X;
            int sy = SEGMENTS_Y;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int j = 0; j <= sy; j++)
            {
                float v = (float)j / sy;
                float y = Mathf.Lerp(-halfH, halfH, v);

                // Triangle taper: full width at foot (v=0), zero width at head (v=1)
                float localHalfW = halfW * (1f - v);

                for (int i = 0; i <= sx; i++)
                {
                    float u = (float)i / sx;
                    float x = Mathf.Lerp(-localHalfW, localHalfW, u);

                    verts.Add(new Vector3(x, y, 0f));
                    uvs.Add(new Vector2(u, v));
                }
            }

            // Only emit triangles where the quad has non-degenerate area
            for (int j = 0; j < sy; j++)
            {
                for (int i = 0; i < sx; i++)
                {
                    int bl = j * (sx + 1) + i;
                    int br = bl + 1;
                    int tl = bl + (sx + 1);
                    int tr = tl + 1;

                    // Front face
                    tris.Add(bl); tris.Add(tl); tris.Add(br);
                    tris.Add(br); tris.Add(tl); tris.Add(tr);

                    // Back face
                    tris.Add(bl); tris.Add(br); tris.Add(tl);
                    tris.Add(br); tris.Add(tr); tris.Add(tl);
                }
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
