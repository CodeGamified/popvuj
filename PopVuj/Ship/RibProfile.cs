// Copyright SeaRäuber 2025-2026
// MIT License

namespace SeaRauber.Ship
{
    /// <summary>
    /// Cross-section curve type for a hull rib.
    /// Controls the shape of the hull at that rib station.
    ///
    ///   SharpV:     \_/ — deep keel, narrow, cuts through waves (sloop)
    ///   Round:      \_/ — elliptical, moderate displacement (schooner, frigate)
    ///   Flat:       |_| — wide, shallow draft, stable platform (barge, galleon)
    ///   Tumblehome: /‾\ — sides curve inward above waterline (warship)
    /// </summary>
    public enum RibCurveType
    {
        SharpV,
        Round,
        Flat,
        Tumblehome,
    }

    /// <summary>
    /// Function assigned to a single floor within a rib section.
    /// Determines what crew stations and activity exist on that floor.
    /// Maps to StationType for the crew pathfinding grid.
    /// </summary>
    public enum FloorFunction
    {
        None,
        OpenDeck,       // Weather deck — walkway, rail positions
        Cannons,        // Gun deck — gunner stations along hull sides
        Hammocks,       // Crew berthing — rest/sleep stations
        Kitchen,        // Galley — cook station, food prep
        CargoBay,       // Hold — cargo storage, crane target
        Magazine,       // Powder room — ammo supply for guns
        ChartRoom,      // Navigation — navigator station, maps
        CaptainCabin,   // Captain's quarters — command station
        Brig,           // Prisoner hold
        Workshop,       // Carpenter's bench — repair station
        SailLocker,     // Spare canvas and cordage
    }

    /// <summary>
    /// A single floor within a rib section.
    /// </summary>
    public struct FloorDef
    {
        public FloorFunction Function;
        public FloorDef(FloorFunction fn) { Function = fn; }
    }

    /// <summary>
    /// A hull rib — one cross-section station along the keel.
    /// N ribs define the hull shape. Mesh is lofted between adjacent ribs.
    ///
    /// Rib layout (looking from bow toward stern):
    ///
    ///     ┌─────────┐  ← Width (beam at this rib)
    ///     │  Floor 2 │  ← OpenDeck
    ///     │  Floor 1 │  ← Cannons
    ///     │  Floor 0 │  ← CargoBay
    ///     └────V─────┘  ← CurveType (SharpV here)
    ///          ↕
    ///        Height
    ///
    /// Height determines floor count:
    ///   1 floor  ≈ 0.8–1.2m (small boat)
    ///   2 floors ≈ 1.5–2.0m (sloop, schooner)
    ///   3 floors ≈ 2.5–3.0m (brigantine, frigate)
    ///   4 floors ≈ 3.0–4.0m (galleon, flagship)
    /// </summary>
    public struct RibDef
    {
        /// <summary>Position along keel in local Z. 0=midship, +Z=bow, -Z=stern.</summary>
        public float ZOffset;

        /// <summary>Full beam width at this rib (port to starboard).</summary>
        public float Width;

        /// <summary>Height from keel bottom to deck edge.</summary>
        public float Height;

        /// <summary>Cross-section curve shape.</summary>
        public RibCurveType CurveType;

        /// <summary>Floors bottom-to-top. floors[0]=lowest (bilge), floors[n-1]=weather deck.</summary>
        public FloorDef[] Floors;

        public int FloorCount => Floors?.Length ?? 0;

        public RibDef(float z, float width, float height, RibCurveType curve, params FloorDef[] floors)
        {
            ZOffset = z;
            Width = width;
            Height = height;
            CurveType = curve;
            Floors = floors;
        }
    }
}
