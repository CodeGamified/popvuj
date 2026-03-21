// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;

namespace PopVuj.Core
{
    /// <summary>
    /// God-sim simulation time — slower base pace than arcade games.
    /// Max 100x for fast-forwarding through generations.
    /// Time formatted as Day:Hour (one day = 60 sim-seconds).
    ///
    /// Day-night cycle: 1 day = 60 sim-seconds.
    ///   Sunrise at 6h, sunset at 18h.
    /// Moon cycle: 8 phases over 8 days (1 frame per day at midnight).
    /// </summary>
    public class PopVujSimulationTime : CodeGamified.Time.SimulationTime
    {
        protected override float MaxTimeScale => 100f;

        public const double SECONDS_PER_DAY = 60.0;
        public const int MOON_PHASE_COUNT = 8;

        protected override void OnInitialize()
        {
            timeScalePresets = new float[]
                { 0f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 50f, 100f };
            currentPresetIndex = 3; // Start at 1x
            // Start at 10h (mid-morning) so the player sees daytime first
            simulationTime = (10.0 / 24.0) * SECONDS_PER_DAY;
        }

        /// <summary>Current time of day in hours (0–24).</summary>
        public override float GetTimeOfDay()
        {
            float progress = (float)(simulationTime % SECONDS_PER_DAY) / (float)SECONDS_PER_DAY;
            return progress * 24f;
        }

        /// <summary>Is it daytime? (6h–18h)</summary>
        public override bool IsDaytime()
        {
            float hour = GetTimeOfDay();
            return hour >= 6f && hour < 18f;
        }

        /// <summary>
        /// Sun direction on a semicircular arc in the XY plane.
        /// Rises in the east (left, -X) at 6h, zenith at 12h, sets west (+X) at 18h.
        /// Below horizon outside 6–18h.
        /// </summary>
        public override Vector3 GetSunDirection()
        {
            float hour = GetTimeOfDay();
            float dayProgress = Mathf.Clamp01((hour - 6f) / 12f); // 0 at 6h, 1 at 18h
            float angle = dayProgress * Mathf.PI; // 0 → π
            // sin = altitude (0 at horizon, 1 at zenith), cos = horizontal
            return new Vector3(-Mathf.Cos(angle), Mathf.Sin(angle), 0f).normalized;
        }

        /// <summary>Sun altitude: 1 = overhead, 0 = horizon, negative = below.</summary>
        public override float GetSunAltitude()
        {
            float hour = GetTimeOfDay();
            if (hour < 6f || hour >= 18f) return -1f;
            float dayProgress = (hour - 6f) / 12f;
            return Mathf.Sin(dayProgress * Mathf.PI);
        }

        /// <summary>Current day number (0-based).</summary>
        public int GetDay()
        {
            return (int)(simulationTime / SECONDS_PER_DAY);
        }

        /// <summary>Moon phase index (0–7). Changes once per day at midnight.</summary>
        public int GetMoonPhase()
        {
            return GetDay() % MOON_PHASE_COUNT;
        }

        public override string GetFormattedTime()
        {
            int day = GetDay() + 1;
            int hour = (int)GetTimeOfDay();
            return $"Day {day}, {hour:D2}h";
        }
    }
}
