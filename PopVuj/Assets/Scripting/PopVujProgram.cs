// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;
using CodeGamified.Engine.Runtime;
using CodeGamified.Time;
using PopVuj.Game;

namespace PopVuj.Scripting
{
    /// <summary>
    /// ProgramBehaviour subclass — tick-based divine decree execution.
    ///
    /// EXECUTION MODEL (tick-based, deterministic):
    ///   - Each simulation tick (~20 ops/sec sim-time), the deity script runs
    ///   - Memory (variables) persists across ticks
    ///   - PC resets to 0 each tick (on HALT)
    ///   - Results are IDENTICAL at 0.5x, 1x, 100x speed
    /// </summary>
    public class PopVujProgram : ProgramBehaviour
    {
        private PopVujMatchManager _match;
        private CityGrid _city;
        private PopVujIOHandler _ioHandler;
        private PopVujCompilerExtension _compilerExt;

        public const float OPS_PER_SECOND = 20f;
        private float _opAccumulator;

        // ── Default starter code — the simplest benevolent god ──

        private const string DEFAULT_CODE = @"# POPVUJ — Write your divine decrees!
# Your script runs at 20 ops/sec (sim-time).
# When it finishes, it restarts from the top.
# Variables persist — track state across ticks.
#
# QUERIES — Read the world:
#   get_population()     → total living minions
#   get_faith()          → faith level (0.0-1.0)
#   get_disease()        → disease level (0.0-1.0)
#   get_crime()          → crime level (0.0-1.0)
#   get_sewer_pop()      → homeless/criminal count
#   get_weather()        → 0=clear,1=rain,2=storm,3=drought
#   get_heretics()       → minions rejecting your rule
#   get_houses()         → house count
#   get_chapels()        → chapel count
#   get_farms()          → farm count
#   get_wood()           → wood stockpile
#   get_trees()          → tree count on map
#   get_city_width()     → city strip width (slots)
#   get_cell(slot)       → surface cell type at slot
#   get_sewer_cell(slot) → sewer cell type at slot
#
# COMMANDS — Shape the world:
#   send_prophet()       → boost faith (+0.08)
#   smite()              → kill a heretic
#   summon_bears()       → unleash bears on heretics
#   send_omen()          → big faith surge (+0.15)
#   set_weather(w)       → 0=clear,1=rain,2=storm,3=drought
#   harvest_tree(slot)   → chop tree for +1 wood
#   build_chapel(slot)   → place chapel (costs 2 wood)
#   build_house(slot)    → place house (costs 1 wood)
#   expand_building(slot)→ grow building +1 tile (1 wood)
#   shrink_building(slot)→ shrink building -1 tile (+1 wood)
#
# This starter harvests trees and builds when able:
faith = get_faith()
heretics = get_heretics()
disease = get_disease()
wood = get_wood()
if faith < 0.3:
    send_prophet()
if heretics > 3:
    smite()
if disease > 0.5:
    set_weather(0)
# Harvest trees for wood when we find them
w = get_city_width()
slot = 0
if slot < w:
    cell = get_cell(slot)
    if cell == 2:
        harvest_tree(slot)
    slot = slot + 1
# Build a house if we have wood
if wood > 1:
    build_house(5)
";

        public string CurrentSourceCode => _sourceCode;
        public System.Action OnCodeChanged;

        public void Initialize(PopVujMatchManager match, CityGrid city,
                               string initialCode = null, string programName = "DeityAI")
        {
            _match = match;
            _city = city;
            _compilerExt = new PopVujCompilerExtension();

            _programName = programName;
            _sourceCode = initialCode ?? DEFAULT_CODE;
            _autoRun = true;

            LoadAndRun(_sourceCode);
        }

        protected override void Update()
        {
            if (_executor == null || _program == null || _isPaused) return;
            if (_match == null || !_match.MatchInProgress || _match.GameOver) return;

            float timeScale = SimulationTime.Instance?.timeScale ?? 1f;
            if (SimulationTime.Instance != null && SimulationTime.Instance.isPaused) return;

            float simDelta = UnityEngine.Time.deltaTime * timeScale;
            _opAccumulator += simDelta * OPS_PER_SECOND;

            int opsToRun = (int)_opAccumulator;
            _opAccumulator -= opsToRun;

            for (int i = 0; i < opsToRun; i++)
            {
                if (_executor.State.IsHalted)
                {
                    _executor.State.PC = 0;
                    _executor.State.IsHalted = false;
                }
                _executor.ExecuteOne();
            }

            if (opsToRun > 0)
                ProcessEvents();
        }

        protected override IGameIOHandler CreateIOHandler()
        {
            _ioHandler = new PopVujIOHandler(_match, _city);
            return _ioHandler;
        }

        protected override CompiledProgram CompileSource(string source, string name)
        {
            return PythonCompiler.Compile(source, name, _compilerExt);
        }

        protected override void ProcessEvents()
        {
            if (_executor?.State == null) return;
            while (_executor.State.OutputEvents.Count > 0)
                _executor.State.OutputEvents.Dequeue();
        }

        public void UploadCode(string newSource)
        {
            _sourceCode = newSource ?? DEFAULT_CODE;
            LoadAndRun(_sourceCode);
            Debug.Log($"[DeityAI] Uploaded new code ({_program?.Instructions?.Length ?? 0} instructions)");
            OnCodeChanged?.Invoke();
        }

        public void ResetExecution()
        {
            if (_executor?.State == null) return;
            _executor.State.Reset();
            _opAccumulator = 0f;
        }
    }
}
