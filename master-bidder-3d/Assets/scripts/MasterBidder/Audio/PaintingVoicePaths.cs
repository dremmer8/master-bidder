using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MasterBidder.Audio
{
    public enum PaintingVoiceField
    {
        Genre,
        Period,
        Artist,
        Year,
        Title,
        Fact,
    }

    /// <summary>
    /// Shared folder / file naming for modular painting voiceovers.
    /// Shared fields (genre/period/artist/year) are keyed by text slug;
    /// per-painting fields (title/fact) are keyed by artworkId.
    /// Layout: Assets/content/paintings/audio/{genre|period|artist|year|title|fact}/&lt;key&gt;.mp3
    /// </summary>
    public static class PaintingVoicePaths
    {
        public const string AudioRoot = "Assets/content/paintings/audio";
        /// <summary>Under a Resources folder so runtime can Resources.Load it.</summary>
        public const string LibraryAssetPath = AudioRoot + "/Resources/PaintingVoiceoverLibrary.asset";
        public const string LegacyLibraryAssetPath = AudioRoot + "/PaintingVoiceoverLibrary.asset";

        public static readonly PaintingVoiceField[] AllFields =
        {
            PaintingVoiceField.Genre,
            PaintingVoiceField.Period,
            PaintingVoiceField.Artist,
            PaintingVoiceField.Year,
            PaintingVoiceField.Title,
            PaintingVoiceField.Fact,
        };

        public static readonly PaintingVoiceField[] SharedFields =
        {
            PaintingVoiceField.Genre,
            PaintingVoiceField.Period,
            PaintingVoiceField.Artist,
            PaintingVoiceField.Year,
        };

        public static string FolderName(PaintingVoiceField field)
        {
            switch (field)
            {
                case PaintingVoiceField.Genre: return "genre";
                case PaintingVoiceField.Period: return "period";
                case PaintingVoiceField.Artist: return "artist";
                case PaintingVoiceField.Year: return "year";
                case PaintingVoiceField.Title: return "title";
                case PaintingVoiceField.Fact: return "fact";
                default: return "misc";
            }
        }

        public static bool IsSharedField(PaintingVoiceField field) =>
            field == PaintingVoiceField.Genre
            || field == PaintingVoiceField.Period
            || field == PaintingVoiceField.Artist
            || field == PaintingVoiceField.Year;

        public static string AssetFolder(PaintingVoiceField field) =>
            $"{AudioRoot}/{FolderName(field)}";

        public static string AssetPath(PaintingVoiceField field, string fileKey) =>
            $"{AssetFolder(field)}/{fileKey}.mp3";

        /// <summary>
        /// Stable ASCII filename from Russian (or any) label.
        /// </summary>
        public static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "empty";

            var map = new Dictionary<char, string>
            {
                ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo",
                ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
                ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
                ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch",
                ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
            };

            var sb = new StringBuilder();
            foreach (char raw in value.Trim().ToLowerInvariant())
            {
                if (map.TryGetValue(raw, out var rep))
                {
                    sb.Append(rep);
                    continue;
                }

                if (char.IsLetterOrDigit(raw))
                    sb.Append(raw);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '-')
                    sb.Append('-');
            }

            string slug = sb.ToString().Trim('-');
            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");
            if (slug.Length > 80)
                slug = slug.Substring(0, 80).Trim('-');
            return string.IsNullOrEmpty(slug) ? "empty" : slug;
        }
    }
}
