using System;
using System.Collections.Generic;
using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Audio
{
    /// <summary>
    /// Runtime lookup for modular painting voice clips (by field type).
    /// Built/refreshed by Master Bidder → Painting Voiceovers.
    /// </summary>
    [CreateAssetMenu(fileName = "PaintingVoiceoverLibrary", menuName = "Master Bidder/Painting Voiceover Library", order = 131)]
    public class PaintingVoiceoverLibrary : ScriptableObject
    {
        [Serializable]
        public class KeyedClip
        {
            public string key;
            public string sourceText;
            public AudioClip clip;
        }

        public List<KeyedClip> genres = new List<KeyedClip>();
        public List<KeyedClip> periods = new List<KeyedClip>();
        public List<KeyedClip> artists = new List<KeyedClip>();
        public List<KeyedClip> years = new List<KeyedClip>();
        public List<KeyedClip> titles = new List<KeyedClip>();
        public List<KeyedClip> facts = new List<KeyedClip>();

        Dictionary<string, AudioClip> _genreMap;
        Dictionary<string, AudioClip> _periodMap;
        Dictionary<string, AudioClip> _artistMap;
        Dictionary<string, AudioClip> _yearMap;
        Dictionary<string, AudioClip> _titleMap;
        Dictionary<string, AudioClip> _factMap;

        public void RebuildMaps()
        {
            _genreMap = BuildMap(genres);
            _periodMap = BuildMap(periods);
            _artistMap = BuildMap(artists);
            _yearMap = BuildMap(years);
            _titleMap = BuildMap(titles);
            _factMap = BuildMap(facts);
        }

        static Dictionary<string, AudioClip> BuildMap(List<KeyedClip> list)
        {
            var map = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            if (list == null) return map;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || string.IsNullOrEmpty(e.key) || e.clip == null) continue;
                map[e.key] = e.clip;
            }
            return map;
        }

        void OnEnable() => RebuildMaps();

        public AudioClip Find(PaintingVoiceField field, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureMaps();
            Dictionary<string, AudioClip> map;
            switch (field)
            {
                case PaintingVoiceField.Genre: map = _genreMap; break;
                case PaintingVoiceField.Period: map = _periodMap; break;
                case PaintingVoiceField.Artist: map = _artistMap; break;
                case PaintingVoiceField.Year: map = _yearMap; break;
                case PaintingVoiceField.Title: map = _titleMap; break;
                case PaintingVoiceField.Fact: map = _factMap; break;
                default: return null;
            }
            return map != null && map.TryGetValue(key, out var clip) ? clip : null;
        }

        public AudioClip FindForPainting(PaintingData painting, PaintingVoiceField field)
        {
            if (painting == null) return null;
            string key = ResolveKey(painting, field);
            return Find(field, key);
        }

        public static string ResolveKey(PaintingData painting, PaintingVoiceField field)
        {
            if (painting == null) return null;
            switch (field)
            {
                case PaintingVoiceField.Genre:
                    return PaintingVoicePaths.Slug(painting.genreRu);
                case PaintingVoiceField.Period:
                    return PaintingVoicePaths.Slug(painting.periodRu);
                case PaintingVoiceField.Artist:
                    return PaintingVoicePaths.Slug(painting.artistRu);
                case PaintingVoiceField.Year:
                    return PaintingVoicePaths.Slug(painting.year);
                case PaintingVoiceField.Title:
                case PaintingVoiceField.Fact:
                    return string.IsNullOrEmpty(painting.artworkId) ? null : painting.artworkId;
                default:
                    return null;
            }
        }

        public static string ResolveSpokenText(PaintingData painting, PaintingVoiceField field)
        {
            return PaintingVoiceText.FormatSpoken(painting, field);
        }

        /// <summary>
        /// Spoken line for a shared value (no painting context).
        /// Key must still be derived from the raw catalog string via <see cref="PaintingVoicePaths.Slug"/>.
        /// </summary>
        public static string ResolveSpokenText(PaintingVoiceField field, string rawValue)
        {
            return PaintingVoiceText.FormatSpoken(field, rawValue);
        }

        void EnsureMaps()
        {
            if (_genreMap == null) RebuildMaps();
        }

        public List<KeyedClip> GetList(PaintingVoiceField field)
        {
            switch (field)
            {
                case PaintingVoiceField.Genre: return genres;
                case PaintingVoiceField.Period: return periods;
                case PaintingVoiceField.Artist: return artists;
                case PaintingVoiceField.Year: return years;
                case PaintingVoiceField.Title: return titles;
                case PaintingVoiceField.Fact: return facts;
                default: return null;
            }
        }

        public void SetList(PaintingVoiceField field, List<KeyedClip> list)
        {
            switch (field)
            {
                case PaintingVoiceField.Genre: genres = list; break;
                case PaintingVoiceField.Period: periods = list; break;
                case PaintingVoiceField.Artist: artists = list; break;
                case PaintingVoiceField.Year: years = list; break;
                case PaintingVoiceField.Title: titles = list; break;
                case PaintingVoiceField.Fact: facts = list; break;
            }
        }
    }
}
