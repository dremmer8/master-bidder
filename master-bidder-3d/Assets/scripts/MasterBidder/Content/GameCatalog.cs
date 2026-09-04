using System.Collections.Generic;
using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Content
{
    using MasterBidder.Core;

    /// <summary>
    /// Runtime catalog: paintings + collectors. Built/refreshed by the MVP importer.
    /// </summary>
    [CreateAssetMenu(fileName = "GameCatalog", menuName = "Master Bidder/Game Catalog", order = 120)]
    public class GameCatalog : ScriptableObject
    {
        public List<PaintingData> paintings = new List<PaintingData>();
        public List<CollectorData> collectors = new List<CollectorData>();

        public PaintingData FindPainting(string artworkId)
        {
            if (string.IsNullOrEmpty(artworkId) || paintings == null) return null;
            for (int i = 0; i < paintings.Count; i++)
            {
                var p = paintings[i];
                if (p != null && p.artworkId == artworkId) return p;
            }
            return null;
        }

        public CollectorData FindCollector(string collectorId)
        {
            if (string.IsNullOrEmpty(collectorId) || collectors == null) return null;
            for (int i = 0; i < collectors.Count; i++)
            {
                var c = collectors[i];
                if (c != null && c.collectorId == collectorId) return c;
            }
            return null;
        }

        public CollectorData DefaultCollector =>
            collectors != null && collectors.Count > 0 ? collectors[0] : null;

        public static ArtworkRarity ParseRarity(string rarity)
        {
            if (string.IsNullOrEmpty(rarity)) return ArtworkRarity.Common;
            switch (rarity.Trim().ToLowerInvariant())
            {
                case "rare": return ArtworkRarity.Rare;
                case "epic": return ArtworkRarity.Epic;
                default: return ArtworkRarity.Common;
            }
        }

        public static string RarityToString(ArtworkRarity rarity)
        {
            switch (rarity)
            {
                case ArtworkRarity.Rare: return "rare";
                case ArtworkRarity.Epic: return "epic";
                default: return "common";
            }
        }
    }
}
