using System;
using System.Collections.Generic;
using MasterBidder.Content;
using UnityEngine;

namespace MasterBidder.Core
{
    /// <summary>
    /// Port of mvp/js/save.js — PlayerPrefs JSON persistence at brief/report only.
    /// </summary>
    public static class SaveService
    {
        public const string Key = "master-bidder-save-v1";
        public const int Version = 1;

        [Serializable]
        public class SaveData
        {
            public int version = Version;
            public string phase;
            public int day;
            public int capital;
            public int dayStartCapital;
            public int clientBudgetRemaining;
            public string[] seenArtworkIds;
            public string[] artworkPurchaseDayKeys;
            public int[] artworkPurchaseDayValues;
            public string[] upgrades;
            public string[] pendingBoosters;
            public string[] activeBoosters;
            public string[] boosterOffers;
            public bool lotMasterLucky;
            public bool creditLineUsed;
            public int dayNumber;
            public float rivalMinSec;
            public float rivalMaxSec;
            public float incorrectFitCoefficient;
            public string selectedBranchId;
            public string currentVenue;
            public string pendingVenueKey;
            public int currentLotIndex;
            public string pendingOrderJson;
            public string dayOrdersJson;
            public string lotsJson;
            public string purchasesJson;
            public string pendingResultJson;
            public string[] branchProgressKeys;
            public int[] branchProgressValues;
        }

        [Serializable]
        class LotSave
        {
            public string id;
            public int basePriceJittered;
            public bool familiar;
        }

        [Serializable]
        class LotSaveList
        {
            public LotSave[] items;
        }

        [Serializable]
        class StringList
        {
            public string[] items;
        }

        public static bool HasSave() => LoadRaw() != null;

        public static void Clear()
        {
            if (PlayerPrefs.HasKey(Key))
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
        }

        public static SaveData LoadRaw()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
                if (data == null || data.version != Version) return null;
                if (data.phase != "brief" && data.phase != "report") return null;
                return data;
            }
            catch
            {
                return null;
            }
        }

        public static void Write(GameState state, SavePhase phase)
        {
            if (state == null) return;
            var data = Serialize(state, phase);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static SaveData Serialize(GameState state, SavePhase phase)
        {
            var purchaseKeys = new List<string>();
            var purchaseVals = new List<int>();
            foreach (var kv in state.ArtworkPurchaseDays)
            {
                purchaseKeys.Add(kv.Key);
                purchaseVals.Add(kv.Value);
            }

            var branchKeys = new List<string>();
            var branchVals = new List<int>();
            foreach (var kv in state.BranchProgress)
            {
                branchKeys.Add(kv.Key);
                branchVals.Add(kv.Value);
            }

            var lots = new LotSaveList { items = new LotSave[state.Lots.Count] };
            for (int i = 0; i < state.Lots.Count; i++)
            {
                lots.items[i] = new LotSave
                {
                    id = state.Lots[i].Id,
                    basePriceJittered = state.Lots[i].BasePriceJittered,
                    familiar = state.Lots[i].Familiar
                };
            }

            return new SaveData
            {
                version = Version,
                phase = phase == SavePhase.Brief ? "brief" : "report",
                day = state.Day,
                capital = state.Capital,
                dayStartCapital = state.DayStartCapital,
                clientBudgetRemaining = state.ClientBudgetRemaining,
                seenArtworkIds = ToArray(state.SeenArtworkIds),
                artworkPurchaseDayKeys = purchaseKeys.ToArray(),
                artworkPurchaseDayValues = purchaseVals.ToArray(),
                upgrades = ToArray(state.Upgrades),
                pendingBoosters = ToArray(state.PendingBoosters),
                activeBoosters = ToArray(state.ActiveBoosters),
                boosterOffers = state.BoosterOffers.ToArray(),
                lotMasterLucky = state.LotMasterLucky,
                creditLineUsed = state.CreditLineUsed,
                dayNumber = state.DayConfig != null ? state.DayConfig.Day : state.Day,
                rivalMinSec = state.DayConfig != null ? state.DayConfig.RivalMinSec : 0f,
                rivalMaxSec = state.DayConfig != null ? state.DayConfig.RivalMaxSec : 0f,
                incorrectFitCoefficient = state.DayConfig != null ? state.DayConfig.IncorrectFitCoefficient : 0f,
                selectedBranchId = state.SelectedBranchId,
                currentVenue = state.CurrentVenue.ToString().ToLowerInvariant(),
                pendingVenueKey = state.PendingVenue.ToString().ToLowerInvariant(),
                currentLotIndex = state.CurrentLotIndex,
                pendingOrderJson = JsonUtility.ToJson(state.PendingOrder),
                dayOrdersJson = JsonUtility.ToJson(WrapOrders(state.DayOrders)),
                lotsJson = JsonUtility.ToJson(lots),
                purchasesJson = JsonUtility.ToJson(WrapPurchases(state.PurchasesToday)),
                pendingResultJson = state.PendingResult != null
                    ? JsonUtility.ToJson(state.PendingResult)
                    : string.Empty,
                branchProgressKeys = branchKeys.ToArray(),
                branchProgressValues = branchVals.ToArray()
            };
        }

        public static void Hydrate(GameState state, SaveData data, GameCatalog catalog)
        {
            state.Day = data.day;
            state.Capital = data.capital;
            state.DayStartCapital = data.dayStartCapital;
            state.ClientBudgetRemaining = data.clientBudgetRemaining;
            state.SeenArtworkIds = new HashSet<string>(data.seenArtworkIds ?? Array.Empty<string>());
            state.ArtworkPurchaseDays = new Dictionary<string, int>();
            if (data.artworkPurchaseDayKeys != null)
            {
                for (int i = 0; i < data.artworkPurchaseDayKeys.Length; i++)
                    state.ArtworkPurchaseDays[data.artworkPurchaseDayKeys[i]] = data.artworkPurchaseDayValues[i];
            }
            state.Upgrades = new HashSet<string>(data.upgrades ?? Array.Empty<string>());
            state.PendingBoosters = new HashSet<string>(data.pendingBoosters ?? Array.Empty<string>());
            state.ActiveBoosters = new HashSet<string>(data.activeBoosters ?? Array.Empty<string>());
            state.BoosterOffers = new List<string>(data.boosterOffers ?? Array.Empty<string>());
            state.LotMasterLucky = data.lotMasterLucky;
            state.CreditLineUsed = data.creditLineUsed;
            state.DayConfig = new WorldDayConfig
            {
                Day = data.dayNumber > 0 ? data.dayNumber : data.day,
                RivalMinSec = data.rivalMinSec,
                RivalMaxSec = data.rivalMaxSec,
                IncorrectFitCoefficient = data.incorrectFitCoefficient
            };
            state.SelectedBranchId = data.selectedBranchId;
            state.CurrentVenue = ParseVenue(data.currentVenue);
            state.PendingVenue = ParseVenue(data.pendingVenueKey);
            state.CurrentLotIndex = data.currentLotIndex;

            if (!string.IsNullOrEmpty(data.pendingOrderJson))
                state.PendingOrder = JsonUtility.FromJson<DayOrder>(data.pendingOrderJson);

            state.DayOrders = UnwrapOrders(data.dayOrdersJson);
            state.PurchasesToday = UnwrapPurchases(data.purchasesJson);
            state.Lots = HydrateLots(data.lotsJson, catalog, state.SeenArtworkIds);

            if (!string.IsNullOrEmpty(data.pendingResultJson))
                state.PendingResult = JsonUtility.FromJson<SettlementResult>(data.pendingResultJson);

            state.BranchProgress = new Dictionary<string, int>();
            if (data.branchProgressKeys != null)
            {
                for (int i = 0; i < data.branchProgressKeys.Length; i++)
                    state.BranchProgress[data.branchProgressKeys[i]] = data.branchProgressValues[i];
            }
        }

        static List<PresentedLot> HydrateLots(string json, GameCatalog catalog, HashSet<string> seen)
        {
            var result = new List<PresentedLot>();
            if (string.IsNullOrEmpty(json) || catalog == null) return result;
            var wrapper = JsonUtility.FromJson<LotSaveList>(json);
            if (wrapper?.items == null) return result;
            for (int i = 0; i < wrapper.items.Length; i++)
            {
                var saved = wrapper.items[i];
                var painting = catalog.FindPainting(saved.id);
                if (painting == null) continue;
                var lot = AuctionRules.ToPresentedLot(painting, saved.basePriceJittered, saved.familiar);
                result.Add(lot);
            }
            return result;
        }

        static VenueTier ParseVenue(string key)
        {
            if (string.IsNullOrEmpty(key)) return VenueTier.Regular;
            switch (key.ToLowerInvariant())
            {
                case "local": return VenueTier.Local;
                case "elite": return VenueTier.Elite;
                default: return VenueTier.Regular;
            }
        }

        static string[] ToArray(HashSet<string> set)
        {
            var arr = new string[set.Count];
            set.CopyTo(arr);
            return arr;
        }

        [Serializable]
        class OrderList
        {
            public DayOrder[] items;
        }

        [Serializable]
        class PurchaseList
        {
            public PurchaseRecord[] items;
        }

        static OrderList WrapOrders(List<DayOrder> orders) =>
            new OrderList { items = orders != null ? orders.ToArray() : Array.Empty<DayOrder>() };

        static List<DayOrder> UnwrapOrders(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<DayOrder>();
            var wrap = JsonUtility.FromJson<OrderList>(json);
            return wrap?.items != null ? new List<DayOrder>(wrap.items) : new List<DayOrder>();
        }

        static PurchaseList WrapPurchases(List<PurchaseRecord> list) =>
            new PurchaseList { items = list != null ? list.ToArray() : Array.Empty<PurchaseRecord>() };

        static List<PurchaseRecord> UnwrapPurchases(string json)
        {
            if (string.IsNullOrEmpty(json)) return new List<PurchaseRecord>();
            var wrap = JsonUtility.FromJson<PurchaseList>(json);
            return wrap?.items != null ? new List<PurchaseRecord>(wrap.items) : new List<PurchaseRecord>();
        }
    }
}
