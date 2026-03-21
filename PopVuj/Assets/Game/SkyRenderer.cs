// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using CodeGamified.Time;
using PopVuj.Core;

namespace PopVuj.Game
{
    /// <summary>
    /// Multi-layer sky renderer driven by OptiFine-style .properties files.
    ///
    /// Composites sky*.properties layers into a RenderTexture each frame,
    /// displayed on a world-space inverted hemisphere mesh at Y≥0.
    ///
    /// Dome at Y≥0 = composited sky (starfields, clouds, sunflares).
    /// Below Y=0  = camera's dark solid background (sewers/underground).
    ///
    /// The dome follows the camera XZ each frame (no parallax), but stays
    /// at Y=0 so the world horizon stays at ground level from any angle.
    ///
    /// Supports:
    ///   - Time-based fade in 10% alpha steps (pixel-art crossfade)
    ///   - Midnight wrapping (e.g. 18:30 → 05:25)
    ///   - Weather filtering (clear/rain/thunder)
    ///   - UV scrolling for cloud/star drift
    /// </summary>
    public class SkyRenderer : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════
        // SKY LAYER DATA (no GameObjects — just data + materials for blitting)
        // ═══════════════════════════════════════════════════════════════

        private class SkyLayer
        {
            public float startFadeIn;
            public float endFadeIn;
            public float startFadeOut;
            public float endFadeOut;
            public bool wraps;
            public bool rotate;
            public float speed;
            public Vector3 axis;
            public HashSet<string> weather;
            public Material material;
            public float currentAlpha;
            public Vector2 uvOffset;
        }

        private readonly List<SkyLayer> _layers = new List<SkyLayer>();
        private PopVujMatchManager _match;
        private Camera _cam;

        // Sky dome (world-space hemisphere at Y≥0)
        private RenderTexture _skyRT;
        private GameObject _domeGO;
        private Material _domeMat;
        private Mesh _domeMesh;

        private const int RT_WIDTH = 1024;
        private const int RT_HEIGHT = 512;
        private const string SKY_PATH = "Textures/sky/world0";
        private const float DOME_RADIUS = 150f;
        private const int DOME_H_SEGS = 48;
        private const int DOME_V_SEGS = 24;
        private const float ALPHA_STEP = 0.1f;

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZE
        // ═══════════════════════════════════════════════════════════════

        public void Initialize(PopVujMatchManager match, CityGrid city)
        {
            _match = match;
            _cam = Camera.main;

            // Clean up stale Skybox component from previous implementation
            var staleSkybox = _cam.GetComponent<Skybox>();
            if (staleSkybox != null) Destroy(staleSkybox);
            _cam.clearFlags = CameraClearFlags.SolidColor;

            // Sky composite RenderTexture
            _skyRT = new RenderTexture(RT_WIDTH, RT_HEIGHT, 0, RenderTextureFormat.ARGB32);
            _skyRT.wrapMode = TextureWrapMode.Repeat;
            _skyRT.filterMode = FilterMode.Point;
            _skyRT.Create();

            // World-space hemisphere dome at Y≥0
            CreateDome();

            LoadSkyLayers();
        }

        private void OnDestroy()
        {
            if (_skyRT != null) { _skyRT.Release(); Destroy(_skyRT); }
            if (_domeMat != null) Destroy(_domeMat);
            if (_domeMesh != null) Destroy(_domeMesh);
            foreach (var layer in _layers)
                if (layer.material != null) Destroy(layer.material);
        }

        // ═══════════════════════════════════════════════════════════════
        // SKY DOME
        // ═══════════════════════════════════════════════════════════════

        private void CreateDome()
        {
            _domeMesh = GenerateHemisphere(DOME_RADIUS, DOME_H_SEGS, DOME_V_SEGS);

            var shader = Shader.Find("PopVuj/Skybox");
            _domeMat = new Material(shader);
            _domeMat.SetTexture("_SkyTex", _skyRT);

            _domeGO = new GameObject("SkyDome");
            _domeGO.transform.SetParent(transform, false);

            var mf = _domeGO.AddComponent<MeshFilter>();
            mf.mesh = _domeMesh;

            var mr = _domeGO.AddComponent<MeshRenderer>();
            mr.material = _domeMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        /// <summary>
        /// Generates an inverted hemisphere mesh (Y≥0, inward-facing).
        /// UV: U = azimuth (0..1 = 360°), V = elevation (0=horizon, 1=zenith).
        /// </summary>
        private static Mesh GenerateHemisphere(float radius, int hSegs, int vSegs)
        {
            int vertCount = (vSegs + 1) * (hSegs + 1);
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            int idx = 0;
            for (int v = 0; v <= vSegs; v++)
            {
                float elev = (v / (float)vSegs) * Mathf.PI * 0.5f;
                float y = Mathf.Sin(elev) * radius;
                float ring = Mathf.Cos(elev) * radius;

                for (int h = 0; h <= hSegs; h++)
                {
                    float azim = (h / (float)hSegs) * Mathf.PI * 2f;
                    verts[idx] = new Vector3(Mathf.Cos(azim) * ring, y, Mathf.Sin(azim) * ring);
                    uvs[idx] = new Vector2(h / (float)hSegs, v / (float)vSegs);
                    idx++;
                }
            }

            int triCount = vSegs * hSegs * 6;
            var tris = new int[triCount];
            int ti = 0;
            int w = hSegs + 1;

            for (int v = 0; v < vSegs; v++)
            {
                for (int h = 0; h < hSegs; h++)
                {
                    int a = v * w + h;
                    int b = a + 1;
                    int c = a + w;
                    int d = c + 1;
                    // CW winding → outward-facing. Shader uses Cull Front
                    // so back faces (inward) render — visible from inside dome.
                    tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
                }
            }

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.up * radius * 0.5f, Vector3.one * radius * 2f);
            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // LAYER LOADING
        // ═══════════════════════════════════════════════════════════════

        private void LoadSkyLayers()
        {
            string basePath = Path.Combine(Application.dataPath, "Resources",
                SKY_PATH.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(basePath)) return;

            var files = Directory.GetFiles(basePath, "sky*.properties");
            Array.Sort(files);

            foreach (var filePath in files)
            {
                try
                {
                    var props = ParseProperties(File.ReadAllText(filePath));
                    var layer = BuildLayer(props);
                    if (layer != null) _layers.Add(layer);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SkyRenderer] Failed to parse {Path.GetFileName(filePath)}: {e.Message}");
                }
            }
        }

        private SkyLayer BuildLayer(Dictionary<string, string> props)
        {
            if (!props.TryGetValue("source", out string source)) return null;

            // Require time fields
            if (!props.TryGetValue("startfadein", out string sfiStr)) return null;
            if (!props.TryGetValue("endfadein", out string efiStr)) return null;
            if (!props.TryGetValue("endfadeout", out string efoStr)) return null;

            float sfi = ParseTime(sfiStr);
            float efi = ParseTime(efiStr);
            float efo = ParseTime(efoStr);

            float sfo;
            if (props.TryGetValue("startfadeout", out string sfoStr))
                sfo = ParseTime(sfoStr);
            else
                sfo = Mathf.Max(efi, efo - 0.25f);

            // Load texture
            string texName = source.TrimStart('.', '/');
            if (texName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                texName = texName.Substring(0, texName.Length - 4);

            var texture = Resources.Load<Texture2D>($"{SKY_PATH}/{texName}");
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;

            // Weather filter
            var weatherSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (props.TryGetValue("weather", out string wStr))
            {
                foreach (var w in wStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    weatherSet.Add(w.Trim());
            }

            // Rotation / UV scroll
            bool rotate = GetProp(props, "rotate", "false")
                .Equals("true", StringComparison.OrdinalIgnoreCase);
            float speed = 0.5f;
            if (props.TryGetValue("speed", out string spStr))
                float.TryParse(spStr, NumberStyles.Float, CultureInfo.InvariantCulture, out speed);

            Vector3 axis = Vector3.up;
            if (props.TryGetValue("axis", out string axStr))
            {
                var p = axStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (p.Length >= 3)
                {
                    float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax);
                    float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay);
                    float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float az);
                    axis = new Vector3(ax, ay, az);
                }
            }

            // All layers additive (we composite from black — no base sky to darken)
            var mat = CreateBlitMat(texture);

            return new SkyLayer
            {
                startFadeIn = sfi,
                endFadeIn = efi,
                startFadeOut = sfo,
                endFadeOut = efo,
                wraps = efo < sfi,
                rotate = rotate,
                speed = speed,
                axis = axis,
                weather = weatherSet,
                material = mat,
                currentAlpha = 0f,
                uvOffset = Vector2.zero,
            };
        }

        private static Material CreateBlitMat(Texture2D tex)
        {
            var shader = Shader.Find("Hidden/PopVuj/SkyBlit");
            var mat = new Material(shader);
            mat.mainTexture = tex;
            return mat;
        }

        // ═══════════════════════════════════════════════════════════════
        // PROPERTIES PARSER
        // ═══════════════════════════════════════════════════════════════

        private static Dictionary<string, string> ParseProperties(string text)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim().Replace("\\:", ":");
                dict[key] = value;
            }
            return dict;
        }

        private static float ParseTime(string s)
        {
            s = s.Replace("\\:", ":").Trim();
            var parts = s.Split(':');
            if (parts.Length >= 2
                && int.TryParse(parts[0], out int h)
                && int.TryParse(parts[1], out int m))
                return h + m / 60f;
            if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return f;
            return 0f;
        }

        private static string GetProp(Dictionary<string, string> d, string key, string def)
        {
            return d.TryGetValue(key, out string v) ? v : def;
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        private void Update()
        {
            // Keep dome centered on camera XZ, pinned at Y=0 (world horizon)
            if (_cam == null) _cam = Camera.main;
            if (_cam != null && _domeGO != null)
            {
                var cp = _cam.transform.position;
                _domeGO.transform.position = new Vector3(cp.x, 0f, cp.z);
            }

            var simTime = SimulationTime.Instance as PopVujSimulationTime;
            if (simTime == null) return;

            float hour = simTime.GetTimeOfDay();
            string weather = GetWeatherString();
            float dt = Time.deltaTime;

            // Update layer alpha + UV scroll
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                bool weatherOk = layer.weather.Count == 0 || layer.weather.Contains(weather);
                float target = weatherOk ? GetLayerAlpha(layer, hour) : 0f;
                layer.currentAlpha = Mathf.Round(target / ALPHA_STEP) * ALPHA_STEP;

                if (layer.rotate)
                {
                    var off = layer.uvOffset;
                    off.x += layer.speed * 0.005f * layer.axis.y * dt;
                    off.y -= layer.speed * 0.005f * layer.axis.x * dt;
                    layer.uvOffset = off;
                }
            }

            CompositeSky();
        }

        // ═══════════════════════════════════════════════════════════════
        // COMPOSITING — blit layers into sky RenderTexture
        // ═══════════════════════════════════════════════════════════════

        private void CompositeSky()
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _skyRT;

            GL.PushMatrix();
            GL.LoadOrtho();
            GL.Clear(true, true, Color.black);

            // Composite sky layers additively (full RT = upper hemisphere)
            for (int i = 0; i < _layers.Count; i++)
            {
                var layer = _layers[i];
                if (layer.currentAlpha < 0.001f) continue;

                GL.Color(new Color(1f, 1f, 1f, layer.currentAlpha));
                layer.material.SetPass(0);
                DrawQuad(0f, 0f, 1f, 1f,
                    layer.uvOffset.x, layer.uvOffset.y,
                    1f + layer.uvOffset.x, 1f + layer.uvOffset.y);
            }

            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        private static void DrawQuad(float x0, float y0, float x1, float y1,
                                     float u0, float v0, float u1, float v1)
        {
            GL.Begin(GL.QUADS);
            GL.TexCoord2(u0, v0); GL.Vertex3(x0, y0, 0);
            GL.TexCoord2(u1, v0); GL.Vertex3(x1, y0, 0);
            GL.TexCoord2(u1, v1); GL.Vertex3(x1, y1, 0);
            GL.TexCoord2(u0, v1); GL.Vertex3(x0, y1, 0);
            GL.End();
        }

        // ═══════════════════════════════════════════════════════════════
        // ALPHA CALCULATION
        // ═══════════════════════════════════════════════════════════════

        private static float GetLayerAlpha(SkyLayer layer, float hour)
        {
            if (layer.wraps)
            {
                float shift = layer.startFadeIn;
                float t   = (hour - shift + 24f) % 24f;
                float efi = (layer.endFadeIn - shift + 24f) % 24f;
                float sfo = (layer.startFadeOut - shift + 24f) % 24f;
                float efo = (layer.endFadeOut - shift + 24f) % 24f;
                return AlphaLinear(t, 0f, efi, sfo, efo);
            }
            return AlphaLinear(hour, layer.startFadeIn, layer.endFadeIn,
                               layer.startFadeOut, layer.endFadeOut);
        }

        private static float AlphaLinear(float t, float sfi, float efi, float sfo, float efo)
        {
            if (t < sfi) return 0f;
            if (efi > sfi && t < efi) return Mathf.InverseLerp(sfi, efi, t);
            if (t <= sfo) return 1f;
            if (efo > sfo) return 1f - Mathf.InverseLerp(sfo, efo, t);
            return 0f;
        }

        // ═══════════════════════════════════════════════════════════════
        // WEATHER MAPPING
        // ═══════════════════════════════════════════════════════════════

        private string GetWeatherString()
        {
            if (_match == null) return "clear";
            switch (_match.CurrentWeather)
            {
                case Weather.Rain:    return "rain";
                case Weather.Storm:   return "thunder";
                case Weather.Drought: return "clear";
                default:              return "clear";
            }
        }
    }
}
