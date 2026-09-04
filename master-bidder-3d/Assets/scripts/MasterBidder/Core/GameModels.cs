using System;
using System;
using System.Collections.Generic;

namespace MasterBidder.Core
{
    [Serializable]
    public struct CriterionTag
    {
        public CriterionType Type;
        public string Value;

        public CriterionTag(CriterionType type, string value)
        {
            Type = type;
            Value = value ?? string.Empty;
        }
    }

    [Serializable]
    public class DayOrder
    {
        public string CollectorId;
        public string NameRu;
        public string TaglineRu;
        public CriterionTag[] CriteriaTags = Array.Empty<CriterionTag>();
        public string CriteriaLabel;
        public int Budget;
        public float PersonalModifier = 1f;
        public VenueTier Venue = VenueTier.Regular;
    }

    [Serializable]
    public class PresentedLot
    {
        public string Id;
        public string TitleRu;
        public string ArtistRu;
        public string PeriodRu;
        public string GenreRu;
        public string FactRu;
        public string Year;
        public ArtworkRarity Rarity;
        public int BasePrice;
        public int BasePriceJittered;
        public bool Familiar;
    }

    [Serializable]
    public class PurchaseRecord
    {
        public string Id;
        public string TitleRu;
        public string PeriodRu;
        public string GenreRu;
        public string ArtistRu;
        public ArtworkRarity Rarity;
        public int Price;
        public int RevealStep;
        public VenueTier Venue;
    }

    [Serializable]
    public class WorldDayConfig
    {
        public int Day;
        public float RivalMinSec;
        public float RivalMaxSec;
        public float IncorrectFitCoefficient;
    }

    [Serializable]
    public class BranchMissionConfig
    {
        public int MissionIndex;
        public float TrophyChance;
        public VenueTier VenueTier;
        public float BranchBudgetMultiplier;
    }

    [Serializable]
    public class OrderStat
    {
        public DayOrder Order;
        public int Spent;
        public int CommissionEarned;
        public int CorrectCount;
        public int IncorrectCount;
        public int Leftover;
        public bool Fulfilled;
    }

    [Serializable]
    public class PurchaseDetail
    {
        public string ArtworkId;
        public string TitleRu;
        public int Price;
        public bool Matched;
        public int Amount;
        public string Reason;
    }

    [Serializable]
    public class SettlementResult
    {
        public OrderStat[] OrderStats = Array.Empty<OrderStat>();
        public PurchaseDetail[] PurchaseDetails = Array.Empty<PurchaseDetail>();
        public int TotalCommission;
        public int TotalClawback;
        public int OtherSpend;
        public int Net;
        public int StartingCapital;
        public int ProjectedCapital;
        public bool Pass;
        public bool OrdersFulfilled;
        public bool SavedByCreditLine;
        public int CreditLineCoverage;
    }

    [Serializable]
    public class MetaUpgradeDef
    {
        public string Id;
        public string NameRu;
        public string DescRu;
        public int Cost;
    }

    [Serializable]
    public class BoosterDef
    {
        public string Id;
        public string NameRu;
        public string DescRu;
        public Func<int, int> CostForNextDay;
    }
}
