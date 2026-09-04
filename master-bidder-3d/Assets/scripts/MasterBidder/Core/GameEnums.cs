namespace MasterBidder.Core
{
    public enum GameScreen
    {
        Intro,
        Brief,
        Auction,
        Report,
        End
    }

    public enum SavePhase
    {
        Brief,
        Report
    }

    public enum CriterionType
    {
        Genre,
        Period,
        Artist,
        Artwork
    }

    public enum VenueTier
    {
        Local,
        Regular,
        Elite
    }

    public enum ArtworkRarity
    {
        Common,
        Rare,
        Epic
    }

    public enum LotOutcome
    {
        Won,
        Lost,
        Skipped
    }

    public enum TutorialStep
    {
        None,
        BuyMatch,
        SkipMiss
    }

    public enum EndReason
    {
        None,
        CareerComplete,
        Bankruptcy
    }
}
