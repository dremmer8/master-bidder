using MasterBidder.Content;
using MasterBidder.Core;
using MasterBidder.Campaign;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Quick headless check that catalog + rules can build a day-1 order/lots.
    /// Menu: Master Bidder → Validate Game Logic
    /// </summary>
    public static class GameLogicValidator
    {
        [UnityEditor.MenuItem("Master Bidder/Validate Game Logic", priority = 20)]
        public static void Validate()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<GameCatalog>("Assets/content/GameCatalog.asset");
            if (catalog == null || catalog.paintings == null || catalog.paintings.Count == 0)
            {
                UnityEditor.EditorUtility.DisplayDialog(
                    "Validate Game Logic",
                    "GameCatalog empty. Run Sync MVP Content first.",
                    "OK");
                return;
            }

            int withId = 0;
            for (int i = 0; i < catalog.paintings.Count; i++)
            {
                if (catalog.paintings[i] != null && !string.IsNullOrEmpty(catalog.paintings[i].artworkId))
                    withId++;
            }

            var session = new GameSession(catalog, seed: 42);
            session.StartCampaign();
            var state = session.State;
            string msg =
                $"Paintings with artworkId: {withId}/{catalog.paintings.Count}\n" +
                $"Collectors: {catalog.collectors.Count}\n" +
                $"Day {state.Day}, capital {state.Capital}\n" +
                $"Branch: {state.SelectedBranchId}\n" +
                $"Venue: {state.PendingVenue}\n" +
                $"Order: {state.PendingOrder?.CriteriaLabel}\n" +
                $"Budget: {state.PendingOrder?.Budget:N0}\n" +
                $"Lots: {state.Lots.Count}\n" +
                $"Starting capital const: {CampaignConfig.StartingCapital}";

            Debug.Log("[GameLogicValidator]\n" + msg);
            UnityEditor.EditorUtility.DisplayDialog("Validate Game Logic", msg, "OK");
        }
    }
}
