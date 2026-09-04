using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterBidder.Content
{
    using MasterBidder.Core;

    [CreateAssetMenu(fileName = "CollectorData", menuName = "Master Bidder/Collector", order = 110)]
    public class CollectorData : ScriptableObject
    {
        public string collectorId;
        public string nameRu;
        [TextArea(1, 3)] public string taglineRu;
        public Sprite portrait;
        public float personalModifier = 1f;
        public int baseBudget = 300000;
        public string orderGenre;
        public string orderPeriod;
        public string orderArtist;

        [Tooltip("Optional authored mission ladder. Empty = shared genre→period→artist ladder.")]
        public List<MissionDefinition> missions = new List<MissionDefinition>();

        public int LadderLength
        {
            get
            {
                if (missions != null && missions.Count > 0) return missions.Count;
                return MasterBidder.Campaign.CampaignConfig.OrderLadderLength;
            }
        }

        public List<CriterionTag> GetOrderTagsForMission(int missionIndex)
        {
            if (missions != null && missions.Count > 0)
            {
                int idx = Mathf.Clamp(missionIndex, 0, missions.Count - 1);
                return new List<CriterionTag>(missions[idx].Tags);
            }

            return BuildLegacyTags(missionIndex);
        }

        List<CriterionTag> BuildLegacyTags(int missionIndex)
        {
            var tags = new List<CriterionTag>();
            int i = missionIndex;
            if (i < Campaign.CampaignConfig.OrderPhaseGenreDays)
            {
                tags.Add(new CriterionTag(CriterionType.Genre, orderGenre));
                return tags;
            }

            i -= Campaign.CampaignConfig.OrderPhaseGenreDays;
            if (i < Campaign.CampaignConfig.OrderPhasePeriodDays)
            {
                tags.Add(new CriterionTag(CriterionType.Period, orderPeriod));
                return tags;
            }

            tags.Add(new CriterionTag(CriterionType.Artist, orderArtist));
            return tags;
        }
    }

    [Serializable]
    public class MissionDefinition
    {
        public List<CriterionTag> Tags = new List<CriterionTag>();
    }
}
