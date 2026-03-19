// Copyright CodeGamified 2025-2026
// MIT License — PopVuj
using UnityEngine;
using UnityEngine.InputSystem;

namespace PopVuj.Scripting
{
    /// <summary>
    /// Captures keyboard input for divine control.
    /// Encodes as a single float readable by bytecode via GET_INPUT:
    ///   0=none, 1=zoom_in (descend), 2=zoom_out (ascend),
    ///   3=scroll_left, 4=scroll_right, 5=scroll_up, 6=scroll_down
    /// </summary>
    public class PopVujInputProvider : MonoBehaviour
    {
        public static PopVujInputProvider Instance { get; private set; }

        public const float INPUT_NONE       = 0f;
        public const float INPUT_ZOOM_IN    = 1f;  // descend toward village/sewers
        public const float INPUT_ZOOM_OUT   = 2f;  // ascend toward heavens
        public const float INPUT_LEFT       = 3f;
        public const float INPUT_RIGHT      = 4f;
        public const float INPUT_UP         = 5f;
        public const float INPUT_DOWN       = 6f;

        public float CurrentInput { get; private set; }

        private InputAction _zoomInAction;
        private InputAction _zoomOutAction;
        private InputAction _leftAction;
        private InputAction _rightAction;
        private InputAction _upAction;
        private InputAction _downAction;

        private void Awake()
        {
            Instance = this;

            _zoomInAction = new InputAction("ZoomIn", InputActionType.Button);
            _zoomInAction.AddBinding("<Keyboard>/e");
            _zoomInAction.AddBinding("<Keyboard>/pageDown");
            _zoomInAction.Enable();

            _zoomOutAction = new InputAction("ZoomOut", InputActionType.Button);
            _zoomOutAction.AddBinding("<Keyboard>/q");
            _zoomOutAction.AddBinding("<Keyboard>/pageUp");
            _zoomOutAction.Enable();

            _leftAction = new InputAction("Left", InputActionType.Button);
            _leftAction.AddBinding("<Keyboard>/leftArrow");
            _leftAction.AddBinding("<Keyboard>/a");
            _leftAction.Enable();

            _rightAction = new InputAction("Right", InputActionType.Button);
            _rightAction.AddBinding("<Keyboard>/rightArrow");
            _rightAction.AddBinding("<Keyboard>/d");
            _rightAction.Enable();

            _upAction = new InputAction("Up", InputActionType.Button);
            _upAction.AddBinding("<Keyboard>/upArrow");
            _upAction.AddBinding("<Keyboard>/w");
            _upAction.Enable();

            _downAction = new InputAction("Down", InputActionType.Button);
            _downAction.AddBinding("<Keyboard>/downArrow");
            _downAction.AddBinding("<Keyboard>/s");
            _downAction.Enable();
        }

        private void Update()
        {
            if (_zoomInAction.WasPressedThisFrame())
                CurrentInput = INPUT_ZOOM_IN;
            else if (_zoomOutAction.WasPressedThisFrame())
                CurrentInput = INPUT_ZOOM_OUT;
            else if (_leftAction.WasPressedThisFrame())
                CurrentInput = INPUT_LEFT;
            else if (_rightAction.WasPressedThisFrame())
                CurrentInput = INPUT_RIGHT;
            else if (_upAction.WasPressedThisFrame())
                CurrentInput = INPUT_UP;
            else if (_downAction.WasPressedThisFrame())
                CurrentInput = INPUT_DOWN;
            else
                CurrentInput = INPUT_NONE;
        }

        private void OnDestroy()
        {
            _zoomInAction?.Disable();  _zoomInAction?.Dispose();
            _zoomOutAction?.Disable(); _zoomOutAction?.Dispose();
            _leftAction?.Disable();    _leftAction?.Dispose();
            _rightAction?.Disable();   _rightAction?.Dispose();
            _upAction?.Disable();      _upAction?.Dispose();
            _downAction?.Disable();    _downAction?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
