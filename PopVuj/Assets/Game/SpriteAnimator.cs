// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections.Generic;
using UnityEngine;

namespace PopVuj.Game
{
    /// <summary>
    /// Loads PNG frame sequences from Resources/Animations/ and provides:
    ///   - Single-texture materials (rain.png, snow.png, etc.)
    ///   - Runtime texture atlases packed from numbered frames (explosion_0–15)
    ///   - Quad mesh shared across all billboard sprites
    ///
    /// All textures use Point filtering for pixel-art fidelity.
    /// Materials use "Particles/Standard Unlit" for additive/alpha-blended rendering.
    /// </summary>
    public static class SpriteAnimator
    {
        // Cached single textures: "rain" → Texture2D
        private static readonly Dictionary<string, Texture2D> _textureCache
            = new Dictionary<string, Texture2D>();

        // Cached atlas results: "explosion" → (atlas, cols, rows, frameCount)
        private static readonly Dictionary<string, AtlasResult> _atlasCache
            = new Dictionary<string, AtlasResult>();

        // Shared quad mesh (unit billboard, pivot at center)
        private static Mesh _quadMesh;

        // Shader references
        private static Shader _particleShader;
        private static Shader _particleAdditiveShader;

        public struct AtlasResult
        {
            public Texture2D Atlas;
            public int Columns;
            public int Rows;
            public int FrameCount;
        }

        /// <summary>
        /// Metadata for a loaded sprite sheet. Single frames have FrameCount == 1.
        /// Vertical strips (height == width * n, n >= 2) have FrameCount == n.
        /// </summary>
        public struct SheetInfo
        {
            public Texture2D Texture;
            public int FrameWidth;
            public int FrameHeight;
            public int FrameCount;
            public bool IsStrip;
        }

        // ═══════════════════════════════════════════════════════════════
        // SINGLE TEXTURE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a single sprite texture from Resources/Animations/{name}.
        /// Returns null if not found. Cached.
        /// </summary>
        public static Texture2D LoadTexture(string name)
        {
            if (_textureCache.TryGetValue(name, out var cached))
                return cached;

            var tex = Resources.Load<Texture2D>($"Animations/{name}");
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            _textureCache[name] = tex;
            return tex;
        }

        /// <summary>
        /// Create a particle material for a single texture.
        /// </summary>
        public static Material CreateMaterial(string textureName, bool additive = false)
        {
            var tex = LoadTexture(textureName);
            if (tex == null) return null;
            return CreateMaterialFromTexture(tex, additive);
        }

        public static Material CreateMaterialFromTexture(Texture2D tex, bool additive = false)
        {
            EnsureShaders();
            var shader = additive ? _particleAdditiveShader : _particleShader;
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.mainTexture = tex;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);
            return mat;
        }

        // ═══════════════════════════════════════════════════════════════
        // VERTICAL STRIP SHEETS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a texture and detect whether it's a vertical sprite strip.
        /// A vertical strip has height > width and height % width == 0.
        /// </summary>
        public static SheetInfo LoadSheet(string name)
        {
            var tex = LoadTexture(name);
            if (tex == null) return default;

            int w = tex.width;
            int h = tex.height;
            bool isStrip = w > 0 && h > w && h % w == 0;
            int frameCount = isStrip ? h / w : 1;

            return new SheetInfo
            {
                Texture = tex,
                FrameWidth = w,
                FrameHeight = isStrip ? w : h,
                FrameCount = frameCount,
                IsStrip = isStrip,
            };
        }

        /// <summary>
        /// Get UV offset and scale for a frame in a vertical strip.
        /// Frame 0 = topmost row. Returns (offsetX, offsetY, scaleX, scaleY).
        /// </summary>
        public static Vector4 GetStripUV(int frameIndex, int frameCount)
        {
            if (frameCount <= 1)
                return new Vector4(0f, 0f, 1f, 1f);
            float frameH = 1f / frameCount;
            float vOffset = 1f - (frameIndex + 1) * frameH;
            return new Vector4(0f, vOffset, 1f, frameH);
        }

        /// <summary>
        /// Extract individual frame Texture2Ds from a vertical strip.
        /// Returns single-element array if not a strip.
        /// </summary>
        public static Texture2D[] ExtractFrames(SheetInfo sheet)
        {
            if (!sheet.IsStrip || sheet.FrameCount <= 1)
                return new[] { sheet.Texture };

            var readable = MakeReadable(sheet.Texture);
            var frames = new Texture2D[sheet.FrameCount];
            int fw = sheet.FrameWidth;
            int fh = sheet.FrameHeight;
            for (int i = 0; i < sheet.FrameCount; i++)
            {
                // Frame 0 = top of image = highest Y in pixel coords
                int srcY = (sheet.FrameCount - 1 - i) * fh;
                var pixels = readable.GetPixels(0, srcY, fw, fh);
                var frame = new Texture2D(fw, fh, TextureFormat.RGBA32, false);
                frame.filterMode = FilterMode.Point;
                frame.wrapMode = TextureWrapMode.Clamp;
                frame.SetPixels(pixels);
                frame.Apply();
                frames[i] = frame;
            }
            return frames;
        }

        // ═══════════════════════════════════════════════════════════════
        // FRAME SEQUENCE → ATLAS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Load a numbered frame sequence (e.g. "explosion" → explosion_0, explosion_1, ...)
        /// and pack into a grid atlas. Returns the atlas info. Cached.
        /// </summary>
        public static AtlasResult LoadAtlas(string baseName, int maxFrames = 64)
        {
            if (_atlasCache.TryGetValue(baseName, out var cached))
                return cached;

            // Try single vertical strip file first (e.g. "explosion.png" = all frames)
            var sheet = LoadSheet(baseName);
            if (sheet.IsStrip)
            {
                var stripResult = new AtlasResult
                {
                    Atlas = sheet.Texture,
                    Columns = 1,
                    Rows = sheet.FrameCount,
                    FrameCount = sheet.FrameCount,
                };
                _atlasCache[baseName] = stripResult;
                return stripResult;
            }

            // Fallback: numbered separate files (TBD to combine into strips)
            var frames = new List<Texture2D>();
            for (int i = 0; i < maxFrames; i++)
            {
                var tex = Resources.Load<Texture2D>($"Animations/{baseName}_{i}");
                if (tex == null) break;
                frames.Add(tex);
            }

            if (frames.Count == 0)
            {
                var result = new AtlasResult { FrameCount = 0 };
                _atlasCache[baseName] = result;
                return result;
            }

            int cols = Mathf.CeilToInt(Mathf.Sqrt(frames.Count));
            int rows = Mathf.CeilToInt((float)frames.Count / cols);
            int frameW = frames[0].width;
            int frameH = frames[0].height;
            int atlasW = cols * frameW;
            int atlasH = rows * frameH;

            var atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, false);
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = TextureWrapMode.Clamp;

            // Fill with transparent
            var clear = new Color32[atlasW * atlasH];
            atlas.SetPixels32(clear);

            // Blit frames into grid (top-left to bottom-right, row-major)
            for (int i = 0; i < frames.Count; i++)
            {
                int c = i % cols;
                int r = rows - 1 - (i / cols); // Unity Y=0 is bottom

                // Use RenderTexture to copy without needing Read/Write import flag
                var readable = MakeReadable(frames[i]);
                var pixels = readable.GetPixels(0, 0, frameW, frameH);
                atlas.SetPixels(c * frameW, r * frameH, frameW, frameH, pixels);
            }
            atlas.Apply();

            var atlasResult = new AtlasResult
            {
                Atlas = atlas,
                Columns = cols,
                Rows = rows,
                FrameCount = frames.Count,
            };
            _atlasCache[baseName] = atlasResult;
            return atlasResult;
        }

        /// <summary>
        /// Create a particle material from an atlas with TextureSheetAnimation
        /// configuration data. Caller configures the ParticleSystem TSA module.
        /// </summary>
        public static Material CreateAtlasMaterial(string baseName, bool additive = false)
        {
            var atlas = LoadAtlas(baseName);
            if (atlas.FrameCount == 0) return null;
            return CreateMaterialFromTexture(atlas.Atlas, additive);
        }

        // ═══════════════════════════════════════════════════════════════
        // QUAD MESH (shared billboard)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Unit quad mesh centered at origin, facing +Z.</summary>
        public static Mesh GetQuadMesh()
        {
            if (_quadMesh != null) return _quadMesh;

            _quadMesh = new Mesh { name = "SpriteQuad" };
            _quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
            };
            _quadMesh.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1),
            };
            _quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _quadMesh.RecalculateNormals();
            return _quadMesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static void EnsureShaders()
        {
            if (_particleShader == null)
                _particleShader = Shader.Find("Particles/Standard Unlit")
                               ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                               ?? Shader.Find("Sprites/Default");
            if (_particleAdditiveShader == null)
                _particleAdditiveShader = Shader.Find("Particles/Standard Unlit")
                                       ?? Shader.Find("Sprites/Default");
        }

        private static Texture2D MakeReadable(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();
            readable.filterMode = FilterMode.Point;
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }
    }
}
