using System;
using System.Collections.Generic;
using NineSlice3D;
using MasterBidder.Campaign;
using MasterBidder.Content;

namespace MasterBidder.Core
{
    /// <summary>
    /// Pure auction / economy helpers ported from mvp/js/engine.js (no Unity scene deps).
    /// </summary>
    public static class AuctionRules
    {
        public static string MaskValue(string text)
        {
            int len = string.IsNullOrEmpty(text) ? 6 : text.Length;
            len = Math.Max(6, Math.Min(len, 28));
            return new string('•', len);
        }

        public static bool MatchesCriteria(PresentedLot artwork, IList<CriterionTag> criteriaTags)
        {
            if (criteriaTags == null || criteriaTags.Count == 0) return false;
            for (int i = 0; i < criteriaTags.Count; i++)
            {
                var tag = criteriaTags[i];
                switch (tag.Type)
                {
                    case CriterionType.Artwork:
                        if (artwork.Id != tag.Value) return false;
                        break;
                    case CriterionType.Period:
                        if (artwork.PeriodRu != tag.Value) return false;
                        break;
                    case CriterionType.Genre:
                        if (artwork.GenreRu != tag.Value) return false;
                        break;
                    case CriterionType.Artist:
                        if (artwork.ArtistRu != tag.Value) return false;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        public static bool MatchesCriteria(PaintingData artwork, IList<CriterionTag> criteriaTags)
        {
            if (artwork == null) return false;
            return MatchesCriteria(ToPresentedLot(artwork, 0, false), criteriaTags);
        }

        public static PresentedLot ToPresentedLot(PaintingData artwork, int jitteredPrice, bool familiar)
        {
            return new PresentedLot
            {
                Id = artwork.artworkId,
                TitleRu = artwork.titleRu,
                ArtistRu = artwork.artistRu,
                PeriodRu = artwork.periodRu,
                GenreRu = artwork.genreRu,
                FactRu = artwork.factRu,
                Year = artwork.year,
                Rarity = GameCatalog.ParseRarity(artwork.rarity),
                BasePrice = artwork.basePrice,
                BasePriceJittered = jitteredPrice,
                Familiar = familiar
            };
        }

        public static int JitterPrice(int basePrice, Random rng)
        {
            float factor = 0.85f + (float)rng.NextDouble() * 0.3f;
            return CampaignConfig.Round100(basePrice * factor);
        }

        public static int MaxPossibleLivePrice(PaintingData artwork)
        {
            const float maxJitter = 1.15f;
            int maxStep = CampaignConfig.RevealableFields.Length;
            return CampaignConfig.Round100(artwork.basePrice * maxJitter * (1 + maxStep * CampaignConfig.PriceStepPct));
        }

        public static int ComputeLivePrice(PresentedLot lot, int step, float priceStepPct)
        {
            return CampaignConfig.Round100(lot.BasePriceJittered * (1 + step * priceStepPct));
        }

        public static float GetPriceStepPct(GameState state)
        {
            float permanent = state.Upgrades.Contains("cool-nerves") ? 0.9f : 1f;
            float daily = state.ActiveBoosters.Contains("auction-discount") ? 0.67f : 1f;
            return CampaignConfig.PriceStepPct * permanent * daily;
        }

        public static float ComputeSpeedMultiplier(int step, float floor)
        {
            int maxStep = CampaignConfig.RevealableFields.Length;
            float t = Math.Min(step, maxStep) / (float)maxStep;
            return Math.Max(floor, 1f - t * (1f - floor));
        }

        public static float GetSpeedFloor(GameState state) =>
            CampaignConfig.SpeedMultiplierFloor + (state.Upgrades.Contains("fast-appraisal") ? 0.1f : 0f);

        public static int GetBoosterCost(BoosterDef booster, GameState state)
        {
            int baseCost = booster.CostForNextDay(state.Day + 1);
            float discount = state.Upgrades.Contains("loyal-client") ? 0.85f : 1f;
            return CampaignConfig.Round100(baseCost * discount);
        }

        public static bool IsArtworkOnSaleCooldown(string artworkId, int currentDay, Dictionary<string, int> purchaseDays)
        {
            if (purchaseDays == null || !purchaseDays.TryGetValue(artworkId, out int purchaseDay))
                return false;
            int daysSince = currentDay - purchaseDay;
            return daysSince >= 1 && daysSince <= CampaignConfig.ArtworkSaleCooldownDays;
        }

        public static string DescribeCriteria(IList<CriterionTag> tags)
        {
            if (tags == null || tags.Count == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < tags.Count; i++)
            {
                string label;
                switch (tags[i].Type)
                {
                    case CriterionType.Period: label = "Период"; break;
                    case CriterionType.Genre: label = "Жанр"; break;
                    case CriterionType.Artist: label = "Автор"; break;
                    case CriterionType.Artwork: label = "Работа"; break;
                    default: label = tags[i].Type.ToString(); break;
                }
                parts.Add($"{label}: {tags[i].Value}");
            }
            return string.Join(" и ", parts);
        }

        public static List<PaintingData> FilterByVenue(IList<PaintingData> all, VenueDefinition venue)
        {
            var list = new List<PaintingData>();
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a == null || string.IsNullOrEmpty(a.artworkId)) continue;
                if (venue.AllowsRarity(GameCatalog.ParseRarity(a.rarity)))
                    list.Add(a);
            }
            return list;
        }

        public static List<PaintingData> MatchingInVenue(
            IList<PaintingData> all,
            IList<CriterionTag> tags,
            VenueDefinition venue)
        {
            var list = new List<PaintingData>();
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a == null) continue;
                if (!venue.AllowsRarity(GameCatalog.ParseRarity(a.rarity))) continue;
                if (MatchesCriteria(a, tags)) list.Add(a);
            }
            return list;
        }

        public static List<PaintingData> FulfillableMatches(
            IList<PaintingData> all,
            IList<CriterionTag> tags,
            VenueDefinition venue)
        {
            var inPool = MatchingInVenue(all, tags, venue);
            if (inPool.Count > 0) return inPool;
            var any = new List<PaintingData>();
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a != null && MatchesCriteria(a, tags)) any.Add(a);
            }
            return any;
        }

        public static int BudgetFloorForArtworks(IList<PaintingData> artworks)
        {
            if (artworks == null || artworks.Count == 0) return 0;
            int max = 0;
            for (int i = 0; i < artworks.Count; i++)
            {
                int price = MaxPossibleLivePrice(artworks[i]);
                if (price > max) max = price;
            }
            return max;
        }

        public static void Shuffle<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static float RandRange(Random rng, float min, float max) =>
            min + (float)rng.NextDouble() * (max - min);

        public static DayOrder BuildOrder(
            CollectorData collector,
            BranchMissionConfig branchCfg,
            VenueDefinition venue,
            GameState state,
            List<CriterionTag> tags,
            IList<PaintingData> catalog,
            Random rng)
        {
            bool wantsTrophy = venue.Tier != VenueTier.Local
                               && branchCfg.TrophyChance > 0f
                               && (float)rng.NextDouble() < branchCfg.TrophyChance;

            float permanentAdvance = state.Upgrades.Contains("standing-advance") ? 1.08f : 1f;
            float dailyAdvance = state.ActiveBoosters.Contains("budget-advance") ? 1.2f : 1f;
            float advanceMultiplier = permanentAdvance * dailyAdvance;

            int budget = CampaignConfig.Round100(
                collector.baseBudget * branchCfg.BranchBudgetMultiplier * venue.BudgetFactor * advanceMultiplier);

            if (wantsTrophy)
            {
                var candidates = MatchingInVenue(catalog, tags, venue);
                if (candidates.Count > 0)
                {
                    var target = candidates[rng.Next(candidates.Count)];
                    int floor = CampaignConfig.Round100(MaxPossibleLivePrice(target) * advanceMultiplier);
                    budget = Math.Max(budget, floor);
                    return new DayOrder
                    {
                        CollectorId = collector.collectorId,
                        NameRu = collector.nameRu,
                        TaglineRu = collector.taglineRu,
                        CriteriaTags = new[]
                        {
                            new CriterionTag(CriterionType.Artwork, target.artworkId)
                        },
                        CriteriaLabel = $"Точная работа: {target.titleRu} — {target.artistRu}",
                        Budget = budget,
                        PersonalModifier = collector.personalModifier,
                        Venue = venue.Tier
                    };
                }
            }

            int categoryFloor = CampaignConfig.Round100(
                BudgetFloorForArtworks(FulfillableMatches(catalog, tags, venue)) * advanceMultiplier);
            budget = Math.Max(budget, categoryFloor);

            return new DayOrder
            {
                CollectorId = collector.collectorId,
                NameRu = collector.nameRu,
                TaglineRu = collector.taglineRu,
                CriteriaTags = tags != null ? tags.ToArray() : Array.Empty<CriterionTag>(),
                CriteriaLabel = DescribeCriteria(tags),
                Budget = budget,
                PersonalModifier = collector.personalModifier,
                Venue = venue.Tier
            };
        }

        public static List<PresentedLot> DrawLots(
            int count,
            HashSet<string> seenSet,
            VenueDefinition venue,
            IList<CriterionTag> orderCriteriaTags,
            int currentDay,
            Dictionary<string, int> purchaseDays,
            bool guaranteeEpic,
            bool tutorialFirstDay,
            IList<PaintingData> catalog,
            Random rng)
        {
            List<PaintingData> FilterCooldown(IList<PaintingData> source)
            {
                var list = new List<PaintingData>();
                for (int i = 0; i < source.Count; i++)
                {
                    var a = source[i];
                    if (a == null || string.IsNullOrEmpty(a.artworkId)) continue;
                    if (!IsArtworkOnSaleCooldown(a.artworkId, currentDay, purchaseDays))
                        list.Add(a);
                }
                return list;
            }

            var pool = FilterCooldown(FilterByVenue(catalog, venue));
            var matches = FilterCooldown(FulfillableMatches(catalog, orderCriteriaTags, venue));
            var fallbackMatches = FulfillableMatches(catalog, orderCriteriaTags, venue);
            var guaranteedSource = matches.Count > 0 ? matches : fallbackMatches;
            PaintingData matchSeed = guaranteedSource.Count > 0
                ? guaranteedSource[rng.Next(guaranteedSource.Count)]
                : null;

            var fillerPool = pool.Count > 0 ? pool : FilterByVenue(catalog, venue);

            PresentedLot Present(PaintingData a) =>
                ToPresentedLot(a, JitterPrice(a.basePrice, rng), seenSet != null && seenSet.Contains(a.artworkId));

            if (tutorialFirstDay)
            {
                var ordered = new List<PaintingData>();
                var used = new HashSet<string>();
                if (matchSeed != null)
                {
                    ordered.Add(matchSeed);
                    used.Add(matchSeed.artworkId);
                }

                var mismatchCandidates = new List<PaintingData>();
                for (int i = 0; i < fillerPool.Count; i++)
                {
                    var a = fillerPool[i];
                    if (used.Contains(a.artworkId)) continue;
                    if (MatchesCriteria(a, orderCriteriaTags)) continue;
                    mismatchCandidates.Add(a);
                }
                Shuffle(mismatchCandidates, rng);

                PaintingData mismatch = mismatchCandidates.Count > 0 ? mismatchCandidates[0] : null;
                if (mismatch == null)
                {
                    for (int i = 0; i < catalog.Count; i++)
                    {
                        var a = catalog[i];
                        if (a == null || used.Contains(a.artworkId)) continue;
                        if (!MatchesCriteria(a, orderCriteriaTags))
                        {
                            mismatch = a;
                            break;
                        }
                    }
                }

                if (mismatch != null && used.Add(mismatch.artworkId))
                    ordered.Add(mismatch);

                if (guaranteeEpic)
                {
                    bool hasEpic = false;
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        if (GameCatalog.ParseRarity(ordered[i].rarity) == ArtworkRarity.Epic)
                        {
                            hasEpic = true;
                            break;
                        }
                    }
                    if (!hasEpic)
                    {
                        var epics = new List<PaintingData>();
                        for (int i = 0; i < catalog.Count; i++)
                        {
                            var a = catalog[i];
                            if (a != null && !used.Contains(a.artworkId)
                                && GameCatalog.ParseRarity(a.rarity) == ArtworkRarity.Epic)
                                epics.Add(a);
                        }
                        if (epics.Count > 0)
                        {
                            var epic = epics[rng.Next(epics.Count)];
                            ordered.Add(epic);
                            used.Add(epic.artworkId);
                        }
                    }
                }

                var fillers = new List<PaintingData>();
                for (int i = 0; i < fillerPool.Count; i++)
                {
                    if (!used.Contains(fillerPool[i].artworkId)) fillers.Add(fillerPool[i]);
                }
                Shuffle(fillers, rng);
                int need = Math.Max(0, count - ordered.Count);
                for (int i = 0; i < need && i < fillers.Count; i++)
                    ordered.Add(fillers[i]);

                var result = new List<PresentedLot>(ordered.Count);
                for (int i = 0; i < ordered.Count; i++)
                    result.Add(Present(ordered[i]));
                return result;
            }

            var guaranteed = new List<PaintingData>();
            if (matchSeed != null) guaranteed.Add(matchSeed);

            if (guaranteeEpic)
            {
                bool hasEpic = false;
                for (int i = 0; i < guaranteed.Count; i++)
                {
                    if (GameCatalog.ParseRarity(guaranteed[i].rarity) == ArtworkRarity.Epic)
                    {
                        hasEpic = true;
                        break;
                    }
                }
                if (!hasEpic)
                {
                    var epicOff = FilterCooldown(FilterByRarity(catalog, ArtworkRarity.Epic));
                    var epicPool = epicOff.Count > 0 ? epicOff : FilterByRarity(catalog, ArtworkRarity.Epic);
                    if (epicPool.Count > 0)
                        guaranteed.Add(epicPool[rng.Next(epicPool.Count)]);
                }
            }

            var guaranteedIds = new HashSet<string>();
            for (int i = 0; i < guaranteed.Count; i++)
                guaranteedIds.Add(guaranteed[i].artworkId);

            var fillerList = new List<PaintingData>();
            for (int i = 0; i < fillerPool.Count; i++)
            {
                if (!guaranteedIds.Contains(fillerPool[i].artworkId))
                    fillerList.Add(fillerPool[i]);
            }
            Shuffle(fillerList, rng);

            int needFill = Math.Max(0, count - guaranteed.Count);
            var picked = new List<PaintingData>(guaranteed);
            for (int i = 0; i < needFill && i < fillerList.Count; i++)
                picked.Add(fillerList[i]);
            Shuffle(picked, rng);

            var presented = new List<PresentedLot>(picked.Count);
            for (int i = 0; i < picked.Count; i++)
                presented.Add(Present(picked[i]));
            return presented;
        }

        static List<PaintingData> FilterByRarity(IList<PaintingData> all, ArtworkRarity rarity)
        {
            var list = new List<PaintingData>();
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a != null && GameCatalog.ParseRarity(a.rarity) == rarity)
                    list.Add(a);
            }
            return list;
        }

        public static SettlementResult ComputeSettlement(GameState state, Random rng)
        {
            float speedFloor = GetSpeedFloor(state);
            float permanentCommission = state.Upgrades.Contains("expert-reputation") ? 1.03f : 1f;
            float commissionBonus = (state.ActiveBoosters.Contains("commission-bonus") ? 1.05f : 1f)
                                    * permanentCommission;

            var orderStats = new List<OrderStat>();
            for (int i = 0; i < state.DayOrders.Count; i++)
            {
                orderStats.Add(new OrderStat { Order = state.DayOrders[i] });
            }

            int totalCommission = 0;
            var purchaseDetails = new List<PurchaseDetail>();

            for (int pIdx = 0; pIdx < state.PurchasesToday.Count; pIdx++)
            {
                var p = state.PurchasesToday[pIdx];
                var matchingIdx = new List<int>();
                for (int i = 0; i < state.DayOrders.Count; i++)
                {
                    if (MatchesCriteria(
                            new PresentedLot
                            {
                                Id = p.Id,
                                PeriodRu = p.PeriodRu,
                                GenreRu = p.GenreRu,
                                ArtistRu = p.ArtistRu
                            },
                            state.DayOrders[i].CriteriaTags))
                    {
                        matchingIdx.Add(i);
                    }
                }

                int creditIdx;
                bool matched;
                if (matchingIdx.Count > 0)
                {
                    creditIdx = matchingIdx[rng.Next(matchingIdx.Count)];
                    matched = true;
                }
                else
                {
                    var sameVenue = new List<int>();
                    for (int i = 0; i < state.DayOrders.Count; i++)
                    {
                        if (state.DayOrders[i].Venue == p.Venue) sameVenue.Add(i);
                    }
                    var pool = sameVenue.Count > 0 ? sameVenue : Range(state.DayOrders.Count);
                    creditIdx = pool[rng.Next(pool.Count)];
                    matched = false;
                }

                var orderStat = orderStats[creditIdx];
                orderStat.Spent += p.Price;

                int rarityValue = CampaignConfig.RarityCommissionValue(p.Rarity);
                float speedMultiplier = ComputeSpeedMultiplier(p.RevealStep, speedFloor);
                float fitCoefficient;
                if (matched)
                {
                    fitCoefficient = 1f;
                    orderStat.CorrectCount += 1;
                }
                else
                {
                    fitCoefficient = state.DayConfig.IncorrectFitCoefficient;
                    var venueDef = CampaignConfig.GetVenue(p.Venue);
                    bool noFine = venueDef.GuaranteedNonNegativeFine
                                  || state.ActiveBoosters.Contains("insurance");
                    if (noFine) fitCoefficient = Math.Max(0f, fitCoefficient);
                    orderStat.IncorrectCount += 1;
                }

                int commission = (int)Math.Round(
                    rarityValue * speedMultiplier * fitCoefficient
                    * orderStat.Order.PersonalModifier * commissionBonus);
                orderStat.CommissionEarned += commission;
                totalCommission += commission;

                purchaseDetails.Add(new PurchaseDetail
                {
                    ArtworkId = p.Id,
                    TitleRu = p.TitleRu,
                    Price = p.Price,
                    Matched = matched,
                    Amount = commission,
                    Reason = matched
                        ? $"Подходит под заказ {orderStat.Order.NameRu}"
                        : $"Не подходит под заказ {orderStat.Order.NameRu}"
                });
            }

            int totalClawback = 0;
            for (int i = 0; i < orderStats.Count; i++)
            {
                var o = orderStats[i];
                o.Leftover = Math.Max(0, o.Order.Budget - o.Spent);
                o.Fulfilled = o.CorrectCount > 0;
                totalClawback += o.Leftover;
            }

            int net = totalCommission;
            int projectedCapital = state.Capital + net;
            bool pass = projectedCapital >= 0;
            bool ordersFulfilled = orderStats.Count > 0;
            for (int i = 0; i < orderStats.Count; i++)
            {
                if (!orderStats[i].Fulfilled)
                {
                    ordersFulfilled = false;
                    break;
                }
            }

            int dayNet = projectedCapital - state.DayStartCapital;
            int otherSpend = dayNet - net;

            return new SettlementResult
            {
                OrderStats = orderStats.ToArray(),
                PurchaseDetails = purchaseDetails.ToArray(),
                TotalCommission = totalCommission,
                TotalClawback = totalClawback,
                OtherSpend = otherSpend,
                Net = net,
                StartingCapital = state.DayStartCapital,
                ProjectedCapital = projectedCapital,
                Pass = pass,
                OrdersFulfilled = ordersFulfilled
            };
        }

        static List<int> Range(int count)
        {
            var list = new List<int>(count);
            for (int i = 0; i < count; i++) list.Add(i);
            return list;
        }
    }
}
