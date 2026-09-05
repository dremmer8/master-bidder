using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using NineSlice3D;

namespace MasterBidder.Audio
{
    /// <summary>
    /// Formats modular voiceover lines for ElevenLabs.
    /// Based on ElevenLabs guidance:
    /// - write numbers/dates fully in words
    /// - end standalone list items with a full stop (terminal cadence, not continuation)
    /// See: https://elevenlabs.io/docs/overview/capabilities/text-to-speech/best-practices
    ///      https://help.elevenlabs.io/hc/en-us/articles/14888917355409
    /// </summary>
    public static class PaintingVoiceText
    {
        static readonly Regex YearToken = new Regex(
            @"(?<prefix>ок\.?\s*|около\s*)?(?<a>\d{3,4})(?:\s*[–—-]\s*(?<b>\d{3,4}))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string FormatSpoken(PaintingData painting, PaintingVoiceField field)
        {
            if (painting == null) return "";
            string raw;
            switch (field)
            {
                case PaintingVoiceField.Genre: raw = painting.genreRu; break;
                case PaintingVoiceField.Period: raw = painting.periodRu; break;
                case PaintingVoiceField.Artist: raw = painting.artistRu; break;
                case PaintingVoiceField.Year: raw = painting.year; break;
                case PaintingVoiceField.Title: raw = painting.titleRu; break;
                case PaintingVoiceField.Fact: raw = painting.factRu; break;
                default: return "";
            }
            return FormatSpoken(field, raw);
        }

        public static string FormatSpoken(PaintingVoiceField field, string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return "";

            string text = rawValue.Trim();
            text = text.Replace('\u00A0', ' ');
            text = Regex.Replace(text, @"\s+", " ");

            // Digits → Russian words (esp. years / ranges).
            text = ExpandDatesAndNumbers(text);

            // Strip trailing continuation cues; force a finished list-item cadence.
            text = text.Trim();
            text = Regex.Replace(text, @"(\.\.\.|…|,|;|:)\s*$", "");
            text = text.TrimEnd('.', '!', '?').Trim();
            if (text.Length == 0) return "";

            // Short catalog labels become complete spoken statements.
            switch (field)
            {
                case PaintingVoiceField.Genre:
                    text = "Жанр — " + UncapitalizeFirst(text);
                    break;
                case PaintingVoiceField.Period:
                    text = "Стиль — " + UncapitalizeFirst(text);
                    break;
                case PaintingVoiceField.Artist:
                    text = "Автор — " + text;
                    break;
                case PaintingVoiceField.Year:
                    if (!StartsWithApprox(text) && !StartsWithYearWord(text))
                        text = "Год — " + text;
                    break;
                case PaintingVoiceField.Title:
                    text = "Название — " + text;
                    break;
                case PaintingVoiceField.Fact:
                    // Keep fact body; ensure it reads as a finished statement.
                    break;
            }

            return text + ".";
        }

        static bool StartsWithApprox(string text)
        {
            return text.StartsWith("около", StringComparison.OrdinalIgnoreCase);
        }

        static bool StartsWithYearWord(string text)
        {
            return text.StartsWith("год", StringComparison.OrdinalIgnoreCase)
                   || text.StartsWith("тысяч", StringComparison.OrdinalIgnoreCase);
        }

        static string UncapitalizeFirst(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length == 1) return text.ToLowerInvariant();
            return char.ToLower(text[0], CultureInfo.GetCultureInfo("ru-RU")) + text.Substring(1);
        }

        public static string ExpandDatesAndNumbers(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return YearToken.Replace(input, m =>
            {
                string prefix = m.Groups["prefix"].Success ? m.Groups["prefix"].Value : "";
                int a = int.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture);
                bool approx = !string.IsNullOrWhiteSpace(prefix);
                if (m.Groups["b"].Success)
                {
                    int b = int.Parse(m.Groups["b"].Value, CultureInfo.InvariantCulture);
                    return FormatYearRange(a, b, approx);
                }
                return FormatSingleYear(a, approx);
            });
        }

        static string FormatSingleYear(int year, bool approx)
        {
            string ordinalGenitive = YearToRussian(year, YearForm.OrdinalGenitive);
            if (approx)
                return "около " + ordinalGenitive + " года";
            return YearToRussian(year, YearForm.OrdinalNominative) + " год";
        }

        static string FormatYearRange(int from, int to, bool approx)
        {
            string a = YearToRussian(from, YearForm.OrdinalGenitive);
            string b = YearToRussian(to, YearForm.OrdinalNominative);
            string core = "с " + a + " по " + b + " год";
            return approx ? "около периода " + core : core;
        }

        enum YearForm
        {
            OrdinalNominative, // тысяча шестьсот шестьдесят пятый
            OrdinalGenitive,   // тысячи шестисот шестидесяти пятого
        }

        /// <summary>
        /// Russian year phrasing for typical art-history dates (1000–2099).
        /// </summary>
        static string YearToRussian(int year, YearForm form = YearForm.OrdinalNominative)
        {
            if (year < 0) year = Math.Abs(year);
            if (year == 0) return "нулевой";

            // Handle 1000–1999 and 2000–2099 specially for natural speech.
            if (year >= 1000 && year <= 1999)
            {
                int rem = year - 1000;
                if (form == YearForm.OrdinalGenitive)
                {
                    if (rem == 0) return "тысячи";
                    return "тысячи " + CardinalOrOrdinalBelow1000(rem, YearForm.OrdinalGenitive);
                }
                if (rem == 0) return "тысячный";
                return "тысяча " + CardinalOrOrdinalBelow1000(rem, YearForm.OrdinalNominative);
            }

            if (year >= 2000 && year <= 2099)
            {
                int rem = year - 2000;
                if (form == YearForm.OrdinalGenitive)
                {
                    if (rem == 0) return "двухтысячного";
                    return "двух тысяч " + CardinalOrOrdinalBelow1000(rem, YearForm.OrdinalGenitive);
                }
                if (rem == 0) return "двухтысячный";
                return "две тысячи " + CardinalOrOrdinalBelow1000(rem, YearForm.OrdinalNominative);
            }

            // Fallback: spell as cardinal-ish ordinal for other ranges.
            return CardinalOrOrdinalBelow1000(year, form);
        }

        static string CardinalOrOrdinalBelow1000(int n, YearForm form)
        {
            if (n <= 0) return form == YearForm.OrdinalGenitive ? "нулевого" : "нулевой";
            if (n >= 1000) n %= 1000;

            int hundreds = n / 100;
            int rest = n % 100;
            var sb = new StringBuilder();

            if (hundreds > 0)
            {
                sb.Append(form == YearForm.OrdinalGenitive
                    ? HundredsGenitive(hundreds)
                    : HundredsNominative(hundreds));
                if (rest > 0) sb.Append(' ');
            }

            if (rest > 0)
                sb.Append(form == YearForm.OrdinalGenitive
                    ? Below100GenitiveFixed(rest)
                    : Below100NominativeFixed(rest));

            return sb.ToString().Trim();
        }

        static string HundredsNominative(int h)
        {
            switch (h)
            {
                case 1: return "сто";
                case 2: return "двести";
                case 3: return "триста";
                case 4: return "четыреста";
                case 5: return "пятьсот";
                case 6: return "шестьсот";
                case 7: return "семьсот";
                case 8: return "восемьсот";
                case 9: return "девятьсот";
                default: return "";
            }
        }

        static string HundredsGenitive(int h)
        {
            // Colloquial art-history genitive chaining: "шестисот", "пятисот"...
            switch (h)
            {
                case 1: return "ста";
                case 2: return "двухсот";
                case 3: return "трёхсот";
                case 4: return "четырёхсот";
                case 5: return "пятисот";
                case 6: return "шестисот";
                case 7: return "семисот";
                case 8: return "восьмисот";
                case 9: return "девятисот";
                default: return "";
            }
        }

        static string OnesNominative(int n)
        {
            switch (n)
            {
                case 1: return "первый";
                case 2: return "второй";
                case 3: return "третий";
                case 4: return "четвёртый";
                case 5: return "пятый";
                case 6: return "шестой";
                case 7: return "седьмой";
                case 8: return "восьмой";
                case 9: return "девятый";
                case 10: return "десятый";
                case 11: return "одиннадцатый";
                case 12: return "двенадцатый";
                case 13: return "тринадцатый";
                case 14: return "четырнадцатый";
                case 15: return "пятнадцатый";
                case 16: return "шестнадцатый";
                case 17: return "семнадцатый";
                case 18: return "восемнадцатый";
                case 19: return "девятнадцатый";
                default: return "";
            }
        }

        static string OnesGenitive(int n)
        {
            switch (n)
            {
                case 1: return "первого";
                case 2: return "второго";
                case 3: return "третьего";
                case 4: return "четвёртого";
                case 5: return "пятого";
                case 6: return "шестого";
                case 7: return "седьмого";
                case 8: return "восьмого";
                case 9: return "девятого";
                case 10: return "десятого";
                case 11: return "одиннадцатого";
                case 12: return "двенадцатого";
                case 13: return "тринадцатого";
                case 14: return "четырнадцатого";
                case 15: return "пятнадцатого";
                case 16: return "шестнадцатого";
                case 17: return "семнадцатого";
                case 18: return "восемнадцатого";
                case 19: return "девятнадцатого";
                default: return "";
            }
        }

        static string TensNominative(int tens)
        {
            switch (tens)
            {
                case 20: return "двадцатый";
                case 30: return "тридцатый";
                case 40: return "сороковой";
                case 50: return "пятидесятый";
                case 60: return "шестидесятый";
                case 70: return "семидесятый";
                case 80: return "восьмидесятый";
                case 90: return "девяностый";
                default: return "";
            }
        }

        static string TensGenitive(int tens)
        {
            switch (tens)
            {
                case 20: return "двадцатого";
                case 30: return "тридцатого";
                case 40: return "сорокового";
                case 50: return "пятидесятого";
                case 60: return "шестидесятого";
                case 70: return "семидесятого";
                case 80: return "восьмидесятого";
                case 90: return "девяностого";
                default: return "";
            }
        }

        // Compound years: "шестьдесят пятый", not "шестидесятый пятый".
        static string Below100NominativeFixed(int n)
        {
            if (n < 20) return OnesNominative(n);
            int tens = (n / 10) * 10;
            int one = n % 10;
            if (one == 0) return TensNominative(tens);
            return TensCardinal(tens) + " " + OnesNominative(one);
        }

        static string Below100GenitiveFixed(int n)
        {
            if (n < 20) return OnesGenitive(n);
            int tens = (n / 10) * 10;
            int one = n % 10;
            if (one == 0) return TensGenitive(tens);
            return TensCardinalGenitive(tens) + " " + OnesGenitive(one);
        }

        static string TensCardinal(int tens)
        {
            switch (tens)
            {
                case 20: return "двадцать";
                case 30: return "тридцать";
                case 40: return "сорок";
                case 50: return "пятьдесят";
                case 60: return "шестьдесят";
                case 70: return "семьдесят";
                case 80: return "восемьдесят";
                case 90: return "девяносто";
                default: return "";
            }
        }

        static string TensCardinalGenitive(int tens)
        {
            switch (tens)
            {
                case 20: return "двадцати";
                case 30: return "тридцати";
                case 40: return "сорока";
                case 50: return "пятидесяти";
                case 60: return "шестидесяти";
                case 70: return "семидесяти";
                case 80: return "восьмидесяти";
                case 90: return "девяноста";
                default: return "";
            }
        }
    }
}
