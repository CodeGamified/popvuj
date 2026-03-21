// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using System.Collections.Generic;
using CodeGamified.Procedural;

namespace PopVuj.Game
{
    /// <summary>
    /// Loads OBJ meshes from Resources/Objects/ with per-asset scale normalization.
    ///
    /// Imported .obj models (Blockbench / Conquest Reforged) have ~1 unit = 1 block
    /// intrinsic scale. This registry maps each asset to a uniform scale factor
    /// that brings it to game-world furniture size (~0.1–0.35 world units).
    ///
    /// Scale values are initial approximations — tune at runtime.
    ///
    /// Usage in blueprint emitters:
    ///   if (ObjectScale.TryAdd(parts, id, "barrel", pos, colorKey))
    ///       return;
    ///   // ... primitive fallback
    ///
    /// Usage in direct renderers (ShipRenderer):
    ///   var go = ObjectScale.CreateMeshObject(name, "wheel", parent);
    ///   if (go != null) { go.transform.localScale = ObjectScale.GetScale("wheel"); ... }
    ///   else { go = CreatePrimitive(name, parent); ... }
    /// </summary>
    public static class ObjectScale
    {
        // Loaded mesh cache — null entries tracked separately in _missing
        private static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        private static readonly HashSet<string> _missing = new HashSet<string>();

        // Loaded texture cache
        private static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        private static readonly HashSet<string> _texMissing = new HashSet<string>();

        // ═══════════════════════════════════════════════════════════════
        // SCALE REGISTRY — uniform scale per asset
        //
        // Models are ~1 block in Blockbench. Values below bring them to
        // game-world size. Multiply by your own factor if needed.
        // ═══════════════════════════════════════════════════════════════

        private static readonly Dictionary<string, float> _scales = new Dictionary<string, float>
        {
            // ── Workshop ────────────────────────────────────────
            { "anvil_on_log",                       0.28f },
            { "anvil",                              0.18f },
            { "small_anvil",                        0.22f },
            { "woodworking_supplies",               0.25f },

            // ── Tables & workbenches ────────────────────────────
            { "standing_two_legged_table_1",        0.25f },
            { "standing_two_legged_table_2",        0.25f },
            { "bronze_3_legged_table",              0.25f },
            { "trestle",                            0.28f },
            { "trestle_tilted",                     0.28f },

            // ── Chairs & seating ────────────────────────────────
            { "old_bench",                          0.22f },
            { "old_bench_center",                   0.22f },
            { "old_bench_left",                     0.22f },
            { "old_bench_right",                    0.22f },
            { "three_legged_chair",                 0.20f },
            { "gothic_arm_chair",                   0.22f },
            { "royal_chair",                        0.24f },
            { "fabric_folding_stool",               0.18f },
            { "wooden_cross_stool",                 0.18f },
            { "rustic_side_chair",                  0.22f },

            // ── Barrels & kegs ──────────────────────────────────
            { "barrel",                             0.16f },
            { "barrel_empty",                       0.16f },
            { "barrel_of_wine",                     0.16f },
            { "barrel_of_wine_horizontal",          0.16f },
            { "small_barrel_one_barrel",            0.12f },
            { "small_barrel_two_barrels",           0.14f },
            { "small_barrel_three_barrels",         0.16f },
            { "small_barrel_four_barrels",          0.18f },
            { "small_barrels_1",                    0.14f },
            { "keg",                                0.14f },
            { "keg_horizontal",                     0.14f },
            { "cask",                               0.14f },
            { "round_barrel",                       0.16f },
            { "hanging_barrel",                     0.14f },
            { "broken_barrel_up",                   0.14f },
            { "broken_barrel_down",                 0.14f },
            { "broken_barrel_horizontal",           0.14f },
            { "tall_barrel_lower",                  0.18f },

            // ── Sacks & burlap ──────────────────────────────────
            { "burlap_sack",                        0.14f },
            { "burlap_sack_one_sack",               0.14f },
            { "burlap_sack_two_sacks",              0.16f },
            { "burlap_sack_with_content",           0.14f },
            { "sack",                               0.14f },
            { "burlap_pile_one_stack",              0.12f },
            { "hay_bundle",                         0.14f },
            { "hay_bundle_one_bundle",              0.12f },

            // ── Chests ──────────────────────────────────────────
            { "whitewashed_linen_chest",            0.16f },
            { "whitewashed_linen_chest_1",          0.16f },
            { "rounded_chest_2",                    0.16f },
            { "carpenters_chest_2",                 0.16f },
            { "carpenters_chest_3",                 0.16f },

            // ── Troughs ─────────────────────────────────────────
            { "water_trough",                       0.30f },
            { "wood_trough",                        0.28f },
            { "filled_wood_trough",                 0.28f },
            { "feeding_trough1",                    0.28f },

            // ── Kitchen & cooking ───────────────────────────────
            { "stove",                              0.28f },
            { "cooking_brazier",                    0.20f },
            { "cooking_brazier_1",                  0.20f },
            { "mortar_and_pestle",                  0.12f },
            { "cast_iron_cauldron",                 0.16f },
            { "cast_iron_pot",                      0.12f },
            { "cast_iron_pan",                      0.12f },
            { "small_hanging_cauldron",             0.16f },
            { "butter_churn",                       0.22f },
            { "wine_press",                         0.22f },
            { "quern",                              0.18f },

            // ── Lanterns & light ────────────────────────────────
            { "lantern_large",                      0.10f },
            { "ship_lantern",                       0.10f },
            { "candle_lantern_1",                   0.10f },
            { "candle_lantern_2",                   0.10f },
            { "round_bronze_lantern_1",             0.10f },
            { "bronze_brazier",                     0.16f },

            // ── Wheels ──────────────────────────────────────────
            { "wheel",                              0.20f },
            { "spoked_wooden_wheel_2",              0.20f },
            { "reinforced_wheel",                   0.24f },
            { "wooden_wheel_2",                     0.20f },
            { "hand_cart_inventory",                0.24f },

            // ── Scrolls & lecterns ──────────────────────────────
            { "scroll_stand/scroll_stand_1",        0.22f },
            { "scroll_stand/scroll_stand_0",        0.22f },
            { "assorted_scrolls_1",                 0.16f },
            { "easel",                              0.22f },

            // ── Buckets & containers ────────────────────────────
            { "wooden_bucket",                      0.12f },
            { "large_bucket",                       0.14f },
            { "large_wooden_bucket",                0.14f },
            { "large_iron_bucket",                  0.14f },
            { "water_jug",                          0.10f },
            { "clay_jug",                           0.10f },
            { "copper_bowl",                        0.10f },

            // ── Baskets ─────────────────────────────────────────
            { "baskets/woven_basket",               0.14f },
            { "baskets/large_woven_basket",         0.16f },
            { "empty_wicker_basket",                0.14f },
            { "wicker_basket_of_firewood",          0.16f },

            // ── Bottles ─────────────────────────────────────────
            { "bottle_of_wine",                     0.09f },
            { "green_bottle",                       0.09f },
            { "brown_bottle",                       0.09f },
            { "bottle_of_frankincense",             0.09f },

            // ── Tools ───────────────────────────────────────────
            { "rope_pulley",                        0.16f },
            { "scythe",                             0.20f },
            { "leaning_broom",                      0.18f },

            // ── Looms (workshop variety) ────────────────────────
            { "looms/simple_warp_weighted_loom_small", 0.25f },

            // ── Target dummies ──────────────────────────────────
            { "round_target_dummy",                 0.22f },

            // ── Tree foliage — canopy meshes ────────────────────
            { "parent_leaves",                      0.55f },
            { "parent_leaves_fruit",                0.55f },
            { "leaf_tips",                          0.40f },

            // ── Cypress ─────────────────────────────────────────
            { "cypress/cypress_leaves_pillar_1",    0.40f },
            { "cypress/cypress_leaves_pillar_2",    0.40f },
            { "cypress/cypress_leaves_pillar_3",    0.40f },
            { "cypress/cypress_leaves_pillar_4",    0.40f },
            { "cypress/cypress_leaves_tip",         0.35f },
            { "cypress/cypress_leaves_top_1",       0.35f },
            { "cypress/cypress_leaves",             0.45f },

            // ── Pine ────────────────────────────────────────────
            { "pine/underbranch",                   0.45f },
            { "pine/underbranch_2",                 0.45f },
            { "pine/underbranch_3",                 0.45f },
            { "pine/underbranch_4",                 0.45f },
            { "pine/top_flat",                      0.40f },
            { "pine/top_short",                     0.40f },
            { "pine/top_tall",                      0.40f },
            { "pine/bush",                          0.35f },
            { "pine/small_bush",                    0.30f },

            // ── Palm fronds ─────────────────────────────────────
            { "caribbean_royal_palm_straight_1",    0.50f },
            { "caribbean_royal_palm_straight_2",    0.50f },
            { "caribbean_royal_palm_old_straight_1",0.50f },
            { "caribbean_royal_palm_old_straight_2",0.50f },
            { "caribbean_royal_palm_corner_1",      0.50f },
            { "caribbean_royal_palm_corner_2",      0.50f },
        };

        // ═══════════════════════════════════════════════════════════════
        // MESH LOADING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a mesh from Resources/Objects/{name}. Caches results.
        /// Returns null if not found (also cached to avoid repeated lookups).
        /// </summary>
        public static Mesh LoadMesh(string name)
        {
            if (_missing.Contains(name)) return null;
            if (_cache.TryGetValue(name, out var cached)) return cached;

            Mesh mesh = null;

            // OBJ files import as Models — try loading as GameObject first
            var go = Resources.Load<GameObject>($"Objects/{name}");
            if (go != null)
            {
                var mf = go.GetComponentInChildren<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }

            // Fallback: some Unity versions expose the mesh directly
            if (mesh == null)
                mesh = Resources.Load<Mesh>($"Objects/{name}");

            if (mesh != null)
                _cache[name] = mesh;
            else
                _missing.Add(name);

            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // TEXTURE LOADING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a texture from Resources/Textures/{path} (without extension).
        /// Caches results. Returns null if not found.
        /// </summary>
        public static Texture2D LoadTexture(string path)
        {
            if (_texMissing.Contains(path)) return null;
            if (_texCache.TryGetValue(path, out var cached)) return cached;

            var tex = Resources.Load<Texture2D>($"Textures/{path}");
            if (tex != null)
                _texCache[path] = tex;
            else
                _texMissing.Add(path);

            return tex;
        }

        /// <summary>
        /// Get the uniform scale vector for an asset. Returns a default if
        /// the asset isn't in the registry (still usable, just needs tuning).
        /// </summary>
        public static Vector3 GetScale(string name)
        {
            if (_scales.TryGetValue(name, out float s))
                return new Vector3(s, s, s);
            return new Vector3(0.20f, 0.20f, 0.20f);
        }

        // ═══════════════════════════════════════════════════════════════
        // CONVENIENCE — ProceduralAssembler path
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Try to load an OBJ mesh and add it as a ProceduralPartDef.
        /// Returns true if the mesh was found and the part was added.
        /// Falls through to false so callers can emit primitive fallback.
        /// </summary>
        public static bool TryAdd(List<ProceduralPartDef> parts, string id, string objName,
            Vector3 pos, string colorKey, Quaternion? rot = null)
        {
            var mesh = LoadMesh(objName);
            if (mesh == null) return false;

            parts.Add(new ProceduralPartDef
            {
                Id = id,
                CustomMesh = mesh,
                LocalPos = pos,
                LocalScale = GetScale(objName),
                LocalRot = rot ?? Quaternion.identity,
                ColorKey = colorKey,
            });
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // CONVENIENCE — Direct renderer path (ShipRenderer, etc.)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Create a GameObject with the loaded OBJ mesh, parented and ready
        /// for positioning. Returns null if the mesh wasn't found — caller
        /// should fall back to CreatePrimitive.
        /// </summary>
        public static GameObject CreateMeshObject(string name, string objName, Transform parent)
        {
            // Load the prefab — this is the .obj as Unity imported it,
            // complete with MeshFilter, MeshRenderer, and Material[] slots
            // matching each sub-material in the .mtl.
            var prefab = Resources.Load<GameObject>($"Objects/{objName}");
            if (prefab == null) return null;

            // Verify it actually has geometry
            var srcMf = prefab.GetComponentInChildren<MeshFilter>();
            if (srcMf == null || srcMf.sharedMesh == null) return null;

            // Instantiate to get the full Material[] array matching submeshes
            var go = Object.Instantiate(prefab);
            go.name = name;
            go.transform.SetParent(parent, false);

            // Strip colliders from the instance
            foreach (var col in go.GetComponentsInChildren<Collider>())
                Object.Destroy(col);

            return go;
        }
    }
}
