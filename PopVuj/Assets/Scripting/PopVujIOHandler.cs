// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using CodeGamified.Engine;
using CodeGamified.Time;
using PopVuj.Game;

namespace PopVuj.Scripting
{
    /// <summary>
    /// Game I/O handler — bridges CUSTOM opcodes to city/match state.
    /// One switch case per opcode.
    /// </summary>
    public class PopVujIOHandler : IGameIOHandler
    {
        private readonly PopVujMatchManager _match;
        private readonly CityGrid _city;

        public PopVujIOHandler(PopVujMatchManager match, CityGrid city)
        {
            _match = match;
            _city = city;
        }

        public bool PreExecute(Instruction inst, MachineState state) => true;

        public void ExecuteIO(Instruction inst, MachineState state)
        {
            int op = (int)inst.Op - (int)OpCode.CUSTOM_0;

            switch ((PopVujOpCode)op)
            {
                // ── Queries → R0 ────────────────────────────────

                case PopVujOpCode.GET_POPULATION:
                    state.SetRegister(0, _match.Population);
                    break;
                case PopVujOpCode.GET_FAITH:
                    state.SetRegister(0, _match.Faith);
                    break;
                case PopVujOpCode.GET_DISEASE:
                    state.SetRegister(0, _match.Disease);
                    break;
                case PopVujOpCode.GET_CRIME:
                    state.SetRegister(0, _match.Crime);
                    break;
                case PopVujOpCode.GET_SEWER_POP:
                    state.SetRegister(0, _match.SewerDenCount);
                    break;
                case PopVujOpCode.GET_WEATHER:
                    state.SetRegister(0, (int)_match.CurrentWeather);
                    break;
                case PopVujOpCode.GET_CITY_W:
                    state.SetRegister(0, _city.Width);
                    break;
                case PopVujOpCode.GET_SCORE:
                    state.SetRegister(0, _match.Score);
                    break;
                case PopVujOpCode.GET_GAME_OVER:
                    state.SetRegister(0, _match.GameOver ? 1f : 0f);
                    break;
                case PopVujOpCode.GET_INPUT:
                    state.SetRegister(0, PopVujInputProvider.Instance != null
                        ? PopVujInputProvider.Instance.CurrentInput : 0f);
                    break;
                case PopVujOpCode.GET_HERETICS:
                    state.SetRegister(0, _match.Heretics);
                    break;
                case PopVujOpCode.GET_BIRTHS:
                    state.SetRegister(0, _match.Births);
                    break;
                case PopVujOpCode.GET_DEATHS:
                    state.SetRegister(0, _match.Deaths);
                    break;
                case PopVujOpCode.GET_HOUSES:
                    state.SetRegister(0, _city.CountSurface(CellType.House));
                    break;
                case PopVujOpCode.GET_CHAPELS:
                    state.SetRegister(0, _city.CountSurface(CellType.Chapel));
                    break;
                case PopVujOpCode.GET_FARMS:
                    state.SetRegister(0, _city.CountSurface(CellType.Farm));
                    break;
                case PopVujOpCode.GET_WORKSHOPS:
                    state.SetRegister(0, _city.CountSurface(CellType.Workshop));
                    break;

                // ── One-arg query: R0=slot → R0 ─────────────────

                case PopVujOpCode.GET_CELL:
                    state.SetRegister(0, (int)_city.GetSurface((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.GET_SEWER_CELL:
                    state.SetRegister(0, (int)_city.GetSewerAt((int)state.GetRegister(0)));
                    break;

                // ── Commands → R0 (1=success, 0=fail) ───────────

                case PopVujOpCode.SEND_PROPHET:
                    state.SetRegister(0, _match.SendProphet());
                    break;
                case PopVujOpCode.SMITE:
                    state.SetRegister(0, _match.Smite());
                    break;
                case PopVujOpCode.SET_WEATHER:
                    state.SetRegister(0, _match.SetWeather((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.SUMMON_BEARS:
                    state.SetRegister(0, _match.SummonBears());
                    break;
                case PopVujOpCode.SEND_OMEN:
                    state.SetRegister(0, _match.SendOmen());
                    break;
                case PopVujOpCode.BUILD_CHAPEL:
                    state.SetRegister(0, _match.BuildChapel((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.BUILD_HOUSE:
                    state.SetRegister(0, _match.BuildHouse((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.HARVEST_TREE:
                    state.SetRegister(0, _match.HarvestTree((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.EXPAND_BUILDING:
                    state.SetRegister(0, _match.ExpandBuilding((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.SHRINK_BUILDING:
                    state.SetRegister(0, _match.ShrinkBuilding((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.GET_WOOD:
                    state.SetRegister(0, _match.Wood);
                    break;
                case PopVujOpCode.GET_TREES:
                    state.SetRegister(0, _city.CountSurface(CellType.Tree));
                    break;

                // ── Harbor queries ───────────────────────────────

                case PopVujOpCode.GET_SHIPS:
                    state.SetRegister(0, _match.ShipCount);
                    break;
                case PopVujOpCode.GET_SHIPS_DOCKED:
                    state.SetRegister(0, _match.DockedShips);
                    break;
                case PopVujOpCode.GET_SHIPS_AT_SEA:
                    state.SetRegister(0, _match.ShipsAtSea);
                    break;
                case PopVujOpCode.GET_HARBOR_WORKERS:
                    state.SetRegister(0, _match.HarborWorkers);
                    break;
                case PopVujOpCode.GET_TRADE_INCOME:
                    state.SetRegister(0, _match.TradeIncome);
                    break;

                // ── Harbor commands ──────────────────────────────

                case PopVujOpCode.BUILD_SHIPYARD:
                    state.SetRegister(0, _match.BuildShipyard((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.BUILD_PIER:
                    state.SetRegister(0, _match.BuildPier((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.BUILD_CRANE:
                    state.SetRegister(0, _match.BuildCrane((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.BUILD_SHIP:
                    state.SetRegister(0, _match.BuildShipCmd((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.LAUNCH_SHIP:
                    state.SetRegister(0, _match.LaunchShip());
                    break;
                case PopVujOpCode.SEND_TRADE:
                    state.SetRegister(0, _match.SendTrade((int)state.GetRegister(0)));
                    break;
                case PopVujOpCode.REPAIR_SHIP:
                    state.SetRegister(0, _match.RepairShip());
                    break;
                case PopVujOpCode.BLESS_SHIP:
                    state.SetRegister(0, _match.BlessShip());
                    break;
            }
        }

        public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
        public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;
    }
}
