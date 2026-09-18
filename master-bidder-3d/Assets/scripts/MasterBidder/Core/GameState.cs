using System.Collections.Generic;

namespace MasterBidder.Core
{
    /// <summary>
    /// Mutable campaign/auction state — port of Game.state from mvp/js/engine.js.
    /// </summary>
    public class GameState
    {
        public int Day = 1;
        public int Capital = Campaign.CampaignConfig.StartingCapital;
        public int ClientBudgetRemaining;
        public int DayStartCapital = Campaign.CampaignConfig.StartingCapital;

        public HashSet<string> SeenArtworkIds = new HashSet<string>();
        /// <summary>Artwork ids whose image was revealed after the auction cloth raised.</summary>
        public HashSet<string> CatalogDiscoveredIds = new HashSet<string>();
        /// <summary>Per-artwork study progress for the 5 reveal fields (0..1 each).</summary>
        public Dictionary<string, float[]> CatalogFieldProgress = new Dictionary<string, float[]>();
        public Dictionary<string, int> ArtworkPurchaseDays = new Dictionary<string, int>();
        public HashSet<string> Upgrades = new HashSet<string>();
        public HashSet<string> PendingBoosters = new HashSet<string>();
        public HashSet<string> ActiveBoosters = new HashSet<string>();
        /// <summary>Global venue licenses: "regular", "elite".</summary>
        public HashSet<string> OwnedLicenses = new HashSet<string>();
        public List<string> BoosterOffers = new List<string>();

        /// <summary>In-progress license exam (not persisted mid-attempt).</summary>
        public ExamSession ActiveExam;

        public bool LotMasterLucky;
        public bool CreditLineUsed;

        public WorldDayConfig DayConfig;
        public List<DayOrder> DayOrders = new List<DayOrder>();
        public List<PresentedLot> Lots = new List<PresentedLot>();
        public DayOrder PendingOrder;
        public VenueTier PendingVenue = VenueTier.Regular;
        public int CurrentLotIndex;
        public List<PurchaseRecord> PurchasesToday = new List<PurchaseRecord>();

        public bool LotResolved;
        public int RevealStep;
        public bool FastForwarding;
        public bool AwaitingLotStart = true;
        public SettlementResult PendingResult;

        public Dictionary<string, int> BranchProgress = new Dictionary<string, int>();
        public string SelectedBranchId;
        public VenueTier CurrentVenue = VenueTier.Regular;

        public bool TutorialPaused;
        public TutorialStep TutorialStep = TutorialStep.None;

        /// <summary>Field id free-revealed by expert-appraiser (no price/speed effect).</summary>
        public string FreeRevealedField;

        /// <summary>Last race banner: won / lost / empty.</summary>
        public string LastLotResult;

        public PresentedLot CurrentLot =>
            Lots != null && CurrentLotIndex >= 0 && CurrentLotIndex < Lots.Count
                ? Lots[CurrentLotIndex]
                : null;
    }
}
