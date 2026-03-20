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
        Fish,       // silver-blue sack from fishing boats
        Rope,       // coiled brown bundle from workshop
        Plank,      // flat lumber stack from workshop
        TradeCrate, // stamped crate packed for export
        ExoticGoods,// ornate chest from distant trade
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
                    FootprintBonus  = 0.36f;   // log sticks out in front & behind
                    VisualWidth     = 0.44f;   // long horizontal
                    VisualHeight    = 0.08f;   // thin
                    VisualDepth     = 0.08f;
                    CarryOffsetY    = 0.12f;   // on shoulder
                    break;

                case CargoKind.Crate:
                    SpeedMultiplier = 0.65f;
                    FootprintBonus  = 0.16f;
                    VisualWidth     = 0.14f;
                    VisualHeight    = 0.14f;
                    VisualDepth     = 0.14f;
                    CarryOffsetY    = 0.16f;   // held in front
                    break;

                case CargoKind.Grain:
                    SpeedMultiplier = 0.70f;
                    FootprintBonus  = 0.10f;
                    VisualWidth     = 0.12f;
                    VisualHeight    = 0.16f;
                    VisualDepth     = 0.10f;
                    CarryOffsetY    = 0.14f;   // sack over shoulder
                    break;

                case CargoKind.Stone:
                    SpeedMultiplier = 0.40f;   // very heavy
                    FootprintBonus  = 0.20f;
                    VisualWidth     = 0.16f;
                    VisualHeight    = 0.12f;
                    VisualDepth     = 0.16f;
                    CarryOffsetY    = 0.08f;   // low carry
                    break;

                case CargoKind.Water:
                    SpeedMultiplier = 0.75f;
                    FootprintBonus  = 0.08f;
                    VisualWidth     = 0.10f;
                    VisualHeight    = 0.12f;
                    VisualDepth     = 0.10f;
                    CarryOffsetY    = 0.10f;
                    break;

                case CargoKind.Fish:
                    SpeedMultiplier = 0.70f;
                    FootprintBonus  = 0.10f;
                    VisualWidth     = 0.12f;
                    VisualHeight    = 0.14f;
                    VisualDepth     = 0.10f;
                    CarryOffsetY    = 0.12f;
                    break;

                case CargoKind.Rope:
                    SpeedMultiplier = 0.72f;
                    FootprintBonus  = 0.12f;
                    VisualWidth     = 0.12f;
                    VisualHeight    = 0.12f;
                    VisualDepth     = 0.12f;
                    CarryOffsetY    = 0.12f;
                    break;

                case CargoKind.Plank:
                    SpeedMultiplier = 0.55f;
                    FootprintBonus  = 0.28f;
                    VisualWidth     = 0.36f;
                    VisualHeight    = 0.06f;
                    VisualDepth     = 0.10f;
                    CarryOffsetY    = 0.12f;   // on shoulder like a log
                    break;

                case CargoKind.TradeCrate:
                    SpeedMultiplier = 0.60f;
                    FootprintBonus  = 0.16f;
                    VisualWidth     = 0.16f;
                    VisualHeight    = 0.16f;
                    VisualDepth     = 0.16f;
                    CarryOffsetY    = 0.16f;
                    break;

                case CargoKind.ExoticGoods:
                    SpeedMultiplier = 0.65f;
                    FootprintBonus  = 0.14f;
                    VisualWidth     = 0.14f;
                    VisualHeight    = 0.14f;
                    VisualDepth     = 0.12f;
                    CarryOffsetY    = 0.14f;
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
                case CargoKind.Fish:  return new UnityEngine.Color(0.55f, 0.65f, 0.80f); // silver-blue
                case CargoKind.Rope:  return new UnityEngine.Color(0.50f, 0.38f, 0.18f); // hemp brown
                case CargoKind.Plank: return new UnityEngine.Color(0.60f, 0.45f, 0.22f); // fresh lumber
                case CargoKind.TradeCrate:  return new UnityEngine.Color(0.50f, 0.35f, 0.15f); // stamped wood
                case CargoKind.ExoticGoods: return new UnityEngine.Color(0.75f, 0.55f, 0.20f); // golden ornate
                default:              return UnityEngine.Color.clear;
            }
        }
    }
}
