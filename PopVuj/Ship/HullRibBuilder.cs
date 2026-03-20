// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Procedural hull mesh builder from rib definitions.
    ///
    /// Generates:
    ///   1. Hull exterior mesh (lofted between rib cross-sections)
    ///   2. Deck floor planes (one per floor per rib section)
    ///   3. Metadata for DeckLayout grid generation
    ///
    /// Cross-section sampling:
    ///   Each rib curve is sampled at N points around the half-section
    ///   (keel to port rail), then mirrored for starboard.
    ///   Adjacent rib samples are connected with quads → triangles.
    /// </summary>
    public static class HullRibBuilder
    {
        private const int SAMPLES_PER_SIDE = 8;

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build hull mesh and deck floor objects from rib definitions.
        /// Attaches everything as children of shipRoot.
        /// Returns hull extents for ShipHull buoyancy configuration.
        /// </summary>
        public static HullExtents Build(GameObject shipRoot, RibDef[] ribs, Shader shader, bool isFlagship)
        {
            if (ribs == null || ribs.Length < 2)
                return HullExtents.Default;

            // Sort ribs by Z (stern to bow)
            var sorted = new RibDef[ribs.Length];
            System.Array.Copy(ribs, sorted, ribs.Length);
            System.Array.Sort(sorted, (a, b) => a.ZOffset.CompareTo(b.ZOffset));

            // Build hull mesh
            var hullGo = BuildHullMesh(shipRoot, sorted, shader);

            // Build deck floor planes
            BuildDeckFloors(shipRoot, sorted, shader, isFlagship);

            // Compute extents for buoyancy
            return ComputeExtents(sorted);
        }

        /// <summary>
        /// Derive hull extents from rib array (for ShipHull sample points).
        /// </summary>
        public static HullExtents ComputeExtents(RibDef[] sortedRibs)
        {
            if (sortedRibs == null || sortedRibs.Length == 0)
                return HullExtents.Default;

            float maxBeam = 0f;
            float bowZ = sortedRibs[sortedRibs.Length - 1].ZOffset;
            float sternZ = sortedRibs[0].ZOffset;

            for (int i = 0; i < sortedRibs.Length; i++)
                if (sortedRibs[i].Width > maxBeam)
                    maxBeam = sortedRibs[i].Width;

            return new HullExtents
            {
                BowOffset = bowZ,
                SternOffset = -sternZ, // ShipHull uses positive stern offset
                BeamHalf = maxBeam * 0.5f,
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // HULL MESH
        // ═══════════════════════════════════════════════════════════════

        static GameObject BuildHullMesh(GameObject parent, RibDef[] ribs, Shader shader)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Sample each rib's cross-section
            var ribRings = new Vector3[ribs.Length][];
            for (int r = 0; r < ribs.Length; r++)
                ribRings[r] = SampleRibRing(ribs[r]);

            // Loft: connect adjacent rib rings with quads
            int ringSize = ribRings[0].Length;
            for (int r = 0; r < ribs.Length - 1; r++)
            {
                int baseIdx = verts.Count;
                verts.AddRange(ribRings[r]);
                verts.AddRange(ribRings[r + 1]);

                for (int i = 0; i < ringSize - 1; i++)
                {
                    int a = baseIdx + i;
                    int b = baseIdx + i + 1;
                    int c = baseIdx + ringSize + i;
                    int d = baseIdx + ringSize + i + 1;

                    // Two triangles per quad (winding for outward-facing normals)
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }

            // Cap bow and stern (simple fan from center)
            CapEnd(verts, tris, ribRings[0], false);                           // Stern cap
            CapEnd(verts, tris, ribRings[ribRings.Length - 1], true);           // Bow cap

            var mesh = new Mesh();
            mesh.name = "Hull";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Hull");
            go.transform.SetParent(parent.transform, false);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = new Color(0.35f, 0.22f, 0.12f); // DARK_WOOD
                mr.material = mat;
            }

            // Mesh collider for raycast selection
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            return go;
        }

        /// <summary>
        /// Sample a full rib ring: starboard side (mirrored) + keel + port side.
        /// Returns vertices in local ship space for one cross-section.
        /// Ring order: starboard rail → keel → port rail (continuous loop).
        /// </summary>
        static Vector3[] SampleRibRing(RibDef rib)
        {
            // Sample port side: keel → port rail
            var portHalf = SampleHalfSection(rib);

            // Full ring: starboard (mirrored, reversed) + keel + port
            int n = portHalf.Length;
            int ringSize = n * 2 - 1; // share keel point
            var ring = new Vector3[ringSize];

            // Starboard side (mirror X, reverse order)
            for (int i = 0; i < n; i++)
            {
                var p = portHalf[n - 1 - i];
                ring[i] = new Vector3(-p.x, p.y, rib.ZOffset);
            }

            // Port side (skip keel, already included from starboard)
            for (int i = 1; i < n; i++)
            {
                ring[n - 1 + i] = new Vector3(portHalf[i].x, portHalf[i].y, rib.ZOffset);
            }

            return ring;
        }

        /// <summary>
        /// Sample half cross-section from keel (bottom center) to port rail (top side).
        /// Returns in local XY (X=athwartship, Y=vertical).
        /// pts[0] = keel (0, -height), pts[N] = rail (halfBeam, 0).
        /// </summary>
        static Vector2[] SampleHalfSection(RibDef rib)
        {
            int n = SAMPLES_PER_SIDE + 1;
            var pts = new Vector2[n];
            float hw = rib.Width * 0.5f;
            float h = rib.Height;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SAMPLES_PER_SIDE;

                switch (rib.CurveType)
                {
                    case RibCurveType.SharpV:
                        pts[i] = new Vector2(t * hw, Mathf.Lerp(-h, 0f, t));
                        break;

                    case RibCurveType.Round:
                        // Quarter-ellipse: (0,-h) → (hw, 0)
                        float a = t * Mathf.PI * 0.5f;
                        pts[i] = new Vector2(
                            Mathf.Sin(a) * hw,
                            -h + Mathf.Sin(a) * h);
                        break;

                    case RibCurveType.Flat:
                        // Flat bottom segment, then straight sides up
                        if (t < 0.35f)
                        {
                            pts[i] = new Vector2((t / 0.35f) * hw * 0.85f, -h);
                        }
                        else
                        {
                            float s = (t - 0.35f) / 0.65f;
                            pts[i] = new Vector2(
                                Mathf.Lerp(hw * 0.85f, hw, s),
                                Mathf.Lerp(-h, 0f, s));
                        }
                        break;

                    case RibCurveType.Tumblehome:
                        // Round below waterline, narrows inward above
                        if (t < 0.6f)
                        {
                            float s = t / 0.6f;
                            float a2 = s * Mathf.PI * 0.5f;
                            pts[i] = new Vector2(
                                Mathf.Sin(a2) * hw * 1.08f,
                                -h + s * h * 0.6f);
                        }
                        else
                        {
                            float s = (t - 0.6f) / 0.4f;
                            pts[i] = new Vector2(
                                hw * (1.08f - s * 0.2f),
                                Mathf.Lerp(-h * 0.4f, 0f, s));
                        }
                        break;

                    default:
                        pts[i] = new Vector2(t * hw, Mathf.Lerp(-h, 0f, t));
                        break;
                }
            }
            return pts;
        }

        /// <summary>Cap a hull end (bow or stern) with a triangle fan.</summary>
        static void CapEnd(List<Vector3> verts, List<int> tris, Vector3[] ring, bool flipWinding)
        {
            // Center point = average of ring
            Vector3 center = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) center += ring[i];
            center /= ring.Length;

            int centerIdx = verts.Count;
            verts.Add(center);

            int baseIdx = verts.Count;
            verts.AddRange(ring);

            for (int i = 0; i < ring.Length - 1; i++)
            {
                int a = centerIdx;
                int b = baseIdx + i;
                int c = baseIdx + i + 1;

                if (flipWinding)
                { tris.Add(a); tris.Add(c); tris.Add(b); }
                else
                { tris.Add(a); tris.Add(b); tris.Add(c); }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DECK FLOORS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build visible deck floor planes between adjacent ribs.
        /// Each floor at each rib section becomes a named child GameObject.
        /// </summary>
        static void BuildDeckFloors(GameObject parent, RibDef[] ribs, Shader shader, bool isFlagship)
        {
            // Compute global floor heights from the tallest rib.
            // All floors at index f share the same Y across the entire hull.
            int maxFloors = 0;
            float maxHeight = 0f;
            for (int r = 0; r < ribs.Length; r++)
            {
                if (ribs[r].FloorCount > maxFloors) maxFloors = ribs[r].FloorCount;
                if (ribs[r].Height > maxHeight) maxHeight = ribs[r].Height;
            }

            if (maxFloors == 0) return;

            float floorSpacing = maxHeight / maxFloors;
            float[] globalFloorY = new float[maxFloors];
            for (int f = 0; f < maxFloors; f++)
                globalFloorY[f] = -maxHeight + (f + 1) * floorSpacing;

            for (int r = 0; r < ribs.Length - 1; r++)
            {
                var aftRib = ribs[r];
                var foreRib = ribs[r + 1];

                // Use the minimum floor count between adjacent ribs
                int floorCount = Mathf.Min(aftRib.FloorCount, foreRib.FloorCount);
                if (floorCount == 0) continue;

                for (int f = 0; f < floorCount; f++)
                {
                    var fn = aftRib.Floors[f].Function;
                    if (fn == FloorFunction.None) continue;

                    float flatY = globalFloorY[f];

                    // Floor width = rib beam at this floor fraction (never narrower than 50% beam)
                    float aftHW = FloorHalfWidth(aftRib, f);
                    float foreHW = FloorHalfWidth(foreRib, f);

                    // Build quad
                    var go = CreateFloorQuad(
                        $"Floor_{r}_{f}_{fn}",
                        aftRib.ZOffset, foreRib.ZOffset,
                        flatY, flatY,
                        aftHW, foreHW,
                        fn, shader, isFlagship);

                    go.transform.SetParent(parent.transform, false);
                }
            }
        }

        /// <summary>World-space Y for a floor index within a rib.</summary>
        static float FloorWorldY(RibDef rib, int floorIndex)
        {
            if (rib.FloorCount <= 1) return 0f;
            float floorHeight = rib.Height / rib.FloorCount;
            return -rib.Height + (floorIndex + 1) * floorHeight;
        }

        /// <summary>
        /// Half-width for a floor within a rib.
        /// Uses the floor's fractional height within the hull, clamped to
        /// at least 50% of beam so decks never collapse to a sliver.
        /// </summary>
        static float FloorHalfWidth(RibDef rib, int floorIndex)
        {
            float t = rib.FloorCount <= 1 ? 1f
                : (float)(floorIndex + 1) / rib.FloorCount;
            // Lower floors are narrower but never below 50% beam
            float fraction = Mathf.Lerp(0.5f, 1f, t);
            return rib.Width * 0.5f * fraction;
        }

        static GameObject CreateFloorQuad(
            string name, float aftZ, float foreZ,
            float aftY, float foreY, float aftHW, float foreHW,
            FloorFunction fn, Shader shader, bool isFlagship)
        {
            var mesh = new Mesh();
            mesh.name = name;

            mesh.vertices = new[]
            {
                new Vector3(-aftHW,  aftY,  aftZ),   // aft port
                new Vector3( aftHW,  aftY,  aftZ),   // aft starboard
                new Vector3(-foreHW, foreY, foreZ),   // fore port
                new Vector3( foreHW, foreY, foreZ),   // fore starboard
            };

            mesh.triangles = new[]
            {
                0, 2, 1,
                1, 2, 3,
            };

            mesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1),
            };

            mesh.RecalculateNormals();

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().mesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.color = FloorColor(fn, isFlagship);
                mr.material = mat;
            }

            // Collider for selection
            go.AddComponent<MeshCollider>().sharedMesh = mesh;

            return go;
        }

        static Color FloorColor(FloorFunction fn, bool isFlagship)
        {
            switch (fn)
            {
                case FloorFunction.OpenDeck:     return new Color(0.55f, 0.40f, 0.25f);
                case FloorFunction.Cannons:      return new Color(0.25f, 0.25f, 0.28f);
                case FloorFunction.Hammocks:     return new Color(0.50f, 0.42f, 0.32f);
                case FloorFunction.Kitchen:      return new Color(0.45f, 0.30f, 0.18f);
                case FloorFunction.CargoBay:     return new Color(0.40f, 0.32f, 0.18f);
                case FloorFunction.Magazine:     return new Color(0.30f, 0.25f, 0.20f);
                case FloorFunction.ChartRoom:    return new Color(0.45f, 0.35f, 0.25f);
                case FloorFunction.CaptainCabin: return isFlagship
                    ? new Color(0.50f, 0.20f, 0.15f)
                    : new Color(0.48f, 0.38f, 0.28f);
                case FloorFunction.Workshop:     return new Color(0.42f, 0.35f, 0.22f);
                case FloorFunction.SailLocker:   return new Color(0.50f, 0.45f, 0.35f);
                case FloorFunction.Brig:         return new Color(0.28f, 0.28f, 0.25f);
                default:                         return new Color(0.55f, 0.40f, 0.25f);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FLOOR → STATION MAPPING (for DeckLayout integration)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Map a FloorFunction to the primary crew StationType.
        /// Used by DeckLayout to place stations on the pathfinding grid.
        /// </summary>
        public static Crew.StationType FloorToStation(FloorFunction fn)
        {
            switch (fn)
            {
                case FloorFunction.Cannons:      return Crew.StationType.Gun;
                case FloorFunction.Hammocks:      return Crew.StationType.Bunk;
                case FloorFunction.Kitchen:       return Crew.StationType.Galley;
                case FloorFunction.CargoBay:      return Crew.StationType.CargoHold;
                case FloorFunction.ChartRoom:     return Crew.StationType.ChartRoom;
                case FloorFunction.Workshop:      return Crew.StationType.Workshop;
                case FloorFunction.CaptainCabin:  return Crew.StationType.ChartRoom;
                case FloorFunction.Magazine:      return Crew.StationType.CargoHold;
                case FloorFunction.SailLocker:    return Crew.StationType.Rigging;
                default:                          return Crew.StationType.None;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTO-GENERATE RIBS FROM LEGACY HULL DIMENSIONS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Generate default ribs from hull bow/stern/beam dimensions.
        /// Used as fallback when a blueprint doesn't define explicit ribs.
        /// </summary>
        public static RibDef[] GenerateDefaultRibs(
            float bowOffset, float sternOffset, float beamHalf,
            RibCurveType curveType, int floorCount)
        {
            float totalLength = bowOffset + sternOffset;
            int ribCount = Mathf.Max(4, Mathf.CeilToInt(totalLength / 1.5f));
            float height = 0.8f + floorCount * 0.6f;

            var ribs = new RibDef[ribCount];
            for (int i = 0; i < ribCount; i++)
            {
                float t = (float)i / (ribCount - 1);
                float z = Mathf.Lerp(-sternOffset, bowOffset, t);

                // Taper toward bow and stern
                float taper;
                if (t < 0.15f)
                    taper = t / 0.15f;
                else if (t > 0.85f)
                    taper = (1f - t) / 0.15f;
                else
                    taper = 1f;

                // Bow tapers more aggressively
                if (t > 0.9f)
                    taper *= 0.5f;

                float width = beamHalf * 2f * Mathf.Clamp01(taper);
                float ribHeight = height * Mathf.Clamp01(taper * 1.2f);

                // Floors: reduce toward ends
                int floors = Mathf.Max(1, Mathf.RoundToInt(floorCount * taper));
                var floorDefs = new FloorDef[floors];
                for (int f = 0; f < floors; f++)
                {
                    if (f == floors - 1)
                        floorDefs[f] = new FloorDef(FloorFunction.OpenDeck);
                    else if (f == 0)
                        floorDefs[f] = new FloorDef(FloorFunction.CargoBay);
                    else
                        floorDefs[f] = new FloorDef(FloorFunction.Hammocks);
                }

                ribs[i] = new RibDef(z, width, ribHeight, curveType, floorDefs);
            }

            return ribs;
        }
    }

    /// <summary>
    /// Computed hull extents from rib definitions.
    /// Used to configure ShipHull buoyancy sample points.
    /// </summary>
    public struct HullExtents
    {
        public float BowOffset;
        public float SternOffset;
        public float BeamHalf;

        public static readonly HullExtents Default = new HullExtents
        {
            BowOffset = 4f,
            SternOffset = 3.5f,
            BeamHalf = 1.5f,
        };
    }
}
