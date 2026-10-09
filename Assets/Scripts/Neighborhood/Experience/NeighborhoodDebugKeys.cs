using DroneDDW.Neighborhood.Versions;
using DroneDDW.Neighborhood.Voice;
using UnityEngine;
#if NEIGHBORHOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DroneDDW.Neighborhood.Experience
{
    /// <summary>
    /// Keyboard shortcuts for testing without a headset: 1 to 3 pick a version, left and right arrows step,
    /// I plays the introductions, F switches the reactions between now and five years on, R replays the
    /// reactions of the current version, S stops the voices. Works with the Input System package or the old
    /// Input Manager, whichever the project uses.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Debug Keys")]
    public sealed class NeighborhoodDebugKeys : MonoBehaviour
    {
        #region Entities

        private enum DebugKey
        {
            One,
            Two,
            Three,
            Left,
            Right,
            Introductions,
            FiveYears,
            Replay,
            Stop,
        }

        #endregion

        #region Serialized fields

        [SerializeField] private NeighborhoodVersionController _controller;

        [SerializeField, Tooltip("Optional.")]
        private NeighborhoodVoiceDirector _voice;

        #endregion

        #region Update

        private void Update()
        {
            if (WasPressed(DebugKey.One))
            {
                _controller.RequestVersion(NeighborhoodVersion.Nature);
            }
            else if (WasPressed(DebugKey.Two))
            {
                _controller.RequestVersion(NeighborhoodVersion.Balanced);
            }
            else if (WasPressed(DebugKey.Three))
            {
                _controller.RequestVersion(NeighborhoodVersion.Efficiency);
            }
            else if (WasPressed(DebugKey.Left))
            {
                _controller.StepVersion(-1);
            }
            else if (WasPressed(DebugKey.Right))
            {
                _controller.StepVersion(1);
            }

            if (_voice == null)
            {
                return;
            }

            if (WasPressed(DebugKey.Introductions))
            {
                _voice.PlayIntroductions();
            }
            else if (WasPressed(DebugKey.FiveYears))
            {
                _voice.Stage = _voice.Stage == NeighborhoodVoiceStage.FiveYearsOn ? NeighborhoodVoiceStage.Now : NeighborhoodVoiceStage.FiveYearsOn;
                Debug.Log($"[Neighborhood] Reactions now use the {_voice.Stage} lines.", this);
            }
            else if (WasPressed(DebugKey.Replay))
            {
                _voice.PlayReactions(_controller.CurrentVersion);
            }
            else if (WasPressed(DebugKey.Stop))
            {
                _voice.Stop();
            }
        }

        #endregion

        #region Internal

        internal void Configure(NeighborhoodVersionController controller, NeighborhoodVoiceDirector voice)
        {
            _controller = controller;
            _voice = voice;
        }

        #endregion

        #region Private

#if NEIGHBORHOOD_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static bool WasPressed(DebugKey key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            switch (key)
            {
                case DebugKey.One: return keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame;
                case DebugKey.Two: return keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame;
                case DebugKey.Three: return keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame;
                case DebugKey.Left: return keyboard.leftArrowKey.wasPressedThisFrame;
                case DebugKey.Right: return keyboard.rightArrowKey.wasPressedThisFrame;
                case DebugKey.Introductions: return keyboard.iKey.wasPressedThisFrame;
                case DebugKey.FiveYears: return keyboard.fKey.wasPressedThisFrame;
                case DebugKey.Replay: return keyboard.rKey.wasPressedThisFrame;
                case DebugKey.Stop: return keyboard.sKey.wasPressedThisFrame;
                default: return false;
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private static bool WasPressed(DebugKey key)
        {
            switch (key)
            {
                case DebugKey.One: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
                case DebugKey.Two: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
                case DebugKey.Three: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
                case DebugKey.Left: return Input.GetKeyDown(KeyCode.LeftArrow);
                case DebugKey.Right: return Input.GetKeyDown(KeyCode.RightArrow);
                case DebugKey.Introductions: return Input.GetKeyDown(KeyCode.I);
                case DebugKey.FiveYears: return Input.GetKeyDown(KeyCode.F);
                case DebugKey.Replay: return Input.GetKeyDown(KeyCode.R);
                case DebugKey.Stop: return Input.GetKeyDown(KeyCode.S);
                default: return false;
            }
        }
#else
        private static bool WasPressed(DebugKey key)
        {
            return false;
        }
#endif

        #endregion
    }
}
