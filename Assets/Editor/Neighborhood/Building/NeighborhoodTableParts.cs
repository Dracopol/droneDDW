using DroneDDW.Neighborhood.Table;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>The table pieces the experience needs to wire up.</summary>
    internal readonly struct NeighborhoodTableParts
    {
        #region Properties

        public NeighborhoodSlider Slider { get; }
        public NeighborhoodModelPreview Preview { get; }

        #endregion

        #region Setup

        public NeighborhoodTableParts(NeighborhoodSlider slider, NeighborhoodModelPreview preview)
        {
            Slider = slider;
            Preview = preview;
        }

        #endregion
    }
}
