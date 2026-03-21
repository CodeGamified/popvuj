// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using CodeGamified.Audio;
using CodeGamified.Settings;
using UnityEngine;

namespace PopVuj.Game
{
    /// <summary>
    /// Implements <see cref="IAudioProvider"/> using PopVuj's Resources/SFX clips.
    /// Loads clips via Resources.Load at construction — all paths are relative to
    /// Resources/SFX/. Random variant selection for multi-clip banks.
    /// </summary>
    public class PopVujAudioProvider : IAudioProvider
    {
        readonly AudioSource _src;

        // ── Editor ──────────────────────────────────────────────
        readonly AudioClip _tap;           // anvil_use
        readonly AudioClip _insert;        // door_open
        readonly AudioClip _delete;        // door_close
        readonly AudioClip _undo;          // anvil_land
        readonly AudioClip _navigate;      // bow
        readonly AudioClip[] _compileOk;   // villager/yes1-3
        readonly AudioClip[] _compileErr;  // villager/no1-3

        // ── Engine ──────────────────────────────────────────────
        readonly AudioClip[] _output;      // villager/idle1-6
        readonly AudioClip _halted;        // horse/death
        readonly AudioClip[] _ioBlocked;   // damage/hit1-3

        // ── Time ────────────────────────────────────────────────
        readonly AudioClip _warpStart;     // wind
        readonly AudioClip _warpArrived;   // gong
        readonly AudioClip _warpCancelled; // cannon
        readonly AudioClip _warpComplete;  // bells

        // ── Persistence ─────────────────────────────────────────
        readonly AudioClip _saveStarted;   // chains
        readonly AudioClip _saveCompleted; // bells
        readonly AudioClip _syncCompleted; // gong

        public PopVujAudioProvider()
        {
            var go = new GameObject("PopVujAudio");
            Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;

            // Editor
            _tap       = Load("random/anvil_use");
            _insert    = Load("random/door_open");
            _delete    = Load("random/door_close");
            _undo      = Load("random/anvil_land");
            _navigate  = Load("random/bow");
            _compileOk  = LoadBank("mob/villager/yes", 1, 3);
            _compileErr = LoadBank("mob/villager/no", 1, 3);

            // Engine
            _output    = LoadBank("mob/villager/idle", 1, 6);
            _halted    = Load("mob/horse/death");
            _ioBlocked = LoadBank("damage/hit", 1, 3);

            // Time
            _warpStart     = Load("extra/wind");
            _warpArrived   = Load("extra/gong");
            _warpCancelled = Load("extra/cannon");
            _warpComplete  = Load("extra/bells");

            // Persistence
            _saveStarted   = Load("extra/chains");
            _saveCompleted = Load("extra/bells");
            _syncCompleted = Load("extra/gong");
        }

        // ═══════════════════════════════════════════════════════════
        //  IAudioProvider — Editor
        // ═══════════════════════════════════════════════════════════

        public void PlayTap()            => Play(_tap, 0.3f);
        public void PlayInsert()         => Play(_insert, 0.25f);
        public void PlayDelete()         => Play(_delete, 0.25f);
        public void PlayUndo()           => Play(_undo, 0.2f);
        public void PlayRedo()           => Play(_tap, 0.2f);
        public void PlayCompileSuccess() => PlayRandom(_compileOk, 0.4f);
        public void PlayCompileError()   => PlayRandom(_compileErr, 0.4f);
        public void PlayNavigate()       => Play(_navigate, 0.15f);

        // ═══════════════════════════════════════════════════════════
        //  IAudioProvider — Engine
        // ═══════════════════════════════════════════════════════════

        public void PlayInstructionStep() { } // too frequent — silent
        public void PlayOutput()           => PlayRandom(_output, 0.2f);
        public void PlayHalted()           => Play(_halted, 0.35f);
        public void PlayIOBlocked()        => PlayRandom(_ioBlocked, 0.3f);
        public void PlayWaitStateChanged() { } // silent

        // ═══════════════════════════════════════════════════════════
        //  IAudioProvider — Time
        // ═══════════════════════════════════════════════════════════

        public void PlayWarpStart()      => Play(_warpStart, 0.3f);
        public void PlayWarpCruise()     { } // silent during cruise
        public void PlayWarpDecelerate() { } // silent
        public void PlayWarpArrived()    => Play(_warpArrived, 0.4f);
        public void PlayWarpCancelled()  => Play(_warpCancelled, 0.35f);
        public void PlayWarpComplete()   => Play(_warpComplete, 0.35f);

        // ═══════════════════════════════════════════════════════════
        //  IAudioProvider — Persistence
        // ═══════════════════════════════════════════════════════════

        public void PlaySaveStarted()    => Play(_saveStarted, 0.2f);
        public void PlaySaveCompleted()  => Play(_saveCompleted, 0.3f);
        public void PlaySyncCompleted()  => Play(_syncCompleted, 0.3f);

        // ═══════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════

        void Play(AudioClip clip, float volume)
        {
            if (_src != null && clip != null)
                _src.PlayOneShot(clip, volume * SettingsBridge.SfxVolume);
        }

        void PlayRandom(AudioClip[] bank, float volume)
        {
            if (bank == null || bank.Length == 0) return;
            Play(bank[Random.Range(0, bank.Length)], volume);
        }

        static AudioClip Load(string path)
        {
            var clip = Resources.Load<AudioClip>($"SFX/{path}");
            if (clip == null)
                Debug.LogWarning($"[PopVujAudio] Missing clip: SFX/{path}");
            return clip;
        }

        static AudioClip[] LoadBank(string prefix, int from, int to)
        {
            var clips = new AudioClip[to - from + 1];
            for (int i = from; i <= to; i++)
                clips[i - from] = Load($"{prefix}{i}");
            return clips;
        }
    }
}
