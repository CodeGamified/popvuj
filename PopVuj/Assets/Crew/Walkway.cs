// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using PopVuj.Game;

namespace PopVuj.Crew
{
    /// <summary>
    /// A point on the walkway graph where edges meet.
    /// </summary>
    public class WalkNode
    {
        public int Id;
        public float X, Z;
        public readonly List<WalkEdge> Edges = new List<WalkEdge>();
    }

    /// <summary>
    /// A straight walkable path between two nodes.
    ///
    /// Minions travel from A to B (progress 0 → Length) or B to A (Length → 0).
    /// World position at any progress is linearly interpolated between A and B.
    ///
    /// The perpendicular vector defines the lane-offset direction.
    /// Right-hand traffic: forward travelers (toward B) use negative lane offset,
    /// reverse travelers (toward A) use positive lane offset.
    /// </summary>
    public class WalkEdge
    {
        public int Id;
        public WalkNode A, B;
        public float Length;

        /// <summary>Normalized direction A→B (x = world X, y = world Z).</summary>
        public Vector2 Dir;
        /// <summary>Perpendicular (90° CCW from Dir). Lane offsets apply along this.</summary>
        public Vector2 Perp;

        /// <summary>World-facing when traveling toward B (0 = keep previous).</summary>
        public int ForwardFacing;
        /// <summary>World-facing when traveling toward A (0 = keep previous).</summary>
        public int ReverseFacing;

        public WalkNode Other(WalkNode n) => n == A ? B : A;
        public float ProgressAt(WalkNode n) => n == A ? 0f : Length;

        /// <summary>World position at a given progress (0 = A, Length = B).</summary>
        public Vector2 WorldAt(float progress)
        {
            if (Length < 0.0001f) return new Vector2(A.X, A.Z);
            float t = progress / Length;
            return new Vector2(
                Mathf.Lerp(A.X, B.X, t),
                Mathf.Lerp(A.Z, B.Z, t));
        }

        /// <summary>Project a world point onto the edge, returning clamped progress.</summary>
        public float Project(float worldX, float worldZ)
        {
            float dx = B.X - A.X, dz = B.Z - A.Z;
            float len2 = dx * dx + dz * dz;
            if (len2 < 0.0001f) return 0f;
            float t = ((worldX - A.X) * dx + (worldZ - A.Z) * dz) / len2;
            return Mathf.Clamp(t * Length, 0f, Length);
        }

        /// <summary>Shortest distance from a world point to the edge line segment.</summary>
        public float DistanceTo(float worldX, float worldZ)
        {
            float dx = B.X - A.X, dz = B.Z - A.Z;
            float len2 = dx * dx + dz * dz;
            if (len2 < 0.0001f)
            {
                float px = worldX - A.X, pz = worldZ - A.Z;
                return Mathf.Sqrt(px * px + pz * pz);
            }
            float t = Mathf.Clamp01(((worldX - A.X) * dx + (worldZ - A.Z) * dz) / len2);
            float nx = A.X + t * dx - worldX;
            float nz = A.Z + t * dz - worldZ;
            return Mathf.Sqrt(nx * nx + nz * nz);
        }

        /// <summary>Right-hand traffic lane offset for an edge direction.</summary>
        public static float RightHandLane(int edgeDirection)
            => edgeDirection > 0 ? Minion.RIGHT_LANE_OFFSET : Minion.LEFT_LANE_OFFSET;

        /// <summary>World-facing for a given edge direction.</summary>
        public int GetFacing(int edgeDirection)
            => edgeDirection > 0 ? ForwardFacing : ReverseFacing;

        /// <summary>True if this edge runs primarily along the depth (Z) axis.</summary>
        public bool IsDepthEdge => Mathf.Abs(Dir.x) < 0.1f;
    }

    /// <summary>One step in a planned route: travel along an edge to a target progress.</summary>
    public struct RouteStep
    {
        public WalkEdge Edge;
        public float TargetProgress;
    }

    /// <summary>
    /// Builds and queries the walkway graph for the current city layout.
    ///
    /// The graph is auto-detected from the city grid:
    ///   Road edge  — X-axis at Z = RoadZ, covering all non-pier buildings
    ///   Bridge edge — Z-axis connecting the road to the pier
    ///   Pier edge  — X-axis at Z = PierZ, covering all pier buildings
    ///
    /// Route planning uses BFS on the tree graph — no A* needed,
    /// there are no cycles so the path between any two edges is unique.
    ///
    /// Depth is parameterized: change PierZ for deeper worlds.
    /// </summary>
    public class WalkGraph
    {
        public readonly List<WalkNode> Nodes = new List<WalkNode>();
        public readonly List<WalkEdge> Edges = new List<WalkEdge>();

        /// <summary>Z of the road walkway (road blocks centered at Z=0).</summary>
        public const float RoadZ = 0f;
        /// <summary>Z of the pier walkway. Change this for deeper worlds.</summary>
        public const float PierZ = 1f;

        private int _nextNodeId, _nextEdgeId;

        // ─────────────────────────────────────────────────────────

        private WalkNode AddNode(float x, float z)
        {
            var n = new WalkNode { Id = _nextNodeId++, X = x, Z = z };
            Nodes.Add(n);
            return n;
        }

        private WalkEdge AddEdge(WalkNode a, WalkNode b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            float len = Mathf.Sqrt(dx * dx + dz * dz);

            Vector2 dir, perp;
            int fwd, rev;

            if (len > 0.0001f)
            {
                dir = new Vector2(dx / len, dz / len);
                perp = new Vector2(-dir.y, dir.x);
                if (Mathf.Abs(dir.x) > 0.1f)
                { fwd = dir.x > 0 ? 1 : -1; rev = -fwd; }
                else
                { fwd = 0; rev = 0; }
            }
            else
            {
                dir = Vector2.right; perp = Vector2.up;
                fwd = 1; rev = -1;
            }

            var e = new WalkEdge
            {
                Id = _nextEdgeId++,
                A = a, B = b,
                Length = len,
                Dir = dir, Perp = perp,
                ForwardFacing = fwd,
                ReverseFacing = rev,
            };
            a.Edges.Add(e);
            b.Edges.Add(e);
            Edges.Add(e);
            return e;
        }

        // ─────────────────────────────────────────────────────────

        /// <summary>Rebuild the graph from the current city grid.</summary>
        public void Build(CityGrid city)
        {
            foreach (var n in Nodes) n.Edges.Clear();
            Nodes.Clear();
            Edges.Clear();
            _nextNodeId = 0;
            _nextEdgeId = 0;

            if (city == null) return;

            float cs = CityRenderer.CellSize;

            int roadLeft = -1, roadRight = -1;
            int pierLeft = -1, pierRight = -1;

            for (int i = 0; i < city.Width; i++)
            {
                int origin = city.GetOwner(i);
                if (origin != i || origin < 0) continue;
                var type = city.GetSurface(i);
                if (type == CellType.Empty || type == CellType.Tree) continue;

                int bw = city.GetBuildingWidth(i);
                int end = i + Mathf.Max(bw, 1);

                if (type == CellType.Pier)
                {
                    if (pierLeft < 0 || i < pierLeft) pierLeft = i;
                    if (end > pierRight) pierRight = end;
                }
                else
                {
                    if (roadLeft < 0 || i < roadLeft) roadLeft = i;
                    if (end > roadRight) roadRight = end;
                }
            }

            if (roadLeft < 0) return;

            if (pierLeft >= 0)
            {
                float bridgeX  = pierLeft * cs + cs * 0.5f;
                float roadMinX = roadLeft * cs;
                float pierMaxX = pierRight * cs;

                var nRoadL   = AddNode(roadMinX, RoadZ);
                var nBridgeR = AddNode(bridgeX,  RoadZ);
                var nBridgeP = AddNode(bridgeX,  PierZ);
                var nPierR   = AddNode(pierMaxX, PierZ);

                AddEdge(nRoadL,   nBridgeR);  // road
                AddEdge(nBridgeR, nBridgeP);  // bridge (depth crossing)
                AddEdge(nBridgeP, nPierR);    // pier
            }
            else
            {
                var nL = AddNode(roadLeft * cs,  RoadZ);
                var nR = AddNode(roadRight * cs, RoadZ);
                AddEdge(nL, nR);
            }
        }

        // ─────────────────────────────────────────────────────────

        /// <summary>Find the edge and progress for a building's frontage.</summary>
        public WalkEdge FindEdgeForBuilding(CityGrid city, int origin, out float progress)
        {
            float cs = CityRenderer.CellSize;
            int bw = city.GetBuildingWidth(origin);
            if (bw < 1) bw = 1;
            float cx = (origin + bw * 0.5f) * cs;
            float bz = city.GetSurface(origin) == CellType.Pier ? PierZ : RoadZ;
            return FindNearestEdge(cx, bz, out progress);
        }

        /// <summary>Find the closest edge to a world point.</summary>
        public WalkEdge FindNearestEdge(float worldX, float worldZ, out float progress)
        {
            WalkEdge best = null;
            float bestDist = float.MaxValue;
            progress = 0f;

            for (int i = 0; i < Edges.Count; i++)
            {
                var e = Edges[i];
                float d = e.DistanceTo(worldX, worldZ);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = e;
                    progress = e.Project(worldX, worldZ);
                }
            }
            return best;
        }

        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Plan a route from one edge+progress to another.
        /// For a tree graph the path is unique — no A* needed.
        /// </summary>
        public bool PlanRoute(
            WalkEdge fromEdge, float fromProgress,
            WalkEdge toEdge, float toProgress,
            List<RouteStep> outRoute)
        {
            outRoute.Clear();
            if (fromEdge == null || toEdge == null) return false;

            if (fromEdge == toEdge)
            {
                outRoute.Add(new RouteStep { Edge = toEdge, TargetProgress = toProgress });
                return true;
            }

            var pathA = FindEdgePath(fromEdge.A, fromEdge, toEdge);
            var pathB = FindEdgePath(fromEdge.B, fromEdge, toEdge);

            List<WalkEdge> edgePath;
            WalkNode exitNode;

            if (pathA != null && (pathB == null || pathA.Count <= pathB.Count))
            { edgePath = pathA; exitNode = fromEdge.A; }
            else if (pathB != null)
            { edgePath = pathB; exitNode = fromEdge.B; }
            else
                return false;

            // Step 1: walk to exit node on source edge
            outRoute.Add(new RouteStep
            { Edge = fromEdge, TargetProgress = fromEdge.ProgressAt(exitNode) });

            // Intermediate edges: walk through to connecting node
            WalkNode prev = exitNode;
            for (int i = 0; i < edgePath.Count - 1; i++)
            {
                var edge = edgePath[i];
                var next = edge.Other(prev);
                outRoute.Add(new RouteStep
                { Edge = edge, TargetProgress = edge.ProgressAt(next) });
                prev = next;
            }

            // Final step: walk to destination on target edge
            outRoute.Add(new RouteStep
            { Edge = toEdge, TargetProgress = toProgress });

            return true;
        }

        /// <summary>
        /// BFS from a start node (excluding the source edge) to find the target edge.
        /// Returns the sequence of edges from start to target, or null if unreachable.
        /// </summary>
        private static List<WalkEdge> FindEdgePath(
            WalkNode start, WalkEdge exclude, WalkEdge target)
        {
            foreach (var e in start.Edges)
            {
                if (e == exclude) continue;
                if (e == target) return new List<WalkEdge> { e };
            }

            var visited = new HashSet<int> { start.Id };
            var queue = new Queue<(WalkNode node, List<WalkEdge> path)>();

            foreach (var e in start.Edges)
            {
                if (e == exclude) continue;
                var next = e.Other(start);
                if (!visited.Add(next.Id)) continue;
                queue.Enqueue((next, new List<WalkEdge> { e }));
            }

            while (queue.Count > 0)
            {
                var (node, path) = queue.Dequeue();
                foreach (var e in node.Edges)
                {
                    var next = e.Other(node);
                    if (!visited.Add(next.Id)) continue;
                    var newPath = new List<WalkEdge>(path) { e };
                    if (e == target) return newPath;
                    queue.Enqueue((next, newPath));
                }
            }

            return null;
        }
    }
}
