// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;

namespace PopVuj.Core
{
    /// <summary>
    /// God-sim simulation time — slower base pace than arcade games.
    /// Max 100x for fast-forwarding through generations.
    /// Time formatted as Day:Hour (one day = 60 sim-seconds).
    /// </summary>
    public class PopVujSimulationTime : CodeGamified.Time.SimulationTime
    {
        protected override float MaxTimeScale => 100f;

        public const double SECONDS_PER_DAY = 60.0;

        protected override void OnInitialize()
        {
            timeScalePresets = new float[]
                { 0f, 0.25f, 0.5f, 1f, 2f, 5f, 10f, 50f, 100f };
            currentPresetIndex = 3; // Start at 1x
        }

        public override string GetFormattedTime()
        {
            int day = (int)(simulationTime / SECONDS_PER_DAY) + 1;
            int hour = (int)((simulationTime % SECONDS_PER_DAY) / (SECONDS_PER_DAY / 24.0));
            return $"Day {day}, {hour:D2}h";
        }
    }
}
