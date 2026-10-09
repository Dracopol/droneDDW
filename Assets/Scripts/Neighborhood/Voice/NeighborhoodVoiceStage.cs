namespace DroneDDW.Neighborhood.Voice
{
    /// <summary>When in the experience a line belongs. Values match "_stage" in the layout.</summary>
    public enum NeighborhoodVoiceStage
    {
        /// <summary>The onboarding introductions (GRA_01, WOR_01, STU_01).</summary>
        Introduction = 0,

        /// <summary>Reactions to a change the participant just made.</summary>
        Now = 1,

        /// <summary>The "five years on" lines.</summary>
        FiveYearsOn = 2,
    }
}
