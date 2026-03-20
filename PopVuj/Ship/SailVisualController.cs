// Copyright SeaRäuber 2025-2026
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace SeaRauber.Ship
{
    /// <summary>
    /// Drives per-frame sail mesh deformation based on wind fill.
    ///
    /// Attached to the ship root. Finds all sail child GameObjects,
    /// replaces their primitive cubes with tessellated sail meshes,
    /// and deforms them each frame to show:
    ///
    ///   - BELLY:   how much wind fills the sail (0 flat → 1 deep curve)
    ///   - SIDE:    which side the wind pushes (port / starboard)
    ///   - FLUTTER: luffing or torn sails flap with sine-wave noise
    ///   - REEF:    top portion of sail gathered to yard/boom
    ///   - FURLED:  mesh collapsed to a tight bundle at the yard
    ///
    /// The visual is driven by ShipSail state — apparent wind angle,
    /// per-sail SailState, sheet trim, and reef level.
    /// </summary>
    public class SailVisualController : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        // PER-SAIL VISUAL DATA
        // ═══════════════════════════════════════════════════════════════

        struct SailVisual
        {
            public GameObject GO;
            public MeshFilter MeshFilter;
            public Mesh Mesh;
            public Vector3[] BaseVerts;     // rest-pose: flat sail
            public Vector3[] DeformedVerts; // scratch buffer for deformation
            public int SegX, SegY;
            public int RigSailIndex;        // index into ShipSail.Rig.Sails[]
            public SailType Type;
            public Renderer Renderer;
            public Color BaseColor;
        }

        readonly List<SailVisual> _visuals = new();
        ShipSail _sail;
        bool _initialized;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize after bootstrap has created subcomponent GameObjects
        /// and ShipSail has its rig plan loaded.
        /// </summary>
        public void Initialize(ShipSail sail, bool isFlagship)
        {
            _sail = sail;
            if (_sail == null || _sail.Rig == null) return;

            // Find all sail child GameObjects and match to rig sails
            var rig = _sail.Rig;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (!IsSailGameObject(child.name)) continue;

                // Match subcomponent name to rig sail
                int rigIdx = FindRigSailIndex(child.name, rig);
                SailType type = SailType.Square;
                if (rigIdx >= 0)
                    type = rig.Sails[rigIdx].Type;
                else
                    type = InferSailType(child.name);

                // Get current primitive scale to determine sail dimensions
                float width = child.transform.localScale.x;
                float height = child.transform.localScale.y;

                // Reset scale to 1 — mesh now defines geometry
                child.transform.localScale = Vector3.one;

                // Preserve renderer color
                var renderer = child.GetComponent<Renderer>();
                Color baseColor = Color.white;
                if (renderer != null && renderer.material != null)
                    baseColor = renderer.material.color;

                // Build custom mesh
                var mf = SailMeshBuilder.BuildSailMesh(child, type, width, height, isFlagship);
                var mesh = mf.mesh;

                // Cache base vertices
                var baseVerts = mesh.vertices;
                var deformed = new Vector3[baseVerts.Length];
                System.Array.Copy(baseVerts, deformed, baseVerts.Length);

                _visuals.Add(new SailVisual
                {
                    GO = child,
                    MeshFilter = mf,
                    Mesh = mesh,
                    BaseVerts = baseVerts,
                    DeformedVerts = deformed,
                    SegX = 6, // matches SailMeshBuilder constants
                    SegY = 8,
                    RigSailIndex = rigIdx,
                    Type = type,
                    Renderer = renderer,
                    BaseColor = baseColor,
                });
            }

            _initialized = _visuals.Count > 0;

            if (_initialized)
                Debug.Log($"[SAIL VIS] Initialized {_visuals.Count} sail meshes on {gameObject.name}");
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        void Update()
        {
            if (!_initialized || _sail == null) return;

            float time = Time.time;
            float awa = _sail.ApparentWindAngle;
            float aws = _sail.ApparentWindSpeed;

            // Wind side: -1 = wind from port, +1 = wind from starboard
            float windSide = awa >= 0 ? -1f : 1f;

            // Base fill amount: proportional to sail force, modulated by wind speed
            float baseFill = _sail.SailForce * Mathf.Clamp01(aws * 0.15f);

            for (int v = 0; v < _visuals.Count; v++)
            {
                var vis = _visuals[v];
                if (vis.Mesh == null) continue;

                float fill = baseFill;
                float flap = 0f;
                float reefFrac = 0f;
                SailState state = SailState.Set;

                // Per-sail state from rig
                if (vis.RigSailIndex >= 0 && _sail.Rig?.Sails != null
                    && vis.RigSailIndex < _sail.Rig.Sails.Length)
                {
                    var s = _sail.Rig.Sails[vis.RigSailIndex];
                    state = s.State;
                    reefFrac = s.ReefLevel * 0.25f;

                    // Adjust fill by sheet trim quality
                    float trimError = Mathf.Abs(s.SheetTrim - Mathf.InverseLerp(30f, 180f, Mathf.Abs(awa)));
                    fill *= Mathf.Lerp(0.3f, 1f, 1f - trimError);
                }

                switch (state)
                {
                    case SailState.Furled:
                        fill = 0f;
                        reefFrac = 1f; // collapse entire sail
                        break;
                    case SailState.Luffing:
                        fill *= 0.15f;
                        flap = 0.8f;
                        break;
                    case SailState.Torn:
                        fill = 0.05f;
                        flap = 1f;
                        break;
                    case SailState.Aback:
                        fill *= 0.4f;
                        windSide = -windSide; // belly wrong direction
                        break;
                    case SailState.Reefed:
                        // fill already reduced by effective area
                        break;
                }

                // Dead zone: no fill
                if (_sail.InDeadZone && state != SailState.Torn)
                {
                    fill = 0f;
                    flap = 0.3f;
                }

                SailMeshBuilder.DeformSailMesh(
                    vis.Mesh, vis.BaseVerts, vis.DeformedVerts,
                    fill, windSide, flap, reefFrac,
                    vis.SegX, vis.SegY, time);

                // Tint torn sails darker
                if (vis.Renderer != null && vis.Renderer.material != null)
                {
                    Color c = vis.BaseColor;
                    if (state == SailState.Torn)
                        c = Color.Lerp(c, new Color(0.3f, 0.28f, 0.25f), 0.5f);
                    else if (state == SailState.Furled)
                        c.a = 0.3f; // semi-transparent when furled
                    vis.Renderer.material.color = c;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MATCHING HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Is this child GO a sail? (vs mast, hull, rigging, etc.)</summary>
        static bool IsSailGameObject(string name)
        {
            // Match: MainSail, ForeSail, Jib, Spanker, ForeCourse, MainTopsail, etc.
            // Exclude: ForeMast, Forestay, SailLocker, etc.
            if (name.Contains("Mast") || name.Contains("Shroud") || name.Contains("Ratline")
                || name.Contains("Stay") || name.Contains("Brace") || name.Contains("Cleat")
                || name.Contains("Bitt") || name.Contains("Vang") || name.Contains("Rail")
                || name.Contains("Boom") || name.Contains("Gaff") || name.Contains("Yard")
                || name.Contains("Nest") || name.Contains("Crosstree") || name.Contains("Helm")
                || name.Contains("Hull") || name.Contains("Deck") || name.Contains("Chart")
                || name.Contains("Gun") || name.Contains("Cargo") || name.Contains("Crane")
                || name.Contains("Signal") || name.Contains("Tiller") || name.Contains("Bowsprit")
                || name.Contains("Castle") || name.Contains("Poop"))
                return false;

            return name.Contains("Sail") || name.Contains("Jib")
                || name.Contains("Spanker") || name.Contains("Course")
                || name.Contains("Topsail") || name.Contains("Topgallant")
                || name.Contains("Staysail");
        }

        /// <summary>Find the rig sail index that best matches a subcomponent name.</summary>
        static int FindRigSailIndex(string goName, SailRigPlan rig)
        {
            if (rig?.Sails == null) return -1;

            // Direct name match (remove spaces from rig sail names)
            for (int i = 0; i < rig.Sails.Length; i++)
            {
                string rigName = rig.Sails[i].Name.Replace(" ", "");
                if (goName.Replace("_", "") == rigName) return i;
            }

            // Partial match: GO name contains key rig words
            for (int i = 0; i < rig.Sails.Length; i++)
            {
                string rn = rig.Sails[i].Name;
                // "ForeCourse" matches rig "Fore Course"
                if (goName.Contains("Fore") && rn.Contains("Fore")
                    && MatchesSailLevel(goName, rn))
                    return i;
                if (goName.Contains("Main") && rn.Contains("Main")
                    && MatchesSailLevel(goName, rn))
                    return i;
                if (goName.Contains("Mizzen") && rn.Contains("Mizzen")
                    && MatchesSailLevel(goName, rn))
                    return i;
                if (goName == "Jib" && rn == "Jib") return i;
                if (goName == "FlyingJib" && rn.Contains("Flying")) return i;
                if (goName == "Spanker" && rn == "Spanker") return i;
            }

            return -1;
        }

        static bool MatchesSailLevel(string goName, string rigName)
        {
            // Match sail tiers: Course, Topsail, Topgallant, Sail, Staysail
            if (goName.Contains("Topgallant") && rigName.Contains("Topgallant")) return true;
            if (goName.Contains("Topsail") && !goName.Contains("Topgallant")
                && rigName.Contains("Topsail") && !rigName.Contains("Topgallant")) return true;
            if (goName.Contains("Course") && rigName.Contains("Course")) return true;
            if (goName.Contains("Staysail") && rigName.Contains("Staysail")) return true;
            // "ForeSail"/"MainSail" matches rig "Fore Sail"/"Main Sail"
            if (goName.EndsWith("Sail") && !goName.Contains("Topsail") && !goName.Contains("Staysail")
                && rigName.EndsWith("Sail") && !rigName.Contains("Topsail") && !rigName.Contains("Staysail"))
                return true;
            return false;
        }

        /// <summary>Infer sail type from GO name when no rig match found.</summary>
        static SailType InferSailType(string name)
        {
            if (name.Contains("Jib")) return SailType.Jib;
            if (name.Contains("Staysail")) return SailType.Staysail;
            if (name.Contains("Spanker")) return SailType.Spanker;
            if (name.Contains("Course") || name.Contains("Topsail") || name.Contains("Topgallant"))
                return SailType.Square;
            return SailType.ForeAndAft; // default
        }
    }
}
