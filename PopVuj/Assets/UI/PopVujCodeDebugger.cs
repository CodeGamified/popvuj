// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using CodeGamified.TUI;
using PopVuj.Scripting;

namespace PopVuj.UI
{
    /// <summary>
    /// Thin adapter — wires a PopVujProgram into the engine's CodeDebuggerWindow
    /// via PopVujDebuggerData (IDebuggerDataSource). All rendering lives in the engine.
    /// </summary>
    public class PopVujCodeDebugger : CodeDebuggerWindow
    {
        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CODE";
        }

        public void Bind(PopVujProgram program)
        {
            SetDataSource(new PopVujDebuggerData(program));
        }
    }
}
