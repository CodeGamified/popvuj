// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;

namespace PopVuj.Crew
{
    /// <summary>
    /// Minion behavioral state.
    /// </summary>
    public enum MinionState
    {
        Idle,       // standing on road, waiting to pick next task
        Walking,    // moving left/right along the road to a target
        InSlot,     // occupying a building slot (working / worshipping / resting)
    }

    /// <summary>
    /// What a minion currently needs most.
    /// </summary>
    public enum MinionNeed
    {
        Rest,       // → House
        Hunger,     // → Farm / Market
        Faith,      // → Chapel
        Work,       // → Workshop / Farm
        Wander,     // → walk around aimlessly
    }

    /// <summary>
    /// Individual minion — a population unit with position, needs, and task state.
    /// Pure C# data object managed by MinionManager. Not a MonoBehaviour.
    /// </summary>
    public class Minion
    {
        // ── Identity ────────────────────────────────────────────
        public int Id;

        // ── Position (world-space X on the road) ────────────────
        public float X;

        // ── State machine ───────────────────────────────────────
        public MinionState State;
        public MinionNeed CurrentNeed;

        // ── Needs (0–1, higher = more urgent) ───────────────────
        public float Hunger;
        public float Fatigue;
        public float Faithlessness;

        // ── Task assignment ─────────────────────────────────────
        public int TargetBuilding;     // grid origin slot (NO_TARGET / WANDERING)
        public int SlotIndex;          // slot within the building (-1 = none)
        public float TaskTimer;        // sim-seconds remaining

        // ── Movement ────────────────────────────────────────────
        public float BaseWalkSpeed;    // innate speed (without cargo)
        public int FacingDirection;    // -1 = left, 1 = right

        // ── Lane / traffic ──────────────────────────────────────
        // Lane is a Z offset from the center road line. Minions shift
        // lanes to avoid walking through each other. Positive = closer
        // to camera, negative = further away.
        public float Lane;             // current Z offset (world units)
        public float LaneTarget;       // desired Z offset we're drifting toward

        // ── Cargo ───────────────────────────────────────────────
        // Transient hauling state. Cargo is picked up at a source,
        // carried to a destination, then dropped off.
        // Affects speed, footprint, and visual appearance.
        public Cargo Cargo;

        // ── Cart (permanent trait) ──────────────────────────────
        // Some minions own a cart. When a cart-owner carries cargo
        // their footprint is wider but the speed penalty is reduced
        // because the cart bears the weight.
        public bool HasCart;

        // ── Sentinel values ─────────────────────────────────────
        public const int NO_TARGET = -1;
        public const int WANDERING = -2;

        // ── Lane constants (shared) ─────────────────────────────
        public const float LANE_WIDTH = 0.06f;    // Z spacing between avoidance sub-lanes
        public const int   MAX_LANES  = 3;        // ±3 sub-lanes from directional home lane
        public const float PERSON_FOOTPRINT = 0.10f;
        public const float CART_FOOTPRINT   = 0.25f;

        // ── Directional lane offsets ────────────────────────────
        // Road Z depth runs from 0 (road surface) to ~0.5 (building face).
        // MinionZ = 0.25 is the center. These offsets are relative to MinionZ.
        // Right-going minions walk closer to camera (negative Z offset from center),
        // left-going minions walk closer to buildings (positive Z offset from center).
        public const float RIGHT_LANE_OFFSET = -0.08f;  // ~33% toward camera
        public const float LEFT_LANE_OFFSET  =  0.08f;  // ~66% toward buildings

        // ── Derived properties ──────────────────────────────────

        /// <summary>Effective walk speed accounting for cargo weight and cart.</summary>
        public float WalkSpeed
        {
            get
            {
                if (Cargo.IsEmpty) return BaseWalkSpeed;
                float mult = Cargo.SpeedMultiplier;
                // Cart reduces the speed penalty by half (cart bears the weight)
                if (HasCart) mult = 1f - (1f - mult) * 0.5f;
                return BaseWalkSpeed * mult;
            }
        }

        /// <summary>Effective road footprint accounting for cargo volume and cart.</summary>
        public float Footprint
        {
            get
            {
                float baseFP = HasCart ? CART_FOOTPRINT : PERSON_FOOTPRINT;
                return baseFP + Cargo.FootprintBonus;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════

        public Minion(int id, float x)
        {
            Id = id;
            X = x;
            State = MinionState.Idle;
            CurrentNeed = MinionNeed.Wander;
            TargetBuilding = NO_TARGET;
            SlotIndex = -1;
            TaskTimer = Random.Range(0.1f, 1f); // stagger initial decisions
            FacingDirection = Random.value > 0.5f ? 1 : -1;

            Hunger = Random.Range(0.1f, 0.5f);
            Fatigue = Random.Range(0.1f, 0.5f);
            Faithlessness = Random.Range(0.1f, 0.5f);

            // ~15% of minions own a cart
            HasCart = Random.value < 0.15f;
            BaseWalkSpeed = HasCart
                ? 0.22f + Random.Range(0f, 0.08f)
                : 0.30f + Random.Range(0f, 0.15f);

            Cargo = Cargo.Empty;

            // Start in the directional home lane with a small random jitter
            Lane = GetDirectionalLane(FacingDirection);
            LaneTarget = Lane;
        }

        // ═══════════════════════════════════════════════════════════════
        // CARGO HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Pick up cargo of the given kind.</summary>
        public void PickupCargo(CargoKind kind)
        {
            Cargo = Cargo.Pickup(kind);
        }

        /// <summary>Drop whatever cargo is being carried. Returns the kind that was dropped.</summary>
        public CargoKind DropCargo()
        {
            var kind = Cargo.Kind;
            Cargo = Cargo.Drop();
            return kind;
        }

        /// <summary>
        /// Home lane Z offset for a given facing direction, with small per-minion jitter
        /// so minions in the same lane don't perfectly overlap.
        /// </summary>
        public static float GetDirectionalLane(int facingDirection)
        {
            float baseLane = facingDirection > 0 ? RIGHT_LANE_OFFSET : LEFT_LANE_OFFSET;
            float jitter = (Random.value - 0.5f) * LANE_WIDTH;
            return baseLane + jitter;
        }
    }
}
