using System;
using System.Collections.Generic;
using MasterBidder.Campaign;
using MasterBidder.Content;
using UnityEngine;

namespace MasterBidder.Core
{
    /// <summary>
    /// Port of the Game object from mvp/js/engine.js.
    /// Timers are driven by <see cref="MasterBidder.Flow.AuctionTimerHost"/> via callbacks.
    /// </summary>
    public class GameSession
    {
        public GameState State { get; private set; }
        public GameCatalog Catalog { get; }
        public System.Random Rng { get; private set; }

        public event Action OnStateChanged;
        public event Action<GameScreen> OnScreenRequested;
        public event Action OnLotTimersClearRequested;
        public event Action OnPresentLotRequested;
        public event Action OnRivalWon;
        public event Action OnAdvanceLotRequested;

        public GameSession(GameCatalog catalog, int? seed = null)
        {
            Catalog = catalog;
            Rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            Init();
        }

        public void Init()
        {
            State = new GameState();
            var first = Catalog != null ? Catalog.DefaultCollector : null;
            State.SelectedBranchId = first != null ? first.collectorId : null;
        }

        public void StartCampaign()
        {
            SaveService.Clear();
            Init();
            StartDay();
        }

        public bool ContinueCampaign()
        {
            var data = SaveService.LoadRaw();
            if (data == null) return false;
            Init();
            SaveService.Hydrate(State, data, Catalog);
            if (data.phase == "report" && State.PendingResult != null)
            {
                RaiseScreen(GameScreen.Report);
                return true;
            }
            if (State.PendingOrder == null || State.Lots == null || State.Lots.Count == 0)
                PrepareDayLots();
            RaiseScreen(GameScreen.Brief);
            return true;
        }

        public void ResetProgress()
        {
            SaveService.Clear();
            State = null;
            RaiseScreen(GameScreen.Intro);
            Notify();
        }

        public void SaveProgress(SavePhase phase)
        {
            if (State == null) return;
            SaveService.Write(State, phase);
        }

        public void StartDay()
        {
            if (State.Upgrades.Contains("investment-portfolio"))
                State.Capital = (int)Math.Round(State.Capital * 1.01);

            RequestClearTimers();
            State.DayStartCapital = State.Capital;
            State.DayConfig = CampaignConfig.GetWorldConfig(
                State.Day,
                State.Upgrades.Contains("legal-counsel"));
            State.DayOrders.Clear();
            State.PurchasesToday.Clear();
            State.ClientBudgetRemaining = 0;
            State.LotResolved = false;
            State.FastForwarding = false;
            State.AwaitingLotStart = true;
            State.CurrentLotIndex = 0;
            State.RevealStep = 0;
            State.TutorialPaused = false;
            State.TutorialStep = TutorialStep.None;
            State.LotMasterLucky = State.Upgrades.Contains("lot-master") && Rng.NextDouble() < 0.1;
            PrepareDayLots();
            RaiseScreen(GameScreen.Brief);
            SaveProgress(SavePhase.Brief);
            Notify();
        }

        public void PrepareDayLots()
        {
            var collector = Catalog.FindCollector(State.SelectedBranchId);
            if (collector == null)
            {
                Debug.LogError("[GameSession] No collector selected.");
                return;
            }

            int missionIndex = State.BranchProgress.TryGetValue(collector.collectorId, out int prog)
                ? prog
                : 0;
            var branchCfg = CampaignConfig.GetBranchMissionConfig(missionIndex, collector.LadderLength);
            var venue = CampaignConfig.GetVenue(branchCfg.VenueTier);
            var tags = collector.GetOrderTagsForMission(missionIndex);
            var order = AuctionRules.BuildOrder(
                collector, branchCfg, venue, State, tags, Catalog.paintings, Rng);

            State.PendingOrder = order;
            State.PendingVenue = venue.Tier;

            int lotsCount = venue.RollLotsCount(Rng)
                            + (State.ActiveBoosters.Contains("marathon") ? 3 : 0)
                            + (State.Upgrades.Contains("expanded-hall") ? 2 : 0);
            bool guaranteeEpic = State.ActiveBoosters.Contains("lucky-lot") || State.LotMasterLucky;

            State.Lots = AuctionRules.DrawLots(
                lotsCount,
                State.SeenArtworkIds,
                venue,
                order.CriteriaTags,
                State.Day,
                State.ArtworkPurchaseDays,
                guaranteeEpic,
                tutorialFirstDay: State.Day == 1,
                Catalog.paintings,
                Rng);
            State.CurrentLotIndex = 0;
            Notify();
        }

        public void SelectBranch(string id)
        {
            if (Catalog.FindCollector(id) == null) return;
            State.SelectedBranchId = id;
            PrepareDayLots();
            SaveProgress(SavePhase.Brief);
            Notify();
        }

        public bool BuyUpgrade(string id)
        {
            MetaUpgradeDef upgrade = null;
            for (int i = 0; i < CampaignConfig.MetaUpgrades.Length; i++)
            {
                if (CampaignConfig.MetaUpgrades[i].Id == id)
                {
                    upgrade = CampaignConfig.MetaUpgrades[i];
                    break;
                }
            }
            if (upgrade == null || State.Upgrades.Contains(id)) return false;
            if (State.Capital < upgrade.Cost) return false;
            State.Capital -= upgrade.Cost;
            State.Upgrades.Add(id);
            SaveProgress(SavePhase.Brief);
            Notify();
            return true;
        }

        public bool BuyBooster(string id)
        {
            BoosterDef booster = null;
            for (int i = 0; i < CampaignConfig.Boosters.Length; i++)
            {
                if (CampaignConfig.Boosters[i].Id == id)
                {
                    booster = CampaignConfig.Boosters[i];
                    break;
                }
            }
            if (booster == null) return false;
            if (!State.BoosterOffers.Contains(id) || State.PendingBoosters.Contains(id)) return false;
            int max = CampaignConfig.GetMaxDailyBoosters(State.Upgrades.Contains("personal-secretary"));
            if (State.PendingBoosters.Count >= max) return false;
            int cost = AuctionRules.GetBoosterCost(booster, State);
            if (State.Capital < cost) return false;
            State.Capital -= cost;
            State.PendingBoosters.Add(id);
            SaveProgress(SavePhase.Report);
            Notify();
            return true;
        }

        public void BeginAuction()
        {
            if (State.PendingOrder == null) return;
            RequestClearTimers();
            State.ClientBudgetRemaining = State.PendingOrder.Budget;
            State.DayOrders.Clear();
            State.DayOrders.Add(State.PendingOrder);
            State.CurrentVenue = State.PendingVenue;
            State.CurrentLotIndex = 0;
            State.LotResolved = false;
            State.FastForwarding = false;
            State.AwaitingLotStart = true;
            State.RevealStep = 0;
            State.TutorialPaused = false;
            State.TutorialStep = TutorialStep.None;
            RaiseScreen(GameScreen.Auction);
            Notify();
        }

        public void StartCurrentLot()
        {
            if (!State.AwaitingLotStart) return;
            State.AwaitingLotStart = false;
            State.LotResolved = false;
            State.FastForwarding = false;
            PresentLotLogicReset();
            if (State.CurrentLotIndex >= State.Lots.Count) return;
            OnPresentLotRequested?.Invoke();
            Notify();
        }

        public void PresentLotLogicReset()
        {
            if (State.CurrentLotIndex >= State.Lots.Count)
            {
                FinishDay();
                return;
            }
            State.LotResolved = false;
            State.FastForwarding = false;
            State.RevealStep = 0;
            State.TutorialPaused = false;
            State.TutorialStep = TutorialStep.None;
            State.FreeRevealedField = null;
            State.LastLotResult = null;

            if (State.ActiveBoosters.Contains("expert-appraiser"))
            {
                var fields = CampaignConfig.RevealableFields;
                State.FreeRevealedField = fields[Rng.Next(fields.Length)];
            }

            Notify();
        }

        public TutorialStep GetDay1TutorialStep(int lotIndex)
        {
            if (State == null || State.Day != 1) return TutorialStep.None;
            if (lotIndex == 0) return TutorialStep.BuyMatch;
            if (lotIndex == 1) return TutorialStep.SkipMiss;
            return TutorialStep.None;
        }

        public void PauseForTutorial(TutorialStep step)
        {
            RequestClearTimers();
            State.TutorialPaused = true;
            State.TutorialStep = step;
            Notify();
        }

        public void DismissTutorialCoach()
        {
            if (!State.TutorialPaused) return;
            State.TutorialPaused = false;
            State.TutorialStep = TutorialStep.None;
            Notify();
        }

        public bool TryBuy(out bool insufficientFunds)
        {
            insufficientFunds = false;
            if (State.AwaitingLotStart || State.LotResolved || State.FastForwarding) return false;
            // Day-1 coaching lots: never buy the miss lot; wait for the coach before buying the match.
            var day1Tut = GetDay1TutorialStep(State.CurrentLotIndex);
            if (day1Tut == TutorialStep.SkipMiss) return false;
            if (day1Tut == TutorialStep.BuyMatch && !State.TutorialPaused) return false;
            if (State.TutorialPaused && State.TutorialStep != TutorialStep.BuyMatch) return false;
            var lot = State.CurrentLot;
            if (lot == null) return false;
            int price = AuctionRules.ComputeLivePrice(lot, State.RevealStep, AuctionRules.GetPriceStepPct(State));
            if (price > State.ClientBudgetRemaining)
            {
                insufficientFunds = true;
                return false;
            }
            if (State.TutorialPaused) DismissTutorialCoach();
            RequestClearTimers();
            State.LotResolved = true;
            State.LastLotResult = "won";
            State.ClientBudgetRemaining -= price;
            State.ArtworkPurchaseDays[lot.Id] = State.Day;
            State.PurchasesToday.Add(new PurchaseRecord
            {
                Id = lot.Id,
                TitleRu = lot.TitleRu,
                PeriodRu = lot.PeriodRu,
                GenreRu = lot.GenreRu,
                ArtistRu = lot.ArtistRu,
                Rarity = lot.Rarity,
                Price = price,
                RevealStep = State.RevealStep,
                Venue = State.CurrentVenue
            });
            Notify();
            return true;
        }

        public bool TryBuy() => TryBuy(out _);

        public void ApplyRivalWin()
        {
            if (State.LotResolved || State.TutorialPaused) return;
            State.FastForwarding = false;
            RequestClearTimers();
            State.LotResolved = true;
            State.LastLotResult = "lost";
            OnRivalWon?.Invoke();
            Notify();
        }

        public void BeginSkip()
        {
            if (State.AwaitingLotStart || State.LotResolved || State.FastForwarding) return;
            // Day-1 coaching lots: never skip the match lot; wait for the coach before skipping the miss.
            var day1Tut = GetDay1TutorialStep(State.CurrentLotIndex);
            if (day1Tut == TutorialStep.BuyMatch) return;
            if (day1Tut == TutorialStep.SkipMiss && !State.TutorialPaused) return;
            if (State.TutorialPaused && State.TutorialStep != TutorialStep.SkipMiss) return;
            if (State.TutorialPaused) DismissTutorialCoach();
            State.FastForwarding = true;
            RequestClearTimers();
            Notify();
        }

        public void FinishDayEarly()
        {
            if (State.TutorialPaused) return;
            if (State.LotResolved && !State.AwaitingLotStart) return;
            State.AwaitingLotStart = false;
            State.FastForwarding = false;
            RequestClearTimers();
            State.LotResolved = true;
            FinishDay();
        }

        public void AdvanceLot()
        {
            State.CurrentLotIndex += 1;
            OnAdvanceLotRequested?.Invoke();
            Notify();
        }

        public void FinishDay()
        {
            var result = AuctionRules.ComputeSettlement(State, Rng);
            if (!result.Pass && State.Upgrades.Contains("credit-line") && !State.CreditLineUsed)
            {
                State.CreditLineUsed = true;
                result.SavedByCreditLine = true;
                result.CreditLineCoverage = -result.ProjectedCapital;
                result.Pass = true;
                result.ProjectedCapital = 0;
            }
            State.PendingResult = result;
            if (result.Pass) State.Capital = result.ProjectedCapital;

            var ids = new List<string>();
            for (int i = 0; i < CampaignConfig.Boosters.Length; i++)
                ids.Add(CampaignConfig.Boosters[i].Id);
            AuctionRules.Shuffle(ids, Rng);
            int offerCount = CampaignConfig.GetMaxDailyBoosters(State.Upgrades.Contains("personal-secretary"));
            State.BoosterOffers = ids.GetRange(0, Math.Min(offerCount, ids.Count));

            RaiseScreen(GameScreen.Report);
            SaveProgress(SavePhase.Report);
            Notify();
        }

        public void ContinueAfterReport()
        {
            var result = State.PendingResult;
            if (result == null) return;
            if (!result.Pass)
            {
                SaveService.Clear();
                RaiseScreen(GameScreen.End);
                Notify();
                return;
            }

            State.ActiveBoosters = new HashSet<string>(State.PendingBoosters);
            State.PendingBoosters.Clear();
            for (int i = 0; i < State.PurchasesToday.Count; i++)
                State.SeenArtworkIds.Add(State.PurchasesToday[i].Id);

            string branchId = State.SelectedBranchId;
            if (result.OrdersFulfilled)
            {
                int prev = State.BranchProgress.TryGetValue(branchId, out int p) ? p : 0;
                State.BranchProgress[branchId] = prev + 1;
            }

            State.Day += 1;
            if (State.Day > CampaignConfig.CampaignLength)
            {
                SaveService.Clear();
                RaiseScreen(GameScreen.End);
                Notify();
                return;
            }
            StartDay();
        }

        public void SetRevealStep(int step)
        {
            if (State == null) return;
            State.RevealStep = step;
            Notify();
        }

        public void NotifyChanged() => Notify();

        void RaiseScreen(GameScreen screen) => OnScreenRequested?.Invoke(screen);
        void Notify() => OnStateChanged?.Invoke();
        void RequestClearTimers() => OnLotTimersClearRequested?.Invoke();
    }
}
