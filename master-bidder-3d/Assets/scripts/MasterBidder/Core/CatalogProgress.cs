using System;
using System.Collections.Generic;
using System.Globalization;
using MasterBidder.Campaign;
using NineSlice3D;

namespace MasterBidder.Core
{
    /// <summary>
    /// Catalog discovery + per-field study progress helpers.
    /// </summary>
    public static class CatalogProgress
    {
        public static int FieldCount => CampaignConfig.RevealableFields.Length;

        public static float[] GetOrCreate(GameState state, string artworkId)
        {
            if (state == null || string.IsNullOrEmpty(artworkId))
                return new float[FieldCount];

            if (!state.CatalogFieldProgress.TryGetValue(artworkId, out var arr)
                || arr == null
                || arr.Length != FieldCount)
            {
                arr = new float[FieldCount];
                state.CatalogFieldProgress[artworkId] = arr;
            }

            return arr;
        }

        public static float GetProgress(GameState state, string artworkId, int fieldIndex)
        {
            if (state == null || fieldIndex < 0 || fieldIndex >= FieldCount)
                return 0f;
            var arr = GetOrCreate(state, artworkId);
            return MathfClamp01(arr[fieldIndex]);
        }

        public static void SetProgress(GameState state, string artworkId, int fieldIndex, float value)
        {
            if (state == null || string.IsNullOrEmpty(artworkId)) return;
            if (fieldIndex < 0 || fieldIndex >= FieldCount) return;
            var arr = GetOrCreate(state, artworkId);
            arr[fieldIndex] = MathfClamp01(value);
        }

        public static bool IsLearned(GameState state, string artworkId, int fieldIndex) =>
            GetProgress(state, artworkId, fieldIndex) >= 1f;

        public static bool IsDiscovered(GameState state, string artworkId) =>
            state != null
            && !string.IsNullOrEmpty(artworkId)
            && state.CatalogDiscoveredIds != null
            && state.CatalogDiscoveredIds.Contains(artworkId);

        public static void MarkDiscovered(GameState state, string artworkId)
        {
            if (state == null || string.IsNullOrEmpty(artworkId)) return;
            if (state.CatalogDiscoveredIds == null)
                state.CatalogDiscoveredIds = new HashSet<string>();
            state.CatalogDiscoveredIds.Add(artworkId);
            GetOrCreate(state, artworkId);
        }

        public static string FieldValue(PaintingData painting, string fieldId)
        {
            if (painting == null) return string.Empty;
            switch (fieldId)
            {
                case "genre": return painting.genreRu ?? string.Empty;
                case "period": return painting.periodRu ?? string.Empty;
                case "artist": return painting.artistRu ?? string.Empty;
                case "fact": return painting.factRu ?? string.Empty;
                case "title": return painting.titleRu ?? string.Empty;
                default: return string.Empty;
            }
        }

        public static string FieldValue(PresentedLot lot, string fieldId)
        {
            if (lot == null) return string.Empty;
            switch (fieldId)
            {
                case "genre": return lot.GenreRu ?? string.Empty;
                case "period": return lot.PeriodRu ?? string.Empty;
                case "artist": return lot.ArtistRu ?? string.Empty;
                case "fact": return lot.FactRu ?? string.Empty;
                case "title": return lot.TitleRu ?? string.Empty;
                default: return string.Empty;
            }
        }

        /// <summary>
        /// Empty query → all paintings. Non-empty → discovered only, each word must
        /// appear as a substring in at least one fully learned field.
        /// </summary>
        public static bool MatchesSearch(PaintingData painting, GameState state, string query)
        {
            if (painting == null) return false;
            if (string.IsNullOrWhiteSpace(query)) return true;
            if (!IsDiscovered(state, painting.artworkId)) return false;

            var tokens = Tokenize(query);
            if (tokens.Count == 0) return true;

            var haystack = BuildLearnedHaystack(painting, state);
            for (int i = 0; i < tokens.Count; i++)
            {
                if (haystack.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            }

            return true;
        }

        static List<string> Tokenize(string query)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(query)) return result;
            var parts = query.Trim().ToLowerInvariant().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                    result.Add(parts[i]);
            }

            return result;
        }

        static string BuildLearnedHaystack(PaintingData painting, GameState state)
        {
            var fields = CampaignConfig.RevealableFields;
            var parts = new List<string>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!IsLearned(state, painting.artworkId, i)) continue;
                var value = FieldValue(painting, fields[i]);
                if (!string.IsNullOrEmpty(value))
                    parts.Add(value.ToLowerInvariant());
            }

            return string.Join(" ", parts);
        }

        public static string SerializeProgressFlat(Dictionary<string, float[]> map, out string[] keys)
        {
            if (map == null || map.Count == 0)
            {
                keys = Array.Empty<string>();
                return string.Empty;
            }

            keys = new string[map.Count];
            var flat = new float[map.Count * FieldCount];
            int i = 0;
            foreach (var kv in map)
            {
                keys[i] = kv.Key;
                var arr = kv.Value;
                for (int f = 0; f < FieldCount; f++)
                    flat[i * FieldCount + f] = arr != null && f < arr.Length ? MathfClamp01(arr[f]) : 0f;
                i++;
            }

            var sb = new System.Text.StringBuilder(flat.Length * 6);
            for (int n = 0; n < flat.Length; n++)
            {
                if (n > 0) sb.Append(',');
                sb.Append(flat[n].ToString("0.###", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        public static Dictionary<string, float[]> DeserializeProgressFlat(string[] keys, string flatCsv)
        {
            var result = new Dictionary<string, float[]>();
            if (keys == null || keys.Length == 0) return result;

            float[] flat = ParseFloatCsv(flatCsv);
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i])) continue;
                var arr = new float[FieldCount];
                for (int f = 0; f < FieldCount; f++)
                {
                    int idx = i * FieldCount + f;
                    arr[f] = idx < flat.Length ? MathfClamp01(flat[idx]) : 0f;
                }

                result[keys[i]] = arr;
            }

            return result;
        }

        static float[] ParseFloatCsv(string csv)
        {
            if (string.IsNullOrEmpty(csv)) return Array.Empty<float>();
            var parts = csv.Split(',');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                    result[i] = 0f;
            }

            return result;
        }

        static float MathfClamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
