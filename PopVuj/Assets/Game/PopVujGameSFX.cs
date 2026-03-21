// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Settings;

namespace PopVuj.Game
{
    /// <summary>
    /// Game-event SFX — one-shot sounds triggered by match/harbor/city events.
    /// Complements <c>PopVujAudioProvider</c> (engine-level) and
    /// <c>PopVujAmbientSFX</c> (zone-based loops).
    ///
    /// All clips from Resources/SFX/. Wired by <c>PopVujBootstrap.WireEvents()</c>.
    /// </summary>
    public class PopVujGameSFX : MonoBehaviour
    {
        private AudioSource _src;

        // ── Villager events ─────────────────────────────────────
        private AudioClip _villagerDeath;
        private AudioClip[] _villagerHit;       // hit1-4 (smite, bears)
        private AudioClip[] _villagerHaggle;    // haggle1-3 (trade)

        // ── Construction / destruction ───────────────────────────
        private AudioClip _anvilBreak;          // building destroyed
        private AudioClip _chop;                // tree harvested / building placed
        private AudioClip[] _bowhit;            // bowhit1-4 (bears summoned)

        public void Initialize()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;

            _villagerDeath  = Load("mob/villager/death");
            _villagerHit    = LoadBank("mob/villager/hit", 1, 4);
            _villagerHaggle = LoadBank("mob/villager/haggle", 1, 3);

            _anvilBreak = Load("random/anvil_break");
            _chop       = Load("extra/chop");
            _bowhit     = LoadBank("random/bowhit", 1, 4);
        }

        // ═══════════════════════════════════════════════════════════
        //  PUBLIC HOOKS — call from WireEvents
        // ═══════════════════════════════════════════════════════════

        /// <summary>Population decreased — play villager death.</summary>
        public void OnPopulationChanged(int pop, int prevPop)
        {
            if (pop < prevPop)
                Play(_villagerDeath, 0.3f);
        }

        /// <summary>Smite triggered — random villager hit.</summary>
        public void OnSmite()
        {
            PlayRandom(_villagerHit, 0.35f);
        }

        /// <summary>Bears summoned — barrage of impacts.</summary>
        public void OnBearsSummoned(int kills)
        {
            // Play multiple hits for dramatic effect (stagger not needed — they overlap)
            for (int i = 0; i < Mathf.Min(kills, 3); i++)
                PlayRandom(_bowhit, 0.3f);
            PlayRandom(_villagerHit, 0.4f);
        }

        /// <summary>Ship returned to harbor — trade haggle.</summary>
        public void OnShipReturned(Ship ship)
        {
            PlayRandom(_villagerHaggle, 0.25f);
        }

        /// <summary>Building placed or tree harvested — chop.</summary>
        public void OnBoardChanged()
        {
            // Only play occasionally to avoid spam (grid changes often)
            if (Random.value < 0.15f)
                Play(_chop, 0.2f);
        }

        /// <summary>Omen sent — prophet arrival.</summary>
        public void OnOmen()
        {
            Play(_chop, 0.15f);
        }

        /// <summary>Building destroyed — anvil break.</summary>
        public void OnBuildingDestroyed()
        {
            Play(_anvilBreak, 0.35f);
        }

        // ═══════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════

        private void Play(AudioClip clip, float volume)
        {
            if (_src != null && clip != null)
                _src.PlayOneShot(clip, volume * SettingsBridge.SfxVolume);
        }

        private void PlayRandom(AudioClip[] bank, float volume)
        {
            if (bank == null || bank.Length == 0) return;
            Play(bank[Random.Range(0, bank.Length)], volume);
        }

        private static AudioClip Load(string path)
        {
            var clip = Resources.Load<AudioClip>($"SFX/{path}");
            if (clip == null)
                Debug.LogWarning($"[GameSFX] Missing clip: SFX/{path}");
            return clip;
        }

        private static AudioClip[] LoadBank(string prefix, int from, int to)
        {
            var clips = new AudioClip[to - from + 1];
            for (int i = from; i <= to; i++)
                clips[i - from] = Load($"{prefix}{i}");
            return clips;
        }
    }
}
