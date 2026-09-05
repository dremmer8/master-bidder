using System.Collections.Generic;

namespace MasterBidder.Campaign
{
    using MasterBidder.Core;

    /// <summary>
    /// Port of mvp/js/campaign.js — tunables, venues, upgrades, boosters, difficulty curves.
    /// </summary>
    public static class CampaignConfig
    {
        public const int CampaignLength = 15;
        public const int StartingCapital = 40000;

        public const float PriceStepPct = 0.12f;
        public const float SpeedMultiplierFloor = 0.35f;
        public const float IncorrectFitStart = 0.15f;
        public const float IncorrectFitEnd = -0.35f;

        /// <summary>Fallback wait between reveals when a voice clip is missing.</summary>
        public const float RevealIntervalSeconds = 2.25f;
        /// <summary>Pause before the first field reveal on a lot.</summary>
        public const float RevealLeadInSeconds = 0.85f;
        /// <summary>Extra silence after a voice line before the next field appears.</summary>
        public const float RevealVoiceTailSeconds = 0.15f;
        public const float ResolutionPauseSeconds = 2.1f;
        public const float SkipFastRevealIntervalSeconds = 0.38f;
        public const float SkipRivalPauseSeconds = 0.65f;

        public const float BranchBudgetMultiplierStart = 0.9f;
        public const float BranchBudgetMultiplierEnd = 1.6f;

        public const int OrderPhaseGenreDays = 3;
        public const int OrderPhasePeriodDays = 3;
        public const int OrderPhaseArtistDays = 4;
        public const int OrderLadderLength =
            OrderPhaseGenreDays + OrderPhasePeriodDays + OrderPhaseArtistDays;

        public const int MaxDailyBoosters = 3;
        public const int ArtworkSaleCooldownDays = 2;

        public static readonly string[] RevealableFields =
        {
            "genre", "period", "artist", "fact", "title"
        };

        public static int RarityCommissionValue(ArtworkRarity rarity)
        {
            switch (rarity)
            {
                case ArtworkRarity.Common: return 6000;
                case ArtworkRarity.Rare: return 20000;
                case ArtworkRarity.Epic: return 55000;
                default: return 6000;
            }
        }

        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static WorldDayConfig GetWorldConfig(int day, bool hasLegalCounsel)
        {
            float t = (day - 1) / (float)(CampaignLength - 1);
            float incorrectFitEnd = IncorrectFitEnd + (hasLegalCounsel ? 0.1f : 0f);
            return new WorldDayConfig
            {
                Day = day,
                RivalMinSec = Lerp(7.5f, 2.25f, t),
                RivalMaxSec = Lerp(13.5f, 5.7f, t),
                IncorrectFitCoefficient = Lerp(IncorrectFitStart, incorrectFitEnd, t)
            };
        }

        public static BranchMissionConfig GetBranchMissionConfig(int missionIndex, int ladderLength)
        {
            int length = System.Math.Max(1, ladderLength);
            float t = System.Math.Min(missionIndex, length - 1) / (float)System.Math.Max(1, length - 1);

            VenueTier venueTier;
            if (missionIndex < length * 0.2f) venueTier = VenueTier.Local;
            else if (missionIndex < length * 0.8f) venueTier = VenueTier.Regular;
            else venueTier = VenueTier.Elite;

            int trophyStart = (int)System.Math.Floor(length * 0.7f);
            float trophyChance = 0f;
            if (missionIndex >= trophyStart)
            {
                trophyChance = Lerp(
                    0f,
                    0.35f,
                    (missionIndex - trophyStart) / (float)System.Math.Max(1, length - 1 - trophyStart));
            }

            return new BranchMissionConfig
            {
                MissionIndex = missionIndex,
                TrophyChance = trophyChance,
                VenueTier = venueTier,
                BranchBudgetMultiplier = Lerp(BranchBudgetMultiplierStart, BranchBudgetMultiplierEnd, t)
            };
        }

        public static VenueDefinition GetVenue(VenueTier tier)
        {
            switch (tier)
            {
                case VenueTier.Local:
                    return new VenueDefinition(
                        VenueTier.Local,
                        "Местный аукцион",
                        new[] { ArtworkRarity.Common, ArtworkRarity.Rare },
                        budgetFactor: 0.45f,
                        minLots: 6,
                        maxLotsExclusive: 8,
                        guaranteedNonNegativeFine: true,
                        rivalSpeedFactor: 2.2f);
                case VenueTier.Elite:
                    return new VenueDefinition(
                        VenueTier.Elite,
                        "Элитный аукцион",
                        new[] { ArtworkRarity.Rare, ArtworkRarity.Epic },
                        budgetFactor: 2.4f,
                        minLots: 8,
                        maxLotsExclusive: 11,
                        guaranteedNonNegativeFine: false,
                        rivalSpeedFactor: 0.6f);
                default:
                    return new VenueDefinition(
                        VenueTier.Regular,
                        "Обычный аукцион",
                        new[] { ArtworkRarity.Common, ArtworkRarity.Rare, ArtworkRarity.Epic },
                        budgetFactor: 1f,
                        minLots: 10,
                        maxLotsExclusive: 13,
                        guaranteedNonNegativeFine: false,
                        rivalSpeedFactor: 1f);
            }
        }

        public static readonly MetaUpgradeDef[] MetaUpgrades =
        {
            new MetaUpgradeDef { Id = "fast-appraisal", NameRu = "Быстрая экспертиза", DescRu = "Порог падения множителя комиссии выше на 0.1 навсегда.", Cost = 80000 },
            new MetaUpgradeDef { Id = "expert-reputation", NameRu = "Репутация эксперта", DescRu = "Вся заработанная комиссия навсегда увеличена на 3%.", Cost = 70000 },
            new MetaUpgradeDef { Id = "cool-nerves", NameRu = "Хладнокровие", DescRu = "Цена лота навсегда растёт на 10% медленнее за каждый раскрытый признак.", Cost = 65000 },
            new MetaUpgradeDef { Id = "standing-advance", NameRu = "Постоянный аванс", DescRu = "Бюджет заказа(ов) навсегда увеличен на 8%.", Cost = 60000 },
            new MetaUpgradeDef { Id = "legal-counsel", NameRu = "Юридический советник", DescRu = "Штраф за неверную покупку на поздних днях кампании навсегда мягче.", Cost = 90000 },
            new MetaUpgradeDef { Id = "credit-line", NameRu = "Кредитная линия", DescRu = "Один раз за карьеру капитал не уходит в минус: обнуляется вместо банкротства.", Cost = 120000 },
            new MetaUpgradeDef { Id = "calm-hall", NameRu = "Спокойный зал", DescRu = "Соперники навсегда реагируют на 15% медленнее на каждом лоте.", Cost = 85000 },
            new MetaUpgradeDef { Id = "expanded-hall", NameRu = "Расширенный зал", DescRu = "В подборке навсегда на 2 лота больше каждый день.", Cost = 55000 },
            new MetaUpgradeDef { Id = "lot-master", NameRu = "Мастер лотов", DescRu = "Каждый день есть 10% шанс, что в подборку бесплатно добавится гарантированный эпический лот.", Cost = 70000 },
            new MetaUpgradeDef { Id = "loyal-client", NameRu = "Постоянный клиент", DescRu = "Цена всех бустеров навсегда ниже на 15%.", Cost = 50000 },
            new MetaUpgradeDef { Id = "personal-secretary", NameRu = "Личный секретарь", DescRu = "Каждый вечер предлагается на 1 бустер больше — и все их можно купить.", Cost = 95000 },
            new MetaUpgradeDef { Id = "investment-portfolio", NameRu = "Инвестиционный портфель", DescRu = "В начале каждого дня капитал навсегда растёт на 1%.", Cost = 100000 },
        };

        public static readonly BoosterDef[] Boosters =
        {
            new BoosterDef { Id = "insurance", NameRu = "Страховка на день", DescRu = "Завтра комиссия за ошибочную покупку не может стать штрафом.", CostForNextDay = d => Round100(15000 + d * 1000) },
            new BoosterDef { Id = "expert-appraiser", NameRu = "Опытный оценщик", DescRu = "Завтра на каждом лоте один случайный признак раскрыт бесплатно с самого начала.", CostForNextDay = d => Round100(20000 + d * 1200) },
            new BoosterDef { Id = "quiet-start", NameRu = "Тихий старт", DescRu = "Завтра на первом лоте дня соперник вообще не подключается.", CostForNextDay = d => Round100(9000 + d * 600) },
            new BoosterDef { Id = "sleepy-rivals", NameRu = "Сонные соперники", DescRu = "Завтра соперники реагируют на 45% медленнее на каждом лоте.", CostForNextDay = d => Round100(24000 + d * 1400) },
            new BoosterDef { Id = "auction-discount", NameRu = "Скидка аукциона", DescRu = "Завтра цена лота растёт на треть медленнее за каждый раскрытый признак.", CostForNextDay = d => Round100(16000 + d * 900) },
            new BoosterDef { Id = "budget-advance", NameRu = "Аванс от заказчика", DescRu = "Завтра бюджет заказа(ов) увеличен на 20%.", CostForNextDay = d => Round100(14000 + d * 900) },
            new BoosterDef { Id = "commission-bonus", NameRu = "Комиссионный бонус", DescRu = "Вся комиссия, заработанная завтра, увеличена на 5%.", CostForNextDay = d => Round100(10000 + d * 700) },
            new BoosterDef { Id = "lucky-lot", NameRu = "Счастливый лот", DescRu = "В завтрашней подборке гарантированно будет хотя бы один эпический лот.", CostForNextDay = d => Round100(17000 + d * 1000) },
            new BoosterDef { Id = "marathon", NameRu = "Марафон", DescRu = "Завтра в зале на 3 лота больше.", CostForNextDay = d => Round100(15000 + d * 900) },
        };

        public static int GetMaxDailyBoosters(bool hasPersonalSecretary) =>
            MaxDailyBoosters + (hasPersonalSecretary ? 1 : 0);

        public static int Round100(int value) => (int)System.Math.Round(value / 100.0) * 100;

        public static int Round100(float value) => (int)System.Math.Round(value / 100.0) * 100;
    }

    public readonly struct VenueDefinition
    {
        public readonly VenueTier Tier;
        public readonly string LabelRu;
        public readonly ArtworkRarity[] RarityPool;
        public readonly float BudgetFactor;
        public readonly int MinLots;
        public readonly int MaxLotsExclusive;
        public readonly bool GuaranteedNonNegativeFine;
        public readonly float RivalSpeedFactor;

        public VenueDefinition(
            VenueTier tier,
            string labelRu,
            ArtworkRarity[] rarityPool,
            float budgetFactor,
            int minLots,
            int maxLotsExclusive,
            bool guaranteedNonNegativeFine,
            float rivalSpeedFactor)
        {
            Tier = tier;
            LabelRu = labelRu;
            RarityPool = rarityPool;
            BudgetFactor = budgetFactor;
            MinLots = minLots;
            MaxLotsExclusive = maxLotsExclusive;
            GuaranteedNonNegativeFine = guaranteedNonNegativeFine;
            RivalSpeedFactor = rivalSpeedFactor;
        }

        public int RollLotsCount(System.Random rng) =>
            MinLots + rng.Next(0, MaxLotsExclusive - MinLots);

        public bool AllowsRarity(ArtworkRarity rarity)
        {
            for (int i = 0; i < RarityPool.Length; i++)
            {
                if (RarityPool[i] == rarity) return true;
            }
            return false;
        }
    }
}
