// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;

namespace PopVuj.Crew
{
    /// <summary>
    /// Meshes for each articulable body part, plus child-transform positions.
    /// </summary>
    public struct MinionRig
    {
        public Mesh Body;           // head + torso (pivot at feet center)
        public Mesh RightArm;       // pivot at shoulder (or connected arms)
        public Mesh LeftArm;        // pivot at shoulder (null if ConnectedArms)
        public Mesh RightLeg;       // pivot at hip
        public Mesh LeftLeg;        // pivot at hip
        public bool ConnectedArms;  // true → RightArm is single connected-arm mesh, LeftArm is null

        // Local positions relative to root (feet at origin)
        public Vector3 RightArmPos; // shoulder (or chest center for connected)
        public Vector3 LeftArmPos;
        public Vector3 RightLegPos; // hip
        public Vector3 LeftLegPos;
    }

    /// <summary>
    /// Builds Minecraft Minion body-part meshes with correct UV mapping for
    /// the standard 64×64 skin layout. Each limb has its pivot at the
    /// rotation joint (shoulder / hip) for procedural walk animation.
    ///
    /// Winding order: clockwise (Unity front-face convention).
    /// Model forward: +Z (matches FacingAngle=0 → +Z away).
    /// </summary>
    public static class MinionModelBuilder
    {
        // ── Skeleton (Steve) ──
        private const float SkeletonPixelHeight = 32f;

        // ── Villager ──
        private const float VillagerPixelHeight = 120f; // legs 32 + torso 48 + head 40

        // ══════════════════════════════════════════════════════════════
        // SKELETON RIG  (Steve shape, 64×64 skin)
        // ══════════════════════════════════════════════════════════════

        public static MinionRig BuildSkeletonRig(float targetHeight = 1.0f)
        {
            float s = targetHeight / SkeletonPixelHeight;
            const float texW = 64f, texH = 64f;

            // ── Pivot positions (pixel coords, feet-at-origin) ──
            // After feet offset of +4px, final pixel Y coordinates:
            //   Feet bottom: 0,  Hip: 12,  Torso bottom: 12,  Shoulder: 24,  Head top: 32
            float hipY      = 12f * s;
            float shoulderY = 24f * s;

            var rig = new MinionRig
            {
                RightArmPos = new Vector3(-6f * s, shoulderY, 0f),
                LeftArmPos  = new Vector3( 6f * s, shoulderY, 0f),
                RightLegPos = new Vector3(-2f * s, hipY, 0f),
                LeftLegPos  = new Vector3( 2f * s, hipY, 0f),
            };

            // ── Body (head 8×8×8 + torso 8×12×4) — pivot at feet ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();

                // Head at center (0, 28, 0) in feet-origin pixel space
                AddBox(v, u, t, s, 0f, 28f, 0f, 8f, 8f, 8f, texW, texH,
                    new RectInt(0,  8, 8, 8),   // right (+X)
                    new RectInt(16, 8, 8, 8),   // left  (-X)
                    new RectInt(8,  8, 8, 8),   // front (+Z) — the face
                    new RectInt(24, 8, 8, 8),   // back  (-Z)
                    new RectInt(8,  0, 8, 8),   // top
                    new RectInt(16, 0, 8, 8));  // bottom

                // Torso at center (0, 18, 0)
                AddBox(v, u, t, s, 0f, 18f, 0f, 8f, 12f, 4f, texW, texH,
                    new RectInt(16, 20, 4, 12),  // right
                    new RectInt(28, 20, 4, 12),  // left
                    new RectInt(20, 20, 8, 12),  // front
                    new RectInt(32, 20, 8, 12),  // back
                    new RectInt(20, 16, 8, 4),   // top
                    new RectInt(28, 16, 8, 4));  // bottom

                rig.Body = MakeMesh("MinionBody", v, u, t);
            }

            // ── Right Arm 4×12×4 — pivot at shoulder (local origin) ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                // Center at local (0, -6, 0) → extends from Y=0 (shoulder) to Y=-12
                AddBox(v, u, t, s, 0f, -6f, 0f, 4f, 12f, 4f, texW, texH,
                    new RectInt(40, 20, 4, 12),
                    new RectInt(48, 20, 4, 12),
                    new RectInt(44, 20, 4, 12),
                    new RectInt(52, 20, 4, 12),
                    new RectInt(44, 16, 4, 4),
                    new RectInt(48, 16, 4, 4));
                rig.RightArm = MakeMesh("MinionRightArm", v, u, t);
            }

            // ── Left Arm 4×12×4 — pivot at shoulder ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                AddBox(v, u, t, s, 0f, -6f, 0f, 4f, 12f, 4f, texW, texH,
                    new RectInt(32, 52, 4, 12),
                    new RectInt(40, 52, 4, 12),
                    new RectInt(36, 52, 4, 12),
                    new RectInt(44, 52, 4, 12),
                    new RectInt(36, 48, 4, 4),
                    new RectInt(40, 48, 4, 4));
                rig.LeftArm = MakeMesh("MinionLeftArm", v, u, t);
            }

            // ── Right Leg 4×12×4 — pivot at hip ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                AddBox(v, u, t, s, 0f, -6f, 0f, 4f, 12f, 4f, texW, texH,
                    new RectInt(0,  20, 4, 12),
                    new RectInt(8,  20, 4, 12),
                    new RectInt(4,  20, 4, 12),
                    new RectInt(12, 20, 4, 12),
                    new RectInt(4,  16, 4, 4),
                    new RectInt(8,  16, 4, 4));
                rig.RightLeg = MakeMesh("MinionRightLeg", v, u, t);
            }

            // ── Left Leg 4×12×4 — pivot at hip ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                AddBox(v, u, t, s, 0f, -6f, 0f, 4f, 12f, 4f, texW, texH,
                    new RectInt(16, 52, 4, 12),
                    new RectInt(24, 52, 4, 12),
                    new RectInt(20, 52, 4, 12),
                    new RectInt(28, 52, 4, 12),
                    new RectInt(20, 48, 4, 4),
                    new RectInt(24, 48, 4, 4));
                rig.LeftLeg = MakeMesh("MinionLeftLeg", v, u, t);
            }

            return rig;
        }

        // ══════════════════════════════════════════════════════════════
        // VILLAGER RIG  (connected arms, 256×256 skin)
        // ══════════════════════════════════════════════════════════════

        public static MinionRig BuildVillagerRig(float targetHeight = 1.0f)
        {
            float s = targetHeight / VillagerPixelHeight;
            const float texW = 256f, texH = 256f;

            // Pixel Y layout (feet at origin):
            //   Feet: 0,  Hip: 32,  Torso bottom: 32,  Shoulder: 80,  Head top: 120
            float hipY      = 32f * s;
            float shoulderY = 80f * s;

            var rig = new MinionRig
            {
                ConnectedArms = true,
                RightArmPos = new Vector3(0f, shoulderY, 4f * s + 0.03f), // centered, forward of chest
                LeftArmPos  = Vector3.zero, // unused
                RightLegPos = new Vector3(-8f * s, hipY, 0f),
                LeftLegPos  = new Vector3( 8f * s, hipY, 0f),
            };

            // ── Body (head 32×40×32 + torso 32×48×24) — pivot at feet ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();

                // Head center at (0, 100, 0) — 80 + 40/2
                // UV origin (0,0) × 4, box W=32 H=40 D=32
                AddBox(v, u, t, s, 0f, 100f, 0f, 32f, 40f, 32f, texW, texH,
                    new RectInt(0,   32, 32, 40),  // right  (+X)
                    new RectInt(64,  32, 32, 40),  // left   (-X)
                    new RectInt(32,  32, 32, 40),  // front  (+Z) — the face
                    new RectInt(96,  32, 32, 40),  // back   (-Z)
                    new RectInt(32,   0, 32, 32),  // top
                    new RectInt(64,   0, 32, 32)); // bottom

                // Torso center at (0, 56, 0) — 32 + 48/2
                // UV origin (16,20)×4 = (64,80), box W=32 H=48 D=24
                AddBox(v, u, t, s, 0f, 56f, 0f, 32f, 48f, 24f, texW, texH,
                    new RectInt(64,  104, 24, 48),  // right  (+X)
                    new RectInt(120, 104, 24, 48),  // left   (-X)
                    new RectInt(88,  104, 32, 48),  // front  (+Z)
                    new RectInt(144, 104, 32, 48),  // back   (-Z)
                    new RectInt(88,   80, 32, 24),  // top
                    new RectInt(120,  80, 32, 24)); // bottom

                rig.Body = MakeMesh("VillagerBody", v, u, t);
            }

            // ── Connected Arms 32×16×16 — pivot at shoulder center ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                // Center at local (0, -8, 0) — extends from Y=0 (shoulder) to Y=-16
                // UV origin (40,38)×4 = (160,152), box W=32 H=16 D=16
                AddBox(v, u, t, s, 0f, -8f, 0f, 32f, 16f, 16f, texW, texH,
                    new RectInt(160, 168, 16, 16),  // right  (+X)
                    new RectInt(208, 168, 16, 16),  // left   (-X)
                    new RectInt(176, 168, 32, 16),  // front  (+Z)
                    new RectInt(224, 168, 32, 16),  // back   (-Z)
                    new RectInt(176, 152, 32, 16),  // top
                    new RectInt(208, 152, 32, 16)); // bottom
                rig.RightArm = MakeMesh("VillagerArms", v, u, t);
                rig.LeftArm = null;
            }

            // ── Right Leg 16×32×16 — pivot at hip ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                // Center at local (0, -16, 0) — extends from Y=0 (hip) to Y=-32
                // UV origin (0,22)×4 = (0,88), box W=16 H=32 D=16
                AddBox(v, u, t, s, 0f, -16f, 0f, 16f, 32f, 16f, texW, texH,
                    new RectInt(0,  104, 16, 32),  // right  (+X)
                    new RectInt(32, 104, 16, 32),  // left   (-X)
                    new RectInt(16, 104, 16, 32),  // front  (+Z)
                    new RectInt(48, 104, 16, 32),  // back   (-Z)
                    new RectInt(16,  88, 16, 16),  // top
                    new RectInt(32,  88, 16, 16)); // bottom
                rig.RightLeg = MakeMesh("VillagerRightLeg", v, u, t);
            }

            // ── Left Leg 16×32×16 — pivot at hip ──
            {
                var v = new List<Vector3>();
                var u = new List<Vector2>();
                var t = new List<int>();
                // Same UV as right leg (standard villager uses mirrored UVs)
                AddBox(v, u, t, s, 0f, -16f, 0f, 16f, 32f, 16f, texW, texH,
                    new RectInt(0,  104, 16, 32),  // right  (+X)
                    new RectInt(32, 104, 16, 32),  // left   (-X)
                    new RectInt(16, 104, 16, 32),  // front  (+Z)
                    new RectInt(48, 104, 16, 32),  // back   (-Z)
                    new RectInt(16,  88, 16, 16),  // top
                    new RectInt(32,  88, 16, 16)); // bottom
                rig.LeftLeg = MakeMesh("VillagerLeftLeg", v, u, t);
            }

            return rig;
        }

        private static Mesh MakeMesh(string name, List<Vector3> v, List<Vector2> u, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetUVs(0, u);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Adds a box (6 faces, 24 verts, 12 tris) to the mesh buffers.
        /// Coordinates are in pixel space × scale.
        /// UV rects are in pixel coordinates on the 64×64 skin.
        /// Arguments: right (+X), left (-X), front (+Z), back (-Z), top (+Y), bottom (-Y).
        /// </summary>
        private static void AddBox(
            List<Vector3> v, List<Vector2> u, List<int> t,
            float s,
            float cx, float cy, float cz,
            float sx, float sy, float sz,
            float texW, float texH,
            RectInt uvR, RectInt uvL,
            RectInt uvF, RectInt uvBk,
            RectInt uvT, RectInt uvBt)
        {
            float hx = sx * 0.5f * s, hy = sy * 0.5f * s, hz = sz * 0.5f * s;
            float px = cx * s, py = cy * s, pz = cz * s;

            // +X face (right)
            AddQuad(v, u, t,
                new Vector3(px+hx, py-hy, pz+hz),
                new Vector3(px+hx, py+hy, pz+hz),
                new Vector3(px+hx, py+hy, pz-hz),
                new Vector3(px+hx, py-hy, pz-hz), uvR, texW, texH);

            // -X face (left)
            AddQuad(v, u, t,
                new Vector3(px-hx, py-hy, pz-hz),
                new Vector3(px-hx, py+hy, pz-hz),
                new Vector3(px-hx, py+hy, pz+hz),
                new Vector3(px-hx, py-hy, pz+hz), uvL, texW, texH);

            // +Y face (top)
            AddQuad(v, u, t,
                new Vector3(px-hx, py+hy, pz-hz),
                new Vector3(px+hx, py+hy, pz-hz),
                new Vector3(px+hx, py+hy, pz+hz),
                new Vector3(px-hx, py+hy, pz+hz), uvT, texW, texH);

            // -Y face (bottom)
            AddQuad(v, u, t,
                new Vector3(px-hx, py-hy, pz+hz),
                new Vector3(px+hx, py-hy, pz+hz),
                new Vector3(px+hx, py-hy, pz-hz),
                new Vector3(px-hx, py-hy, pz-hz), uvBt, texW, texH);

            // +Z face (front — Minion's face)
            AddQuad(v, u, t,
                new Vector3(px-hx, py-hy, pz+hz),
                new Vector3(px-hx, py+hy, pz+hz),
                new Vector3(px+hx, py+hy, pz+hz),
                new Vector3(px+hx, py-hy, pz+hz), uvF, texW, texH);

            // -Z face (back)
            AddQuad(v, u, t,
                new Vector3(px+hx, py-hy, pz-hz),
                new Vector3(px+hx, py+hy, pz-hz),
                new Vector3(px-hx, py+hy, pz-hz),
                new Vector3(px-hx, py-hy, pz-hz), uvBk, texW, texH);
        }

        /// <summary>
        /// Adds a quad with outward-facing normal.
        /// Vertices are the four corners of the face. Triangle winding is
        /// reversed from BL→TL→TR to produce outward normals, and UVs are
        /// horizontally flipped to compensate so textures appear non-mirrored.
        /// </summary>
        private static void AddQuad(
            List<Vector3> v, List<Vector2> u, List<int> t,
            Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br,
            RectInt uvPx, float texW, float texH)
        {
            int i = v.Count;
            v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);

            // Pixel rect → UV (Minecraft Y=0 is top, Unity V=0 is bottom → flip Y)
            float u0 = uvPx.x / texW;
            float u1 = (uvPx.x + uvPx.width) / texW;
            float v0 = 1f - (uvPx.y + uvPx.height) / texH;
            float v1 = 1f - uvPx.y / texH;

            // Swap u0/u1 to match reversed winding (prevents horizontal mirror)
            u.Add(new Vector2(u1, v0)); // BL
            u.Add(new Vector2(u1, v1)); // TL
            u.Add(new Vector2(u0, v1)); // TR
            u.Add(new Vector2(u0, v0)); // BR

            // Reversed winding → outward normals
            t.Add(i); t.Add(i+2); t.Add(i+1);
            t.Add(i); t.Add(i+3); t.Add(i+2);
        }
    }
}
