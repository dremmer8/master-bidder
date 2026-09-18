using System;
using System.Collections.Generic;
using MasterBidder.Campaign;
using MasterBidder.Content;
using NineSlice3D;

namespace MasterBidder.Core
{
    /// <summary>
    /// Builds a mixed 10-card license exam from discovered paintings.
    /// </summary>
    public static class LicenseExamGenerator
    {
        static readonly string[] QuizFields = { "genre", "period", "artist", "title" };
        static readonly string[] InputFields = { "artist", "title", "genre", "period" };
        static readonly ExamQuestionKind[] KindCycle =
        {
            ExamQuestionKind.DropdownRestore,
            ExamQuestionKind.Quiz4,
            ExamQuestionKind.TextInput,
            ExamQuestionKind.PickPainting,
            ExamQuestionKind.Quiz4,
            ExamQuestionKind.DropdownRestore,
            ExamQuestionKind.TextInput,
            ExamQuestionKind.PickPainting,
            ExamQuestionKind.Quiz4,
            ExamQuestionKind.DropdownRestore
        };

        public static ExamSession Generate(GameState state, GameCatalog catalog, Random rng)
        {
            var discovered = CollectDiscovered(state, catalog);
            var all = CollectAll(catalog);
            int count = CampaignConfig.LicenseExamQuestionCount;
            var questions = new ExamQuestion[count];

            for (int i = 0; i < count; i++)
            {
                var kind = KindCycle[i % KindCycle.Length];
                var target = discovered[rng.Next(discovered.Count)];
                questions[i] = BuildQuestion(kind, target, discovered, all, state, rng);
            }

            var session = new ExamSession
            {
                Questions = questions,
                PlayerAnswers = new string[count],
                CurrentIndex = 0,
                FeePaid = true
            };

            for (int i = 0; i < count; i++)
            {
                if (questions[i].HasAutofill)
                    session.PlayerAnswers[i] = questions[i].AutofillAnswer ?? string.Empty;
                else
                    session.PlayerAnswers[i] = string.Empty;
            }

            return session;
        }

        static ExamQuestion BuildQuestion(
            ExamQuestionKind kind,
            PaintingData target,
            List<PaintingData> discovered,
            List<PaintingData> all,
            GameState state,
            Random rng)
        {
            switch (kind)
            {
                case ExamQuestionKind.DropdownRestore:
                    return BuildDropdown(target, discovered, all, state, rng);
                case ExamQuestionKind.Quiz4:
                    return BuildQuiz(target, discovered, all, state, rng);
                case ExamQuestionKind.TextInput:
                    return BuildTextInput(target, state, rng);
                case ExamQuestionKind.PickPainting:
                    return BuildPickPainting(target, discovered, all, state, rng);
                default:
                    return BuildQuiz(target, discovered, all, state, rng);
            }
        }

        static ExamQuestion BuildDropdown(
            PaintingData target,
            List<PaintingData> discovered,
            List<PaintingData> all,
            GameState state,
            Random rng)
        {
            string fieldId = QuizFields[rng.Next(QuizFields.Length)];
            string correct = CatalogProgress.FieldValue(target, fieldId);
            var options = BuildTextOptions(correct, fieldId, discovered, all, rng, 4);
            var q = new ExamQuestion
            {
                Kind = ExamQuestionKind.DropdownRestore,
                TargetArtworkId = target.artworkId,
                FieldId = fieldId,
                Prompt = PromptForField(fieldId, showPaintingHint: true),
                CorrectAnswer = correct,
                Options = options
            };
            ApplyAutofill(q, state, target.artworkId, fieldId, correct);
            return q;
        }

        static ExamQuestion BuildQuiz(
            PaintingData target,
            List<PaintingData> discovered,
            List<PaintingData> all,
            GameState state,
            Random rng)
        {
            string fieldId = QuizFields[rng.Next(QuizFields.Length)];
            string correct = CatalogProgress.FieldValue(target, fieldId);
            var options = BuildTextOptions(correct, fieldId, discovered, all, rng, 4);
            var q = new ExamQuestion
            {
                Kind = ExamQuestionKind.Quiz4,
                TargetArtworkId = target.artworkId,
                FieldId = fieldId,
                Prompt = PromptForField(fieldId, showPaintingHint: true),
                CorrectAnswer = correct,
                Options = options
            };
            ApplyAutofill(q, state, target.artworkId, fieldId, correct);
            return q;
        }

        static ExamQuestion BuildTextInput(PaintingData target, GameState state, Random rng)
        {
            string fieldId = InputFields[rng.Next(InputFields.Length)];
            string correct = CatalogProgress.FieldValue(target, fieldId);
            var q = new ExamQuestion
            {
                Kind = ExamQuestionKind.TextInput,
                TargetArtworkId = target.artworkId,
                FieldId = fieldId,
                Prompt = PromptForField(fieldId, showPaintingHint: true),
                CorrectAnswer = correct,
                Options = Array.Empty<string>()
            };
            ApplyAutofill(q, state, target.artworkId, fieldId, correct);
            return q;
        }

        static ExamQuestion BuildPickPainting(
            PaintingData target,
            List<PaintingData> discovered,
            List<PaintingData> all,
            GameState state,
            Random rng)
        {
            // Ask via a field the player may have studied; answer is the painting id.
            string fieldId = QuizFields[rng.Next(QuizFields.Length)];
            string clue = CatalogProgress.FieldValue(target, fieldId);
            var optionIds = BuildPaintingOptions(target.artworkId, discovered, all, rng, 4);
            var q = new ExamQuestion
            {
                Kind = ExamQuestionKind.PickPainting,
                TargetArtworkId = target.artworkId,
                FieldId = fieldId,
                Prompt = PickPaintingPrompt(fieldId, clue),
                CorrectAnswer = target.artworkId,
                Options = optionIds
            };

            // Autofill if the clue field is learned (player "remembers" which painting it is).
            int fi = FieldIndex(fieldId);
            if (fi >= 0 && CatalogProgress.IsLearned(state, target.artworkId, fi))
            {
                q.HasAutofill = true;
                q.AutofillAnswer = target.artworkId;
            }

            return q;
        }

        static void ApplyAutofill(ExamQuestion q, GameState state, string artworkId, string fieldId, string correct)
        {
            int fi = FieldIndex(fieldId);
            if (fi < 0) return;
            if (!CatalogProgress.IsLearned(state, artworkId, fi)) return;
            q.HasAutofill = true;
            q.AutofillAnswer = correct;
        }

        static string[] BuildTextOptions(
            string correct,
            string fieldId,
            List<PaintingData> discovered,
            List<PaintingData> all,
            Random rng,
            int count)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(correct)) set.Add(correct);

            void AddFrom(List<PaintingData> pool)
            {
                if (pool == null) return;
                var order = ShuffledIndices(pool.Count, rng);
                for (int i = 0; i < order.Length && set.Count < count; i++)
                {
                    var v = CatalogProgress.FieldValue(pool[order[i]], fieldId);
                    if (!string.IsNullOrEmpty(v)) set.Add(v);
                }
            }

            AddFrom(discovered);
            AddFrom(all);

            // Pad with placeholders if still short.
            int pad = 1;
            while (set.Count < count)
                set.Add("— " + pad++);

            var list = new List<string>(set);
            Shuffle(list, rng);
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list.ToArray();
        }

        static string[] BuildPaintingOptions(
            string correctId,
            List<PaintingData> discovered,
            List<PaintingData> all,
            Random rng,
            int count)
        {
            var ids = new List<string> { correctId };
            void AddFrom(List<PaintingData> pool)
            {
                if (pool == null) return;
                var order = ShuffledIndices(pool.Count, rng);
                for (int i = 0; i < order.Length && ids.Count < count; i++)
                {
                    var id = pool[order[i]].artworkId;
                    if (string.IsNullOrEmpty(id) || ids.Contains(id)) continue;
                    ids.Add(id);
                }
            }

            AddFrom(discovered);
            AddFrom(all);
            while (ids.Count < count)
                ids.Add(correctId + "_pad" + ids.Count);

            Shuffle(ids, rng);
            if (ids.Count > count) ids.RemoveRange(count, ids.Count - count);
            return ids.ToArray();
        }

        static string PromptForField(string fieldId, bool showPaintingHint)
        {
            string label = fieldId switch
            {
                "genre" => "жанр",
                "period" => "стиль",
                "artist" => "автор",
                "title" => "название",
                "fact" => "факт",
                _ => fieldId
            };
            return showPaintingHint
                ? $"Укажите {label} этой картины:"
                : $"Укажите {label}:";
        }

        static string PickPaintingPrompt(string fieldId, string clue)
        {
            if (string.IsNullOrEmpty(clue)) clue = "…";
            return fieldId switch
            {
                "artist" => $"Какая картина принадлежит автору «{clue}»?",
                "title" => $"Какая картина называется «{clue}»?",
                "genre" => $"Какая картина в жанре «{clue}»?",
                "period" => $"Какая картина в стиле «{clue}»?",
                _ => $"Какая картина связана с «{clue}»?"
            };
        }

        static int FieldIndex(string fieldId)
        {
            var fields = CampaignConfig.RevealableFields;
            for (int i = 0; i < fields.Length; i++)
                if (fields[i] == fieldId) return i;
            return -1;
        }

        public static List<PaintingData> CollectDiscovered(GameState state, GameCatalog catalog)
        {
            var list = new List<PaintingData>();
            if (state?.CatalogDiscoveredIds == null || catalog?.paintings == null) return list;
            for (int i = 0; i < catalog.paintings.Count; i++)
            {
                var p = catalog.paintings[i];
                if (p == null || string.IsNullOrEmpty(p.artworkId)) continue;
                if (state.CatalogDiscoveredIds.Contains(p.artworkId))
                    list.Add(p);
            }

            return list;
        }

        static List<PaintingData> CollectAll(GameCatalog catalog)
        {
            var list = new List<PaintingData>();
            if (catalog?.paintings == null) return list;
            for (int i = 0; i < catalog.paintings.Count; i++)
            {
                var p = catalog.paintings[i];
                if (p != null && !string.IsNullOrEmpty(p.artworkId))
                    list.Add(p);
            }

            return list;
        }

        static int[] ShuffledIndices(int count, Random rng)
        {
            var arr = new int[count];
            for (int i = 0; i < count; i++) arr[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }

            return arr;
        }

        static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public static bool AnswersMatch(string expected, string actual)
        {
            if (expected == null) expected = string.Empty;
            if (actual == null) actual = string.Empty;
            return string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static int Grade(ExamSession session)
        {
            if (session?.Questions == null) return 0;
            int correct = 0;
            for (int i = 0; i < session.Questions.Length; i++)
            {
                string ans = session.PlayerAnswers != null && i < session.PlayerAnswers.Length
                    ? session.PlayerAnswers[i]
                    : string.Empty;
                if (AnswersMatch(session.Questions[i].CorrectAnswer, ans))
                    correct++;
            }

            return correct;
        }
    }
}
