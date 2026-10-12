using DroneDDW.Neighborhood.Table;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>The table pieces the scene builders need to wire up. Default (all null) when the table couldn't be built.</summary>
    internal readonly struct NeighborhoodTableParts
    {
        #region Properties

        public GameObject Root { get; }
        public NeighborhoodSlider Slider { get; }

        /// <summary>The miniature preview; null on the practice table.</summary>
        public NeighborhoodModelPreview Preview { get; }

        public NeighborhoodLockButton LockButton { get; }
        public NeighborhoodHandBones HandBones { get; }

        /// <summary>The slider's hand driver; null without the XR Interaction Toolkit.</summary>
        public Component SliderDriver { get; }

        /// <summary>The arrows showing the slider slides, hidden; on the practice table only, otherwise null.</summary>
        public NeighborhoodSliderHint Hint { get; }

        #endregion

        #region Setup

        public NeighborhoodTableParts(GameObject root, NeighborhoodSlider slider, NeighborhoodModelPreview preview,
            NeighborhoodLockButton lockButton, NeighborhoodHandBones handBones, Component sliderDriver,
            NeighborhoodSliderHint hint)
        {
            Root = root;
            Slider = slider;
            Preview = preview;
            LockButton = lockButton;
            HandBones = handBones;
            SliderDriver = sliderDriver;
            Hint = hint;
        }

        #endregion
    }
}
