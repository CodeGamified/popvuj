// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using PopVuj.Game;

namespace PopVuj.Crew
{
    /// <summary>
    /// Renders minions as Minecraft models on the city road.
    ///
    /// Each minion gets a base skin (by identity) with a profession overlay
    /// composited at runtime based on their current SlotRole.
    ///
    /// Base skins:     Resources/Skins/{name}.png   — tied to Minion.Id
    /// Overlays:       Resources/Skins/Overlays/{name}.png — tied to SlotRole
    ///
    /// Composite textures are cached per (base, overlay) pair so each unique
    /// combination is only blended once.
    ///
    /// Cargo volumes are rendered as a second cube attached to each minion,
    /// sized and colored per CargoKind. Hidden when minion carries nothing.
    /// </summary>
    public class MinionRenderer : MonoBehaviour
    {
        private MinionManager _manager;
        private CityGrid _city;

        private readonly List<GameObject> _cargoPool = new List<GameObject>();
        private GameObject _parent;
        private GameObject _cargoParent;

        // Shared Minion rig (meshes + pivot positions)
        private MinionRig _rig;

        // Per-minion articulated hierarchy
        private struct MinionBody
        {
            public GameObject Root;
            public Transform RightArm, LeftArm, RightLeg, LeftLeg;
            public MeshRenderer[] Renderers;
        }
        private readonly List<MinionBody> _bodies = new List<MinionBody>();

        // ── Skin system ─────────────────────────────────────────

        // Base skins loaded from Resources/Skins/ (identity, assigned by Id)
        private readonly List<Texture2D> _baseSkins = new List<Texture2D>();

        // Overlay textures loaded from Resources/Skins/Overlays/ keyed by SlotRole
        private readonly Dictionary<SlotRole, Texture2D> _overlayTextures = new Dictionary<SlotRole, Texture2D>();

        // Cached composited textures: "base_idx:overlay_role" → blended texture
        private readonly Dictionary<string, Texture2D> _compositeCache = new Dictionary<string, Texture2D>();

        // Cached materials: texture instance ID → material (avoids duplicate materials)
        private readonly Dictionary<int, Material> _materialCache = new Dictionary<int, Material>();

        // Shared shader reference
        private Shader _shader;

        // Fallback tint material when no skins are found
        private Material _fallbackMaterial;

        // ── Minion dimensions ───────────────────────────────────
        // Mesh is built at unit height (1.0). MinionScale maps to world units.
        private const float MinionScale = 0.5f;

        // Mesh-space proportions (relative to unit height)
        private const float MinionW = 0.57f;
        private const float MinionH = 1.0f;
        private const float CartW   = 1.29f;            // wider body for cart-pullers
        private const float CartH   = 0.86f;            // slightly shorter (hunched under load)
        private const float CartDepth = 0.86f;           // thicker Z for the cart body
        private const float RoadY = 0.3f;              // matches CityRenderer RoadH
        private const float MinionZ = 0.5f;            // rendering Z base (SyncWorldPositions compensates)
        private const float InBuildingZ = 0.7f;        // slightly in front of building face

        // ── Color palette ───────────────────────────────────────

        private static readonly Color IdleColor = new Color(0.85f, 0.65f, 0.45f);
        private static readonly Color WalkColor = new Color(0.80f, 0.60f, 0.40f);
        private static readonly Color CartIdleColor = new Color(0.55f, 0.40f, 0.20f);   // darker wood-brown for cart
        private static readonly Color CartWalkColor = new Color(0.50f, 0.35f, 0.18f);
        private static readonly Color WorkColor = new Color(0.50f, 0.60f, 0.70f);
        private static readonly Color PrayColor = new Color(0.90f, 0.85f, 0.50f);
        private static readonly Color RestColor = new Color(0.55f, 0.50f, 0.65f);
        private static readonly Color EatColor  = new Color(0.60f, 0.75f, 0.45f);
        private static readonly Color HarborColor = new Color(0.40f, 0.55f, 0.65f);
        private static readonly Color SailorColor = new Color(0.35f, 0.45f, 0.60f);

        // Walk animation tuning
        private const float SWING_SPEED = 9f;      // radians/sec
        private const float SWING_ANGLE = 40f;     // max swing in degrees
        private const float IDLE_SWING  = 0f;      // no swing when idle

        public void Initialize(MinionManager manager, CityGrid city)
        {
            _manager = manager;
            _city = city;
            _parent = new GameObject("Minions");
            _parent.transform.SetParent(transform, false);
            _cargoParent = new GameObject("Cargo");
            _cargoParent.transform.SetParent(transform, false);

            _rig = MinionModelBuilder.BuildVillagerRig();

            _shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");

            LoadBaseSkins();
            LoadOverlays();

            // Fallback material for when no skins exist
            _fallbackMaterial = new Material(_shader);
            if (_fallbackMaterial.HasProperty("_BaseColor"))
                _fallbackMaterial.SetColor("_BaseColor", IdleColor);
            else
                _fallbackMaterial.color = IdleColor;

            Debug.Log($"[MinionRenderer] Loaded {_baseSkins.Count} base skin(s), {_overlayTextures.Count} overlay(s)");
        }

        // ── Base skin names (order = assignment priority by Id) ──

        private static readonly string[] BaseSkinNames = {
            "villager", "guy", "farmer", "smith", "priest", "librarian", "butcher",
            "plains", "desert", "jungle", "savanna", "snow", "swamp", "taiga",
        };

        private void LoadBaseSkins()
        {
            foreach (var name in BaseSkinNames)
            {
                var tex = Resources.Load<Texture2D>($"Skins/{name}");
                if (tex == null) continue;
                tex = MakeReadable(tex);
                _baseSkins.Add(tex);
            }
        }

        // ── Overlay mapping: SlotRole → overlay texture name ─────

        private static readonly Dictionary<SlotRole, string> OverlayNames = new Dictionary<SlotRole, string>
        {
            { SlotRole.Farmer,           "farmer" },
            { SlotRole.Worker,           "toolsmith" },
            { SlotRole.Preacher,         "cleric" },
            { SlotRole.Worshipper,       "cleric" },
            { SlotRole.Merchant,         "librarian" },
            { SlotRole.Foreman,          "mason" },
            { SlotRole.Shipwright,       "mason" },
            { SlotRole.Docker,           "fisherman" },
            { SlotRole.CraneOperator,    "armorer" },
            { SlotRole.WarehouseKeeper,  "cartographer" },
            { SlotRole.Caretaker,        "shepherd" },
            { SlotRole.Sailor,           "fletcher" },
            // Resident has no overlay — uses base skin only
        };

        private void LoadOverlays()
        {
            foreach (var kvp in OverlayNames)
            {
                if (_overlayTextures.ContainsKey(kvp.Key)) continue;
                var tex = Resources.Load<Texture2D>($"Skins/Overlays/{kvp.Value}");
                if (tex == null) continue;
                _overlayTextures[kvp.Key] = MakeReadable(tex);
            }
        }

        /// <summary>
        /// Returns a readable copy of the texture with Point filtering.
        /// Needed because Resources.Load textures may not be Read/Write enabled,
        /// and GetPixels32() would fail during overlay compositing.
        /// </summary>
        private static Texture2D MakeReadable(Texture2D source)
        {
            // Use a temporary RenderTexture to copy pixels without needing Read/Write import flag
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            readable.filterMode = FilterMode.Point;
            readable.wrapMode = TextureWrapMode.Clamp;
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        // ── Composite blending (CPU) ────────────────────────────

        /// <summary>
        /// Returns a material for the given minion considering their base skin
        /// and current profession overlay. Composites are cached.
        /// </summary>
        private Material GetSkinMaterial(Minion m)
        {
            if (_baseSkins.Count == 0) return _fallbackMaterial;

            int baseIdx = m.Id % _baseSkins.Count;
            var baseTex = _baseSkins[baseIdx];

            // Determine current overlay from SlotRole
            SlotRole role = GetCurrentRole(m);
            Texture2D overlayTex;
            _overlayTextures.TryGetValue(role, out overlayTex);

            // Build cache key
            string key = overlayTex != null
                ? $"{baseIdx}:{role}"
                : $"{baseIdx}:none";

            // Check composite cache
            Texture2D finalTex;
            if (!_compositeCache.TryGetValue(key, out finalTex))
            {
                if (overlayTex != null)
                    finalTex = BlendTextures(baseTex, overlayTex);
                else
                    finalTex = baseTex;
                _compositeCache[key] = finalTex;
            }

            // Check material cache (keyed by texture instance ID)
            int texId = finalTex.GetInstanceID();
            Material mat;
            if (!_materialCache.TryGetValue(texId, out mat))
            {
                mat = new Material(_shader);
                if (mat.HasProperty("_BaseMap"))
                    mat.SetTexture("_BaseMap", finalTex);
                else if (mat.HasProperty("_MainTex"))
                    mat.SetTexture("_MainTex", finalTex);
                _materialCache[texId] = mat;
            }

            return mat;
        }

        /// <summary>
        /// Resolves a minion's current SlotRole. Returns Resident when idle/walking.
        /// </summary>
        private SlotRole GetCurrentRole(Minion m)
        {
            if (m.State == MinionState.InSlot && m.TargetBuilding >= 0)
            {
                var type = _city.GetSurface(m.TargetBuilding);
                int bw = _city.GetBuildingWidth(m.TargetBuilding);
                return BuildingSlots.GetSlotRole(type, m.SlotIndex, bw);
            }
            if (m.State == MinionState.Hauling)
                return SlotRole.Docker;
            // Idle / walking — map by current need
            switch (m.CurrentNeed)
            {
                case MinionNeed.Work:   return SlotRole.Worker;
                case MinionNeed.Faith:  return SlotRole.Worshipper;
                case MinionNeed.Hunger: return SlotRole.Farmer;
                case MinionNeed.Haul:   return SlotRole.Docker;
                default:                return SlotRole.Resident;  // no overlay
            }
        }

        /// <summary>
        /// Alpha-blends overlay on top of base. Both must be readable.
        /// Returns a new Texture2D with Point filtering.
        /// </summary>
        private static Texture2D BlendTextures(Texture2D baseTex, Texture2D overlay)
        {
            int w = baseTex.width;
            int h = baseTex.height;
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.filterMode = FilterMode.Point;
            result.wrapMode = TextureWrapMode.Clamp;

            var basePixels = baseTex.GetPixels32();
            var overPixels = overlay.GetPixels32();

            // Overlay may be a different size — sample nearest
            bool sameSize = overlay.width == w && overlay.height == h;

            for (int i = 0; i < basePixels.Length; i++)
            {
                Color32 bp = basePixels[i];
                Color32 op;
                if (sameSize)
                {
                    op = overPixels[i];
                }
                else
                {
                    int px = (i % w) * overlay.width / w;
                    int py = (i / w) * overlay.height / h;
                    op = overPixels[py * overlay.width + px];
                }

                // Standard alpha-over compositing
                float oa = op.a / 255f;
                float ba = bp.a / 255f;
                float outA = oa + ba * (1f - oa);
                if (outA < 0.001f)
                {
                    basePixels[i] = new Color32(0, 0, 0, 0);
                }
                else
                {
                    byte r = (byte)((op.r * oa + bp.r * ba * (1f - oa)) / outA);
                    byte g = (byte)((op.g * oa + bp.g * ba * (1f - oa)) / outA);
                    byte b = (byte)((op.b * oa + bp.b * ba * (1f - oa)) / outA);
                    basePixels[i] = new Color32(r, g, b, (byte)(outA * 255f));
                }
            }

            result.SetPixels32(basePixels);
            result.Apply();
            return result;
        }

        private void LateUpdate()
        {
            if (_manager == null) return;
            var minions = _manager.Minions;

            // Grow pools on demand
            while (_bodies.Count < minions.Count)
                _bodies.Add(CreateMinionBody(_bodies.Count));
            while (_cargoPool.Count < minions.Count)
                _cargoPool.Add(CreateCargoCube(_cargoPool.Count));

            // Hide excess pool objects
            for (int i = minions.Count; i < _bodies.Count; i++)
                _bodies[i].Root.SetActive(false);
            for (int i = minions.Count; i < _cargoPool.Count; i++)
                _cargoPool[i].SetActive(false);

            // Position each active minion + cargo
            for (int i = 0; i < minions.Count; i++)
            {
                var m = minions[i];
                var body = _bodies[i];
                var go = body.Root;
                var cargoGO = _cargoPool[i];
                go.SetActive(true);

                float x, y, z;

                if (m.State == MinionState.InSlot && m.TargetBuilding >= 0)
                {
                    // Inside building — positioned at slot-specific furniture
                    x = GetSlotWorldX(m.TargetBuilding, m.SlotIndex);
                    y = GetBuildingSlotY(m.TargetBuilding, m.SlotIndex);
                    z = InBuildingZ;
                    // No cargo visible inside buildings
                    cargoGO.SetActive(false);
                }
                else
                {
                    // Walking or idle — on the road surface
                    // Minion mesh pivot is at feet, so Y = road surface
                    x = m.X;
                    y = RoadY;
                    z = m.RenderZ;

                    // Render cargo volume if carrying something
                    float mw = (m.HasCart ? CartW : MinionW) * MinionScale;
                    float mh = (m.HasCart ? CartH : MinionH) * MinionScale;
                    if (!m.Cargo.IsEmpty)
                    {
                        cargoGO.SetActive(true);
                        float facingRad = m.FacingAngle * Mathf.Deg2Rad;
                        float cargoFwd = mw * 0.5f + m.Cargo.VisualWidth * 0.3f;
                        float cargoX = x + Mathf.Sin(facingRad) * cargoFwd;
                        float cargoY = y + mh * 0.5f + m.Cargo.CarryOffsetY;
                        float cargoZ = z + Mathf.Cos(facingRad) * cargoFwd;
                        cargoGO.transform.localPosition = new Vector3(cargoX, cargoY, cargoZ);
                        cargoGO.transform.localScale = new Vector3(
                            m.Cargo.VisualWidth, m.Cargo.VisualHeight, m.Cargo.VisualDepth);
                        cargoGO.transform.localRotation = Quaternion.Euler(0f, m.FacingAngle, 0f);
                        SetColor(cargoGO, Cargo.GetColor(m.Cargo.Kind));
                    }
                    else
                    {
                        cargoGO.SetActive(false);
                    }
                }

                go.transform.localPosition = new Vector3(x, y, z);
                go.transform.localScale = (m.HasCart ? new Vector3(CartW / MinionW, CartH / MinionH, CartDepth / MinionW) : Vector3.one) * MinionScale;
                go.transform.localRotation = Quaternion.Euler(0f, m.FacingAngle, 0f);

                // ── Walk animation ──
                bool walking = m.State == MinionState.Walking || m.State == MinionState.Hauling;
                if (walking)
                {
                    // Phase offset per minion so they don't all sync
                    float phase = Time.time * SWING_SPEED + m.Id * 1.7f;
                    float swing = Mathf.Sin(phase) * SWING_ANGLE;

                    if (_rig.ConnectedArms)
                    {
                        // Villager: connected arms swing gently as one unit
                        body.RightArm.localRotation = Quaternion.Euler(swing * 0.3f, 0f, 0f);
                        // Shorter leg swing for villager
                        body.RightLeg.localRotation = Quaternion.Euler(-swing * 0.6f, 0f, 0f);
                        body.LeftLeg.localRotation  = Quaternion.Euler( swing * 0.6f, 0f, 0f);
                    }
                    else
                    {
                        // Skeleton: full independent arm/leg swing
                        body.RightArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
                        body.LeftArm.localRotation  = Quaternion.Euler(-swing, 0f, 0f);
                        body.RightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
                        body.LeftLeg.localRotation  = Quaternion.Euler(swing, 0f, 0f);
                    }
                }
                else
                {
                    body.RightArm.localRotation = Quaternion.identity;
                    if (body.LeftArm != null)
                        body.LeftArm.localRotation  = Quaternion.identity;
                    body.RightLeg.localRotation = Quaternion.identity;
                    body.LeftLeg.localRotation  = Quaternion.identity;
                }

                // Assign skin material based on identity + profession overlay
                var skinMat = GetSkinMaterial(m);
                for (int r = 0; r < body.Renderers.Length; r++)
                {
                    if (body.Renderers[r].sharedMaterial != skinMat)
                        body.Renderers[r].sharedMaterial = skinMat;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SLOT POSITIONING — aligned to procedural blueprint furniture
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// World X for a specific slot within a building.
        /// Positions match the procedural blueprint interior layouts so
        /// minions appear at the correct furniture (lectern, pew, bed, bench).
        /// </summary>
        private float GetSlotWorldX(int origin, int slotIndex)
        {
            int bw = _city.GetBuildingWidth(origin);
            if (bw < 1) bw = 1;
            var type = _city.GetSurface(origin);
            int totalSlots = BuildingSlots.GetSlotCount(type, bw);

            float startX = origin * CityRenderer.CellSize;
            float buildingW = bw * CityRenderer.CellSize;

            if (totalSlots <= 1)
                return startX + buildingW * 0.5f;

            var role = BuildingSlots.GetSlotRole(type, slotIndex, bw);

            switch (type)
            {
                case CellType.Chapel:
                    if (role == SlotRole.Preacher)
                    {
                        // Lectern position — left 12% of building
                        return startX + buildingW * 0.12f;
                    }
                    else
                    {
                        // Pew positions — fill 28% to 95% of building
                        int pewIndex = slotIndex - 1; // slot 0 is preacher
                        int pewCount = totalSlots - 1;
                        float pewStart = startX + buildingW * 0.28f;
                        float pewEnd = startX + buildingW * 0.95f;
                        float pewRegion = pewEnd - pewStart;
                        float pewSpacing = pewRegion / Mathf.Max(1, pewCount);
                        return pewStart + pewSpacing * (pewIndex + 0.5f);
                    }

                case CellType.House:
                {
                    // Beds — evenly distributed across 5% to 95% of building
                    float bedRegion = buildingW * 0.9f;
                    int bedCount = Mathf.Max(1, bw);
                    float bedSpacing = bedRegion / bedCount;
                    int bedIndex = Mathf.Clamp(slotIndex / 2, 0, bedCount - 1); // 2 residents per bed
                    return startX + buildingW * 0.05f + bedSpacing * (bedIndex + 0.5f);
                }

                case CellType.Workshop:
                {
                    // Workbenches — evenly distributed across 5% to 95%
                    int benchCount = Mathf.Max(1, bw);
                    float benchRegion = buildingW * 0.9f;
                    float benchSpacing = benchRegion / benchCount;
                    int benchIndex = Mathf.Clamp(slotIndex / 2, 0, benchCount - 1);
                    return startX + buildingW * 0.05f + benchSpacing * (benchIndex + 0.5f);
                }

                case CellType.Market:
                {
                    // Stalls — merchant at left stall (slot 0), others at stalls
                    int stallCount = Mathf.Max(1, bw);
                    float stallRegion = buildingW * 0.9f;
                    float stallSpacing = stallRegion / stallCount;
                    int stallIndex = Mathf.Clamp(slotIndex, 0, stallCount - 1);
                    return startX + buildingW * 0.05f + stallSpacing * (stallIndex + 0.5f);
                }

                case CellType.Shipyard:
                {
                    // Foreman at left (slot 0), shipwrights along the drydock
                    if (slotIndex == 0)
                        return startX + buildingW * 0.10f;
                    int wrightCount = totalSlots - 1;
                    float wrightRegion = buildingW * 0.8f;
                    float wrightSpacing = wrightRegion / Mathf.Max(1, wrightCount);
                    return startX + buildingW * 0.15f + wrightSpacing * (slotIndex - 1 + 0.5f);
                }

                case CellType.Pier:
                {
                    // Dockers spread along the pier
                    float pierRegion = buildingW * 0.9f;
                    float pierSpacing = pierRegion / Mathf.Max(1, totalSlots);
                    return startX + buildingW * 0.05f + pierSpacing * (slotIndex + 0.5f);
                }

                case CellType.Warehouse:
                {
                    // Crane operators at front (1 per tile width), keeper at desk, haulers at shelves
                    if (slotIndex < bw)
                    {
                        // Crane operator — centered on their crane
                        return startX + (slotIndex + 0.5f) * CityRenderer.CellSize;
                    }
                    if (slotIndex == bw)
                    {
                        // Keeper at desk (far left)
                        return startX + buildingW * 0.05f;
                    }
                    // Haulers distributed across shelving area
                    int haulerIdx = slotIndex - bw - 1;
                    int haulerCount = totalSlots - bw - 1;
                    float haulerRegion = buildingW * 0.80f;
                    float haulerSpacing = haulerRegion / Mathf.Max(1, haulerCount);
                    return startX + buildingW * 0.10f + haulerSpacing * (haulerIdx + 0.5f);
                }

                default:
                {
                    // Generic even distribution
                    float margin = buildingW * 0.1f;
                    float usable = buildingW - margin * 2f;
                    float step = usable / (totalSlots - 1);
                    return startX + margin + Mathf.Clamp(slotIndex, 0, totalSlots - 1) * step;
                }
            }
        }

        /// <summary>
        /// World Y for a minion inside a building — placed at furniture height.
        /// Preacher stands taller (at lectern), worshippers sit (on pew), etc.
        /// </summary>
        private float GetBuildingSlotY(int origin, int slotIndex)
        {
            var type = _city.GetSurface(origin);
            int bw = _city.GetBuildingWidth(origin);
            if (bw < 1) bw = 1;
            var role = BuildingSlots.GetSlotRole(type, slotIndex, bw);

            switch (type)
            {
                case CellType.Chapel:
                    // Preacher stands at lectern; worshippers sit on pews (raised seat)
                    return role == SlotRole.Preacher
                        ? RoadY
                        : RoadY + 0.06f;

                case CellType.House:
                    // Resting in beds (slightly raised)
                    return RoadY + 0.04f;

                case CellType.Workshop:
                    // Standing at workbench
                    return RoadY;

                case CellType.Farm:
                    // Crouching among crops
                    return RoadY;

                case CellType.Market:
                    // Standing at counter
                    return RoadY;

                case CellType.Fountain:
                    return RoadY;

                case CellType.Shipyard:
                    // Foreman stands, shipwrights work at hull height
                    return RoadY;

                case CellType.Pier:
                    return RoadY;

                case CellType.Warehouse:
                    // Crane operators at ground in front; keeper at desk; haulers at shelf height
                    return RoadY;

                default:
                    return RoadY;
            }
        }

        private Color GetInBuildingColor(int origin)
        {
            var type = _city.GetSurface(origin);
            switch (type)
            {
                case CellType.Chapel:  return PrayColor;
                case CellType.House:   return RestColor;
                case CellType.Farm:
                case CellType.Market:  return EatColor;
                case CellType.Shipyard:
                case CellType.Pier:       return HarborColor;
                case CellType.Warehouse:  return WorkColor;
                default:                  return WorkColor;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private MinionBody CreateMinionBody(int index)
        {
            var root = new GameObject($"Minion_{index}");
            root.transform.SetParent(_parent.transform, false);

            var renderers = new List<MeshRenderer>();

            // Body (head + torso) — no pivot offset, mesh built with feet at origin
            var bodyGO = CreatePart("Body", root.transform, _rig.Body, Vector3.zero);
            renderers.Add(bodyGO.GetComponent<MeshRenderer>());

            // Arms — pivot at shoulder (or single connected-arm mesh)
            Transform rArmT = null, lArmT = null;
            if (_rig.ConnectedArms)
            {
                var arms = CreatePart("Arms", root.transform, _rig.RightArm, _rig.RightArmPos);
                renderers.Add(arms.GetComponent<MeshRenderer>());
                rArmT = arms.transform;
            }
            else
            {
                var rArm = CreatePart("RArm", root.transform, _rig.RightArm, _rig.RightArmPos);
                renderers.Add(rArm.GetComponent<MeshRenderer>());
                rArmT = rArm.transform;
                var lArm = CreatePart("LArm", root.transform, _rig.LeftArm, _rig.LeftArmPos);
                renderers.Add(lArm.GetComponent<MeshRenderer>());
                lArmT = lArm.transform;
            }

            // Legs — pivot at hip
            var rLeg = CreatePart("RLeg", root.transform, _rig.RightLeg, _rig.RightLegPos);
            renderers.Add(rLeg.GetComponent<MeshRenderer>());
            var lLeg = CreatePart("LLeg", root.transform, _rig.LeftLeg, _rig.LeftLegPos);
            renderers.Add(lLeg.GetComponent<MeshRenderer>());

            return new MinionBody
            {
                Root = root,
                RightArm = rArmT,
                LeftArm  = lArmT,
                RightLeg = rLeg.transform,
                LeftLeg  = lLeg.transform,
                Renderers = renderers.ToArray(),
            };
        }

        private GameObject CreatePart(string name, Transform parent, Mesh mesh, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _fallbackMaterial;
            return go;
        }

        private GameObject CreateCargoCube(int index)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Cargo_{index}";
            go.transform.SetParent(_cargoParent.transform, false);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.SetActive(false);
            return go;
        }

        private static void SetColor(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            var mat = r.material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;
        }
    }
}
