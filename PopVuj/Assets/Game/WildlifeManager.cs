// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.Time;

namespace PopVuj.Game
{
    /// <summary>
    /// Ambient wildlife — brings the city to life with birds, rats, bats, and toads.
    ///
    /// Sprite sheets from Resources/Animations/:
    ///   Each PNG is a vertical strip (64×64*N) containing animation frames.
    ///   Numbered variants (_1–4) are skin variations — each creature
    ///   picks a random skin at spawn and animates through its strip's frames.
    ///
    ///   Birds  — bluejay, duck, hawk, owl, pigeon, puffin, raven, seagull
    ///            Idle skins: {name}_1–4.png   Flying skins: {name}_flying_1–4.png
    ///   Ground — rat_1–4.png, toad_1–4.png
    ///   Air    — bat_1–2.png, bat_flying_1–4.png
    ///
    /// Behavior:
    ///   Birds perch on buildings, occasionally take flight across the city,
    ///   then land on another building. Seagulls circle the harbor.
    ///   Rats scurry along the sewer layer (below Y=0).
    ///   Bats hang in sewer spaces, startled into flight by nearby events.
    ///   Toads sit near the Fountain/Cistern area.
    ///
    /// Population scales with city size. Storm weather grounds birds,
    /// rain brings out toads, drought drives rats underground.
    /// </summary>
    public class WildlifeManager : MonoBehaviour
    {
        private CityGrid _city;
        private PopVujMatchManager _match;

        // Active creatures
        private readonly List<Creature> _creatures = new List<Creature>();
        private readonly List<GameObject> _creatureGOs = new List<GameObject>();
        private int _targetCount;

        // Sprite sheets per species: each entry is a skin variant (vertical strip)
        private readonly Dictionary<string, SpriteAnimator.SheetInfo[]> _idleSheets
            = new Dictionary<string, SpriteAnimator.SheetInfo[]>();
        private readonly Dictionary<string, SpriteAnimator.SheetInfo[]> _flySheets
            = new Dictionary<string, SpriteAnimator.SheetInfo[]>();

        // Shared quad mesh
        private Mesh _quad;
        private Shader _shader;

        // Timing
        private float _spawnTimer;
        private const float SPAWN_INTERVAL = 2f;
        private const float FRAME_RATE = 6f; // frames per second

        // Species definitions
        private static readonly string[] BirdSpecies = {
            "bluejay", "duck", "hawk", "owl", "pigeon", "puffin", "raven", "seagull"
        };
        private static readonly string[] GroundSpecies = { "rat", "toad" };

        private const float BirdSize = 0.35f;
        private const float GroundSize = 0.25f;
        private const float BatSize = 0.30f;
        private const float RoadY = 0.3f;

        // ═══════════════════════════════════════════════════════════════
        // CREATURE DATA
        // ═══════════════════════════════════════════════════════════════

        private enum CreatureState { Idle, Walking, Flying, Landing }
        private enum CreatureZone { Surface, Sewer, Sky, Harbor }

        private class Creature
        {
            public string Species;
            public CreatureState State;
            public CreatureZone Zone;
            public float X, Y;
            public float VelX, VelY;
            public float Size;
            public float StateTimer;
            public int FrameIndex;
            public float FrameTimer;
            public int FacingDir; // -1 left, 1 right
            public bool IsBird;
            public bool IsBat;
            public int SkinIndex;  // index into skin variant arrays
            public Material Mat;   // per-creature material
            public Mesh Mesh;      // per-creature mesh (UVs set per frame)
        }

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        public void Initialize(CityGrid city, PopVujMatchManager match)
        {
            _city = city;
            _match = match;
            _quad = SpriteAnimator.GetQuadMesh();
            _shader = Shader.Find("Particles/Standard Unlit")
                   ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                   ?? Shader.Find("Sprites/Default");

            LoadSpeciesFrames();
            _targetCount = Mathf.Clamp(city.Width / 3, 4, 20);
        }

        private void LoadSpeciesFrames()
        {
            // Birds: idle skins (_1 to _4), flying skins (_flying_1 to _flying_4)
            foreach (var species in BirdSpecies)
            {
                _idleSheets[species] = LoadVariants(species, "_", 1, 4);
                _flySheets[species] = LoadVariants(species, "_flying_", 1, 4);
            }

            // Rats: rat_1 to rat_4
            _idleSheets["rat"] = LoadVariants("rat", "_", 1, 4);

            // Toads: toad_1 to toad_4
            _idleSheets["toad"] = LoadVariants("toad", "_", 1, 4);

            // Bats: bat_1 to bat_2 (idle skins), bat_flying_1 to bat_flying_4 (flying skins)
            _idleSheets["bat"] = LoadVariants("bat", "_", 1, 2);
            _flySheets["bat"] = LoadVariants("bat", "_flying_", 1, 4);
        }

        /// <summary>
        /// Load numbered skin variants as SheetInfo[]. Each PNG is a vertical strip spritesheet.
        /// </summary>
        private SpriteAnimator.SheetInfo[] LoadVariants(string species, string sep, int from, int to)
        {
            var variants = new List<SpriteAnimator.SheetInfo>();
            for (int i = from; i <= to; i++)
            {
                var sheet = SpriteAnimator.LoadSheet($"{species}{sep}{i}");
                if (sheet.Texture != null)
                    variants.Add(sheet);
            }
            return variants.ToArray();
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_match == null || !_match.MatchInProgress || _match.GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;
            float dt = Time.deltaTime * Mathf.Min(timeScale, 4f);

            // Adjust target count based on weather
            int weatherMod = 0;
            if (_match.CurrentWeather == Weather.Storm) weatherMod = -4;
            if (_match.CurrentWeather == Weather.Rain) weatherMod = 2; // toads love rain
            _targetCount = Mathf.Clamp(_city.Width / 3 + weatherMod, 2, 20);

            // Spawn / cull
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SPAWN_INTERVAL;
                if (_creatures.Count < _targetCount)
                    SpawnCreature();
                else if (_creatures.Count > _targetCount + 2)
                    DespawnRandom();
            }

            // Update each creature
            for (int i = _creatures.Count - 1; i >= 0; i--)
                UpdateCreature(_creatures[i], dt, i);

            // Sync visuals
            SyncVisuals();
        }

        // ═══════════════════════════════════════════════════════════════
        // SPAWNING
        // ═══════════════════════════════════════════════════════════════

        private void SpawnCreature()
        {
            float cityW = _city.Width * CityRenderer.CellSize;
            var c = new Creature();

            // Weighted roll: 50% birds, 25% rats, 15% bats, 10% toads
            float roll = Random.value;
            if (roll < 0.50f)
            {
                // Bird
                c.Species = BirdSpecies[Random.Range(0, BirdSpecies.Length)];
                c.IsBird = true;
                c.Zone = CreatureZone.Surface;
                c.Size = BirdSize;
                // Start perched on a building
                c.X = Random.Range(1f, cityW - 1f);
                c.Y = Random.Range(2f, 4f); // above buildings
                c.State = CreatureState.Idle;
                c.StateTimer = Random.Range(3f, 8f);

                // Seagulls prefer the harbor zone
                if (c.Species == "seagull")
                {
                    c.X = cityW - Random.Range(1f, 5f);
                    c.Zone = CreatureZone.Harbor;
                }
            }
            else if (roll < 0.75f)
            {
                // Rat
                c.Species = "rat";
                c.Zone = CreatureZone.Sewer;
                c.Size = GroundSize;
                c.X = Random.Range(2f, cityW - 2f);
                c.Y = -Random.Range(0.3f, 1.0f); // in the sewer
                c.State = CreatureState.Walking;
                c.StateTimer = Random.Range(2f, 6f);
            }
            else if (roll < 0.90f)
            {
                // Bat
                c.Species = "bat";
                c.IsBat = true;
                c.Zone = CreatureZone.Sewer;
                c.Size = BatSize;
                c.X = Random.Range(2f, cityW - 2f);
                c.Y = -Random.Range(0.1f, 0.5f);
                c.State = CreatureState.Idle;
                c.StateTimer = Random.Range(4f, 12f);
            }
            else
            {
                // Toad
                c.Species = "toad";
                c.Zone = CreatureZone.Surface;
                c.Size = GroundSize * 0.8f;
                // Toads near fountains
                float fountainX = FindBuildingX(CellType.Fountain);
                c.X = fountainX + Random.Range(-1f, 1f);
                c.Y = RoadY + 0.05f;
                c.State = CreatureState.Idle;
                c.StateTimer = Random.Range(3f, 10f);
            }

            c.FacingDir = Random.value > 0.5f ? 1 : -1;
            c.FrameIndex = 0;
            c.FrameTimer = 0f;

            // Pick random skin variant
            int skinCount = 1;
            if (_idleSheets.TryGetValue(c.Species, out var skins) && skins.Length > 0)
                skinCount = skins.Length;
            c.SkinIndex = (skinCount > 1) ? Random.Range(0, skinCount) : 0;

            _creatures.Add(c);

            // Create visual with per-creature mesh + material
            var go = new GameObject($"Wildlife_{c.Species}_{_creatures.Count}");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            c.Mesh = CreateCreatureMesh();
            mf.mesh = c.Mesh;
            var mr = go.AddComponent<MeshRenderer>();

            var sheet = GetCurrentSheet(c);
            c.Mat = new Material(_shader);
            if (sheet.Texture != null) c.Mat.mainTexture = sheet.Texture;
            c.Mat.SetFloat("_ZWrite", 0f);
            c.Mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Set initial frame UVs directly on mesh
            SetMeshFrameUVs(c.Mesh, 0, sheet.FrameCount);

            mr.material = c.Mat;

            _creatureGOs.Add(go);
        }

        /// <summary>Creates a per-creature quad mesh so we can set UVs per-frame.</summary>
        private static Mesh CreateCreatureMesh()
        {
            var m = new Mesh { name = "CreatureQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            m.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1),
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            return m;
        }

        /// <summary>Sets quad UVs to show a specific frame from a vertical strip.</summary>
        private static void SetMeshFrameUVs(Mesh mesh, int frameIndex, int frameCount)
        {
            if (frameCount <= 1)
                return; // full texture, default UVs are fine

            float fh = 1f / frameCount;
            // Frame 0 = top of image = highest UV Y
            float vTop = 1f - frameIndex * fh;
            float vBot = vTop - fh;
            mesh.uv = new[]
            {
                new Vector2(0, vBot), new Vector2(1, vBot),
                new Vector2(1, vTop), new Vector2(0, vTop),
            };
        }

        private void DespawnRandom()
        {
            if (_creatures.Count == 0) return;
            int idx = Random.Range(0, _creatures.Count);
            if (_creatureGOs[idx] != null)
                Destroy(_creatureGOs[idx]);
            _creatures.RemoveAt(idx);
            _creatureGOs.RemoveAt(idx);
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATURE AI
        // ═══════════════════════════════════════════════════════════════

        private void UpdateCreature(Creature c, float dt, int idx)
        {
            float cityW = _city.Width * CityRenderer.CellSize;

            // Advance animation frame
            c.FrameTimer += dt * FRAME_RATE;
            if (c.FrameTimer >= 1f)
            {
                c.FrameTimer -= 1f;
                c.FrameIndex++;
            }

            // State timer
            c.StateTimer -= dt;

            switch (c.State)
            {
                case CreatureState.Idle:
                    if (c.StateTimer <= 0f)
                    {
                        if (c.IsBird || c.IsBat)
                        {
                            // Take flight
                            c.State = CreatureState.Flying;
                            c.VelX = c.FacingDir * Random.Range(1.5f, 3f);
                            c.VelY = Random.Range(0.5f, 1.5f);
                            c.StateTimer = Random.Range(2f, 5f);
                        }
                        else
                        {
                            // Start walking
                            c.State = CreatureState.Walking;
                            c.VelX = c.FacingDir * Random.Range(0.3f, 0.8f);
                            c.StateTimer = Random.Range(2f, 5f);
                        }
                    }
                    break;

                case CreatureState.Walking:
                    c.X += c.VelX * dt;
                    // Bounce off city edges
                    if (c.X < 0.5f || c.X > cityW - 0.5f)
                    {
                        c.FacingDir *= -1;
                        c.VelX *= -1;
                        c.X = Mathf.Clamp(c.X, 0.5f, cityW - 0.5f);
                    }
                    if (c.StateTimer <= 0f)
                    {
                        c.State = CreatureState.Idle;
                        c.VelX = 0f;
                        c.StateTimer = Random.Range(2f, 6f);
                    }
                    break;

                case CreatureState.Flying:
                    c.X += c.VelX * dt;
                    c.Y += c.VelY * dt;
                    // Arc: slow down vertical, then descend
                    c.VelY -= dt * 0.5f;

                    // Seagulls circle: sinusoidal Y
                    if (c.Species == "seagull")
                        c.VelY = Mathf.Sin(Time.time * 1.5f + idx) * 0.8f;

                    // Wrap at city edges (fly off-screen then reappear)
                    if (c.X < -2f) c.X = cityW + 1f;
                    if (c.X > cityW + 2f) c.X = -1f;

                    // Land after timer
                    if (c.StateTimer <= 0f && c.Y > 1f)
                    {
                        c.State = CreatureState.Landing;
                        c.VelY = -1.5f;
                        c.StateTimer = 1.5f;
                    }

                    // Bats in sewers: constrain Y
                    if (c.IsBat && c.Zone == CreatureZone.Sewer)
                        c.Y = Mathf.Clamp(c.Y, -1.5f, -0.1f);
                    break;

                case CreatureState.Landing:
                    c.X += c.VelX * 0.5f * dt; // slow horizontal
                    c.Y += c.VelY * dt;
                    float landY = c.IsBat ? -Random.Range(0.1f, 0.5f) : Random.Range(1.5f, 3.5f);
                    if (c.Y <= landY || c.StateTimer <= 0f)
                    {
                        c.Y = landY;
                        c.State = CreatureState.Idle;
                        c.VelX = 0f;
                        c.VelY = 0f;
                        c.StateTimer = Random.Range(3f, 10f);
                        c.FacingDir = Random.value > 0.5f ? 1 : -1;
                    }
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // VISUAL SYNC
        // ═══════════════════════════════════════════════════════════════

        private void SyncVisuals()
        {
            for (int i = 0; i < _creatures.Count && i < _creatureGOs.Count; i++)
            {
                var c = _creatures[i];
                var go = _creatureGOs[i];
                if (go == null || c.Mat == null) continue;

                // Position
                go.transform.localPosition = new Vector3(c.X, c.Y, -0.3f);
                go.transform.localScale = new Vector3(c.Size * c.FacingDir, c.Size, c.Size);

                // Select skin variant's strip and animate via mesh UVs
                var sheet = GetCurrentSheet(c);
                if (sheet.Texture != null)
                {
                    // Swap texture on state transitions (idle ↔ flying)
                    if (c.Mat.mainTexture != sheet.Texture)
                        c.Mat.mainTexture = sheet.Texture;

                    int frameCount = Mathf.Max(sheet.FrameCount, 1);
                    int frame = c.FrameIndex % frameCount;
                    if (c.Mesh != null)
                        SetMeshFrameUVs(c.Mesh, frame, frameCount);
                }

                // Billboard
                FaceCamera(go.transform, c.FacingDir);
            }
        }

        /// <summary>
        /// Returns the SheetInfo for the creature's current state and skin variant.
        /// </summary>
        private SpriteAnimator.SheetInfo GetCurrentSheet(Creature c)
        {
            bool flying = c.State == CreatureState.Flying || c.State == CreatureState.Landing;

            if (flying && _flySheets.TryGetValue(c.Species, out var flySheets) && flySheets.Length > 0)
                return flySheets[c.SkinIndex % flySheets.Length];

            if (_idleSheets.TryGetValue(c.Species, out var idleSheets) && idleSheets.Length > 0)
                return idleSheets[c.SkinIndex % idleSheets.Length];

            return default;
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private float FindBuildingX(CellType type)
        {
            for (int i = 0; i < _city.Width; i++)
            {
                if (_city.GetSurface(i) == type && _city.GetOwner(i) == i)
                    return (i + _city.GetBuildingWidth(i) * 0.5f) * CityRenderer.CellSize;
            }
            return _city.Width * CityRenderer.CellSize * 0.5f;
        }

        private static void FaceCamera(Transform t, int facingDir)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var lookDir = t.position - cam.transform.position;
            lookDir.y = 0f; // keep upright
            if (lookDir.sqrMagnitude > 0.001f)
                t.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }
    }
}
