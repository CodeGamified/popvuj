// Copyright CodeGamified 2025-2026
// MIT License — PopVuj

namespace PopVuj.Crew
{
    /// <summary>
    /// What kind of material a minion is carrying.
    /// </summary>
    public enum CargoKind
    {
        None,       // empty-handed
        Log,        // harvested tree trunk — long, horizontal
        Crate,      // generic supply box (from workshop / market)
        Grain,      // sack of grain from a farm
        Stone,      // quarried block (future)
        Water,      // bucket / barrel (from fountain)
    }

    /// <summary>
    /// Physical cargo carried by a minion. Encapsulates the visual volume,
    /// movement penalty, and footprint expansion caused by hauling materials.
    ///
    /// Each CargoKind has fixed physical properties. A minion with cargo:
    ///   - walks slower (SpeedMultiplier)
    ///   - occupies more road space (FootprintBonus added to base)
    ///   - renders with an attached volume of a specific size and color
    ///
    /// Cargo is transient — picked up at a source, carried to a destination,
    /// then dropped off. The minion returns to normal afterward.
    ///
    /// Usage:
    ///   Cargo.Pickup(CargoKind.Log)  → returns a new Cargo
    ///   cargo.Drop()                 → returns Cargo.Empty
    ///   Cargo.Empty                  → singleton "nothing carried"
    /// </summary>
    public sealed class Cargo
    {
        // ── Static singleton for empty-handed ───────────────────
        public static readonly Cargo Empty = new Cargo(CargoKind.None);

        // ── What ────────────────────────────────────────────────
        public CargoKind Kind { get; private set; }

        // ── Physical properties (derived from Kind) ─────────────
        public float SpeedMultiplier { get; private set; }   // 1.0 = no penalty
        public float FootprintBonus  { get; private set; }   // added to minion base footprint
        public float VisualWidth     { get; private set; }   // render size X
        public float VisualHeight    { get; private set; }   // render size Y
        public float VisualDepth     { get; private set; }   // render size Z
        public float CarryOffsetY    { get; private set; }   // Y offset above minion center

        // ── Convenience ─────────────────────────────────────────
        public bool IsEmpty => Kind == CargoKind.None;

        // ═══════════════════════════════════════════════════════════════
        // FACTORY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Create a cargo of the given kind with all physical properties set.</summary>
        public static Cargo Pickup(CargoKind kind)
        {
            if (kind == CargoKind.None) return Empty;
            return new Cargo(kind);
        }

        /// <summary>Drop the cargo — returns the Empty singleton.</summary>
        public Cargo Drop() => Empty;

        // ═══════════════════════════════════════════════════════════════
        // CONSTRUCTION — private, use Pickup()
        // ═══════════════════════════════════════════════════════════════

        private Cargo(CargoKind kind)
        {
            Kind = kind;
            Apply(kind);
        }

        private void Apply(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Log:
                    SpeedMultiplier = 0.50f;   // logs are heavy and awkward
                    FootprintBonus  = 0.18f;   // log sticks out in front & behind
                    VisualWidth     = 0.22f;   // long horizontal
                    VisualHeight    = 0.04f;   // thin
                    VisualDepth     = 0.04f;
                    CarryOffsetY    = 0.06f;   // on shoulder
                    break;

                case CargoKind.Crate:
                    SpeedMultiplier = 0.65f;
                    FootprintBonus  = 0.08f;
                    VisualWidth     = 0.07f;
                    VisualHeight    = 0.07f;
                    VisualDepth     = 0.07f;
                    CarryOffsetY    = 0.08f;   // held in front
                    break;

                case CargoKind.Grain:
                    SpeedMultiplier = 0.70f;
                    FootprintBonus  = 0.05f;
                    VisualWidth     = 0.06f;
                    VisualHeight    = 0.08f;
                    VisualDepth     = 0.05f;
                    CarryOffsetY    = 0.07f;   // sack over shoulder
                    break;

                case CargoKind.Stone:
                    SpeedMultiplier = 0.40f;   // very heavy
                    FootprintBonus  = 0.10f;
                    VisualWidth     = 0.08f;
                    VisualHeight    = 0.06f;
                    VisualDepth     = 0.08f;
                    CarryOffsetY    = 0.04f;   // low carry
                    break;

                case CargoKind.Water:
                    SpeedMultiplier = 0.75f;
                    FootprintBonus  = 0.04f;
                    VisualWidth     = 0.05f;
                    VisualHeight    = 0.06f;
                    VisualDepth     = 0.05f;
                    CarryOffsetY    = 0.05f;
                    break;

                default: // None
                    SpeedMultiplier = 1.0f;
                    FootprintBonus  = 0f;
                    VisualWidth     = 0f;
                    VisualHeight    = 0f;
                    VisualDepth     = 0f;
                    CarryOffsetY    = 0f;
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // COLORS — used by MinionRenderer to tint cargo volume
        // ═══════════════════════════════════════════════════════════════

        public static UnityEngine.Color GetColor(CargoKind kind)
        {
            switch (kind)
            {
                case CargoKind.Log:   return new UnityEngine.Color(0.40f, 0.25f, 0.10f); // bark brown
                case CargoKind.Crate: return new UnityEngine.Color(0.55f, 0.42f, 0.22f); // pale wood
                case CargoKind.Grain: return new UnityEngine.Color(0.80f, 0.72f, 0.35f); // golden wheat
                case CargoKind.Stone: return new UnityEngine.Color(0.45f, 0.45f, 0.42f); // grey
                case CargoKind.Water: return new UnityEngine.Color(0.25f, 0.50f, 0.75f); // blue
                default:              return UnityEngine.Color.clear;
            }
        }
    }
}
