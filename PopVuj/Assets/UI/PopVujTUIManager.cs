// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CodeGamified.TUI;
using CodeGamified.Settings;
using CodeGamified.Audio;
using PopVuj.Game;
using PopVuj.AI;
using PopVuj.Scripting;

namespace PopVuj.UI
{
    /// <summary>
    /// TUI Manager for PopVuj — unified code debugger panels with intra-panel
    /// column draggers, plus a unified 7-column status panel at the bottom.
    ///
    /// Layout (left/right independent, middle 34% is game view):
    ///   ┌────────────────────────────┐                ┌────────────────────────────┐
    ///   │ GOD SCRIPT                 │   GAME VIEW    │ FATE SCRIPT                │
    ///   │ SOURCE ┆ MACHINE ┆ STATE   │   (34% open)   │ STATE ┆ MACHINE ┆ SOURCE   │
    ///   ├────────────────────────────┴────────────────┴────────────────────────────┤
    ///   │  GOD  ┆ SETTINGS ┆ WORLD ┆  POPVUJ  ┆ CONTROLS ┆ AUDIO ┆   FATE        │
    ///   └─────────────────────────────────────────────────────────────────────────┘
    ///   All column dividers (┆) are draggable.
    /// </summary>
    public class PopVujTUIManager : MonoBehaviour, ISettingsListener
    {
        // Dependencies
        private PopVujMatchManager _match;
        private PopVujProgram _playerProgram;
        private PopVujFateController _fate;
        private Equalizer _equalizer;

        // Canvas
        private Canvas _canvas;
        private RectTransform _canvasRect;

        // Debugger panels (unified, one per side)
        private PopVujCodeDebugger _godDebugger;
        private PopVujCodeDebugger _fateDebugger;
        private RectTransform _godDebuggerRect;
        private RectTransform _fateDebuggerRect;

        // Status panel (unified, bottom)
        private PopVujStatusPanel _statusPanel;
        private RectTransform _statusPanelRect;

        // Edge draggers for cross-type linking
        private TUIEdgeDragger _godRightEdge;
        private TUIEdgeDragger _fateLeftEdge;

        // Font
        private TMP_FontAsset _font;
        private float _fontSize;

        // All panel rects for bulk cleanup
        private RectTransform[] _allPanelRects;

        public void Initialize(PopVujMatchManager match, PopVujProgram program,
                               PopVujFateController fate,
                               Equalizer equalizer = null)
        {
            _match = match;
            _playerProgram = program;
            _fate = fate;
            _equalizer = equalizer;
            _fontSize = SettingsBridge.FontSize;

            BuildCanvas();
            BuildPanels();
        }

        private void OnEnable()  => SettingsBridge.Register(this);
        private void OnDisable() => SettingsBridge.Unregister(this);

        public void OnSettingsChanged(SettingsSnapshot settings, SettingsCategory changed)
        {
            if (changed != SettingsCategory.Display) return;
            if (Mathf.Approximately(settings.FontSize, _fontSize)) return;

            _fontSize = settings.FontSize;
            RebuildPanels();
        }

        private void RebuildPanels()
        {
            if (_allPanelRects != null)
                foreach (var rt in _allPanelRects)
                    if (rt != null) Destroy(rt.gameObject);

            _godDebugger = null; _fateDebugger = null;
            _statusPanel = null;

            BuildPanels();
        }

        // ═══════════════════════════════════════════════════════════════
        // CANVAS
        // ═══════════════════════════════════════════════════════════════

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("PopVujTUI_Canvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = Camera.main;
            _canvas.sortingOrder = 100;
            _canvas.planeDistance = 1f;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            _canvasRect = canvasGO.GetComponent<RectTransform>();

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PANELS
        // ═══════════════════════════════════════════════════════════════

        private void BuildPanels()
        {
            const float statusH = 0.25f;
            const float pLeft  = 0f;
            const float pRight = 0.33f;
            const float aLeft  = 0.67f;
            const float aRight = 1.0f;

            // ── God debugger (left panel) ──
            _godDebuggerRect = CreatePanel("God_Debugger",
                new Vector2(pLeft, statusH), new Vector2(pRight, 1f));
            _godDebugger = _godDebuggerRect.gameObject.AddComponent<PopVujCodeDebugger>();
            AddPanelBackground(_godDebuggerRect);
            _godDebugger.InitializeProgrammatic(GetFont(), _fontSize,
                _godDebuggerRect.GetComponent<Image>());
            _godDebugger.SetTitle("GOD SCRIPT");
            _godDebugger.Bind(_playerProgram);

            // ── Fate debugger (right panel) ──
            _fateDebuggerRect = CreatePanel("Fate_Debugger",
                new Vector2(aLeft, statusH), new Vector2(aRight, 1f));
            _fateDebugger = _fateDebuggerRect.gameObject.AddComponent<PopVujCodeDebugger>();
            AddPanelBackground(_fateDebuggerRect);
            _fateDebugger.InitializeProgrammatic(GetFont(), _fontSize,
                _fateDebuggerRect.GetComponent<Image>());
            _fateDebugger.SetTitle("FATE SCRIPT");
            _fateDebugger.SetMirrorPanels(true);
            if (_fate != null)
                _fateDebugger.Bind(_fate.Program);

            // ── Status Panel (unified 7-column) ──
            _statusPanelRect = CreatePanel("StatusPanel",
                new Vector2(0f, 0f), new Vector2(1f, statusH));
            _statusPanel = _statusPanelRect.gameObject.AddComponent<PopVujStatusPanel>();
            AddPanelBackground(_statusPanelRect);
            _statusPanel.InitializeProgrammatic(GetFont(), _fontSize - 1f,
                _statusPanelRect.GetComponent<Image>());
            _statusPanel.Bind(_match, _playerProgram, _fate);
            if (_equalizer != null)
                _statusPanel.BindEqualizer(_equalizer);

            // Track all for teardown
            _allPanelRects = new[]
            {
                _godDebuggerRect, _fateDebuggerRect,
                _statusPanelRect
            };

            LinkEdges();
            StartCoroutine(LinkColumnDraggers());
        }

        // ═══════════════════════════════════════════════════════════════
        // EDGE LINKING
        // ═══════════════════════════════════════════════════════════════

        private IEnumerator LinkColumnDraggers()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            LinkDraggerPair(_statusPanel, 0, _godDebugger, 0);
            LinkDraggerPair(_statusPanel, 1, _godDebugger, 1);
            LinkDraggerPair(_statusPanel, 4, _fateDebugger, 0);
            LinkDraggerPair(_statusPanel, 5, _fateDebugger, 1);

            LinkColumnToEdge(_statusPanel, 2, _godRightEdge);
            LinkColumnToEdge(_statusPanel, 3, _fateLeftEdge);
        }

        private static void LinkDraggerPair(TerminalWindow a, int aIdx, TerminalWindow b, int bIdx)
        {
            var da = a?.GetColumnDragger(aIdx);
            var db = b?.GetColumnDragger(bIdx);
            if (da != null && db != null) da.LinkDragger(db);
        }

        private void LinkColumnToEdge(TerminalWindow panel, int colIdx, TUIEdgeDragger edgeDragger)
        {
            var colDragger = panel?.GetColumnDragger(colIdx);
            if (colDragger == null || edgeDragger == null) return;

            var statusRect = panel.GetComponent<RectTransform>();
            if (statusRect == null) return;

            bool syncing = false;

            edgeDragger.OnDragged = anchorValue =>
            {
                if (syncing) return;
                syncing = true;

                float canvasW = _canvasRect.rect.width;
                float statusLeft = statusRect.anchorMin.x * canvasW;
                float statusWidth = (statusRect.anchorMax.x - statusRect.anchorMin.x) * canvasW;
                if (statusWidth <= 0) { syncing = false; return; }

                float edgeX = anchorValue * canvasW;
                float localX = edgeX - statusLeft;
                float cw = colDragger.CharWidth;
                if (cw <= 0) { syncing = false; return; }

                int charPos = Mathf.RoundToInt(localX / cw);
                colDragger.SetPositionWithNotify(charPos);

                syncing = false;
            };

            colDragger.ExternalCallback = charPos =>
            {
                if (syncing) return;
                syncing = true;

                float cw = colDragger.CharWidth;
                float canvasW = _canvasRect.rect.width;
                float statusLeft = statusRect.anchorMin.x * canvasW;
                float edgeX = statusLeft + charPos * cw;
                float anchorValue = edgeX / canvasW;

                var tgt = edgeDragger.TargetRect;
                Vector2 aMin = tgt.anchorMin;
                Vector2 aMax = tgt.anchorMax;
                if (edgeDragger.DragEdge == TUIEdgeDragger.Edge.Right)
                    aMax.x = Mathf.Clamp(anchorValue, aMin.x + 0.05f, 1f);
                else if (edgeDragger.DragEdge == TUIEdgeDragger.Edge.Left)
                    aMin.x = Mathf.Clamp(anchorValue, 0f, aMax.x - 0.05f);
                tgt.anchorMin = aMin;
                tgt.anchorMax = aMax;

                syncing = false;
            };
        }

        private void LinkEdges()
        {
            _godRightEdge = TUIEdgeDragger.Create(_godDebuggerRect, _canvasRect, TUIEdgeDragger.Edge.Right);
            _fateLeftEdge = TUIEdgeDragger.Create(_fateDebuggerRect, _canvasRect, TUIEdgeDragger.Edge.Left);

            var pBottom    = TUIEdgeDragger.Create(_godDebuggerRect,  _canvasRect, TUIEdgeDragger.Edge.Bottom);
            var aBottom    = TUIEdgeDragger.Create(_fateDebuggerRect, _canvasRect, TUIEdgeDragger.Edge.Bottom);
            var statusTop  = TUIEdgeDragger.Create(_statusPanelRect,  _canvasRect, TUIEdgeDragger.Edge.Top);

            var allHDraggers = new[]
            {
                (pBottom,   _godDebuggerRect),
                (aBottom,   _fateDebuggerRect),
                (statusTop, _statusPanelRect),
            };
            var allHTargets = new[]
            {
                (_godDebuggerRect,  TUIEdgeDragger.Edge.Bottom),
                (_fateDebuggerRect, TUIEdgeDragger.Edge.Bottom),
                (_statusPanelRect,  TUIEdgeDragger.Edge.Top),
            };
            foreach (var (dragger, ownerRect) in allHDraggers)
                foreach (var (tgtRect, tgtEdge) in allHTargets)
                    if (tgtRect != ownerRect)
                        dragger.LinkEdge(tgtRect, tgtEdge);
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        private RectTransform CreatePanel(string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvasRect, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return rt;
        }

        private void AddPanelBackground(RectTransform panel)
        {
            var img = panel.gameObject.GetComponent<Image>();
            if (img == null)
                img = panel.gameObject.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0.5f);
            img.raycastTarget = true;
        }

        private TMP_FontAsset GetFont()
        {
            if (_font != null) return _font;
            _font = Resources.Load<TMP_FontAsset>("Unifont SDF");
            return _font;
        }
    }
}
