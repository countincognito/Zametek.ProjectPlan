namespace Zametek.ViewModel.ProjectPlan
{
    public static class TrackerSearchHelper
    {
        /// <summary>
        /// Returns the symbol for a Find button: where the last tracked entry
        /// lies relative to the current tracker index (nowhere, ahead, behind,
        /// or in the currently visible position).
        /// </summary>
        public static string GetSearchSymbol(
            int? lastTrackerIndex,
            int trackerIndex)
        {
            if (lastTrackerIndex is null)
            {
                return Resource.ProjectPlan.Symbols.Symbol_Nowhere;
            }
            if (lastTrackerIndex > trackerIndex)
            {
                return Resource.ProjectPlan.Symbols.Symbol_Forwards;
            }
            if (lastTrackerIndex < trackerIndex)
            {
                return Resource.ProjectPlan.Symbols.Symbol_Backwards;
            }
            return Resource.ProjectPlan.Symbols.Symbol_InPlace;
        }
    }
}
