using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using MasterBidder.Audio;
using MasterBidder.Content;
using NineSlice3D;
using UnityEditor;
using UnityEngine;

namespace MasterBidder.Editor
{
    /// <summary>
    /// Batch-generate modular ElevenLabs voiceovers by field type
    /// (shared genre/period/artist/year + per-painting title/fact).
    /// Menu: Master Bidder → Painting Voiceovers (ElevenLabs)
    /// </summary>
    public sealed class PaintingVoiceoverWindow : EditorWindow
    {
        const string PrefApiKey = "MasterBidder.ElevenLabs.ApiKey";
        const string PrefVoiceId = "MasterBidder.ElevenLabs.VoiceId";
        const string PrefModelId = "MasterBidder.ElevenLabs.ModelId";
        const string PrefForce = "MasterBidder.ElevenLabs.Force";
        const string PrefAssign = "MasterBidder.ElevenLabs.AssignClips";

        const string CatalogPath = "Assets/content/GameCatalog.asset";
        const string PaintingsFolder = "Assets/content/paintings/configs";
        const string DefaultModel = "eleven_multilingual_v2";
        const string OutputFormat = "mp3_44100_128";

        string _apiKey = "";
        string _voiceId = "";
        string _modelId = DefaultModel;
        bool _force;
        bool _assignLibrary = true;
        bool _genGenre = true;
        bool _genPeriod = true;
        bool _genArtist = true;
        bool _genYear = true;
        bool _genTitle = true;
        bool _genFact = true;
        string _singleFilter = "";
        int _singlePaintingIndex;
        string[] _paintingPopupLabels = Array.Empty<string>();
        List<PaintingData> _filteredPaintings = new List<PaintingData>();
        Vector2 _logScroll;
        readonly StringBuilder _log = new StringBuilder();
        List<PaintingData> _paintings = new List<PaintingData>();
        VoiceOption[] _voices = Array.Empty<VoiceOption>();
        int _voicePopupIndex;
        bool _busy;
        string _stats = "";

        const string PrefSingleId = "MasterBidder.ElevenLabs.SinglePaintingId";

        [Serializable]
        class TtsRequestBody
        {
            public string text;
            public string model_id;
            public string apply_text_normalization = "on";
        }

        [Serializable]
        class VoicesResponse
        {
            public VoiceDto[] voices;
        }

        [Serializable]
        class VoiceDto
        {
            public string voice_id;
            public string name;
        }

        struct VoiceOption
        {
            public string Id;
            public string Label;
        }

        struct ClipJob
        {
            public PaintingVoiceField Field;
            public string Key;
            public string Text;
            public string AssetPath;
        }

        [MenuItem("Master Bidder/Painting Voiceovers (ElevenLabs)", priority = 15)]
        public static void Open()
        {
            var win = GetWindow<PaintingVoiceoverWindow>("Painting Voiceovers");
            win.minSize = new Vector2(540, 620);
            win.Show();
        }

        void OnEnable()
        {
            _apiKey = EditorPrefs.GetString(PrefApiKey, "");
            _voiceId = EditorPrefs.GetString(PrefVoiceId, "");
            _modelId = EditorPrefs.GetString(PrefModelId, DefaultModel);
            _force = EditorPrefs.GetBool(PrefForce, false);
            _assignLibrary = EditorPrefs.GetBool(PrefAssign, true);
            ReloadPaintings();
        }

        void OnDisable() => PersistPrefs();

        void PersistPrefs()
        {
            EditorPrefs.SetString(PrefApiKey, _apiKey ?? "");
            EditorPrefs.SetString(PrefVoiceId, _voiceId ?? "");
            EditorPrefs.SetString(PrefModelId, string.IsNullOrEmpty(_modelId) ? DefaultModel : _modelId);
            EditorPrefs.SetBool(PrefForce, _force);
            EditorPrefs.SetBool(PrefAssign, _assignLibrary);
        }

        void OnGUI()
        {
            EditorGUI.BeginDisabledGroup(_busy);
            DrawSettings();
            EditorGUILayout.Space(6);
            DrawFieldToggles();
            EditorGUILayout.Space(6);
            DrawSinglePainting();
            EditorGUILayout.Space(6);
            DrawActions();
            EditorGUILayout.Space(6);
            if (!string.IsNullOrEmpty(_stats))
                EditorGUILayout.HelpBox(_stats, MessageType.None);
            DrawLog();
            EditorGUI.EndDisabledGroup();
        }

        void DrawSinglePainting()
        {
            EditorGUILayout.LabelField("Test one painting", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _singleFilter = EditorGUILayout.TextField("Filter", _singleFilter);
            if (EditorGUI.EndChangeCheck() || _paintingPopupLabels.Length == 0 || _filteredPaintings.Count == 0)
                RebuildPaintingPopupLabels();

            if (_paintingPopupLabels.Length == 0)
            {
                EditorGUILayout.HelpBox("No paintings match filter.", MessageType.Warning);
                return;
            }

            _singlePaintingIndex = Mathf.Clamp(_singlePaintingIndex, 0, _paintingPopupLabels.Length - 1);
            EditorGUI.BeginChangeCheck();
            _singlePaintingIndex = EditorGUILayout.Popup("Painting", _singlePaintingIndex, _paintingPopupLabels);
            if (EditorGUI.EndChangeCheck())
            {
                var sel = GetSelectedSinglePainting();
                if (sel != null)
                    EditorPrefs.SetString(PrefSingleId, sel.artworkId ?? "");
            }

            var painting = GetSelectedSinglePainting();
            if (painting != null)
            {
                EditorGUILayout.LabelField(
                    $"{painting.artworkId}  |  {painting.titleRu}  —  {painting.artistRu}",
                    EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Preview This Painting", GUILayout.Height(26)))
                    PreviewSinglePainting();
                if (GUILayout.Button("Generate This Painting", GUILayout.Height(26)))
                    GenerateSinglePainting();
            }
        }

        void DrawSettings()
        {
            EditorGUILayout.LabelField("ElevenLabs → modular field clips", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
            using (new EditorGUILayout.HorizontalScope())
            {
                _voiceId = EditorGUILayout.TextField("Voice ID", _voiceId);
                if (GUILayout.Button("Fetch Voices", GUILayout.Width(110)))
                    FetchVoices();
            }

            if (_voices.Length > 0)
            {
                var labels = new string[_voices.Length];
                for (int i = 0; i < _voices.Length; i++)
                    labels[i] = _voices[i].Label;
                int next = EditorGUILayout.Popup("Voice Picker", Mathf.Clamp(_voicePopupIndex, 0, _voices.Length - 1), labels);
                if (next != _voicePopupIndex || string.IsNullOrEmpty(_voiceId))
                {
                    _voicePopupIndex = next;
                    _voiceId = _voices[_voicePopupIndex].Id;
                }
            }

            _modelId = EditorGUILayout.TextField("Model ID", string.IsNullOrEmpty(_modelId) ? DefaultModel : _modelId);
            _force = EditorGUILayout.ToggleLeft("Force regenerate (overwrite existing mp3)", _force);
            _assignLibrary = EditorGUILayout.ToggleLeft("Rebuild PaintingVoiceoverLibrary after generate", _assignLibrary);
            if (EditorGUI.EndChangeCheck())
                PersistPrefs();

            EditorGUILayout.HelpBox(
                "Shared: genre / period / artist / year (one clip per unique value).\n" +
                "Per painting: title / fact (keyed by artworkId).\n" +
                PaintingVoicePaths.AudioRoot + "/{field}/<key>.mp3",
                MessageType.Info);
        }

        void DrawFieldToggles()
        {
            EditorGUILayout.LabelField("Generate fields", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                _genGenre = GUILayout.Toggle(_genGenre, "genre", "Button");
                _genPeriod = GUILayout.Toggle(_genPeriod, "period", "Button");
                _genArtist = GUILayout.Toggle(_genArtist, "artist", "Button");
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _genYear = GUILayout.Toggle(_genYear, "year", "Button");
                _genTitle = GUILayout.Toggle(_genTitle, "title", "Button");
                _genFact = GUILayout.Toggle(_genFact, "fact", "Button");
            }
        }

        void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload + Stats"))
                    ReloadPaintings();
                if (GUILayout.Button("Preview Jobs"))
                    PreviewJobs();
                if (GUILayout.Button("Rebuild Library Only"))
                    RebuildLibraryFromDisk();
            }

            if (GUILayout.Button("Generate Missing (selected fields)", GUILayout.Height(32)))
                GenerateJobs(onlyMissing: true);
            if (GUILayout.Button("Generate All (selected fields)", GUILayout.Height(28)))
                GenerateJobs(onlyMissing: false);
        }

        void DrawLog()
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.MinHeight(180));
            EditorGUILayout.TextArea(_log.ToString(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Clear Log", GUILayout.Width(100)))
                _log.Length = 0;
        }

        void ReloadPaintings()
        {
            _paintings = new List<PaintingData>();
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            if (catalog != null && catalog.paintings != null && catalog.paintings.Count > 0)
            {
                for (int i = 0; i < catalog.paintings.Count; i++)
                    if (catalog.paintings[i] != null)
                        _paintings.Add(catalog.paintings[i]);
            }
            else
            {
                string[] guids = AssetDatabase.FindAssets("t:PaintingData", new[] { PaintingsFolder });
                for (int i = 0; i < guids.Length; i++)
                {
                    var p = AssetDatabase.LoadAssetAtPath<PaintingData>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    if (p != null) _paintings.Add(p);
                }
            }

            _paintings.Sort((a, b) => string.CompareOrdinal(a.artworkId, b.artworkId));

            string preferredId = EditorPrefs.GetString(PrefSingleId, "");
            RebuildPaintingPopupLabels();
            if (!string.IsNullOrEmpty(preferredId))
            {
                for (int i = 0; i < _paintings.Count; i++)
                {
                    if (_paintings[i].artworkId == preferredId)
                    {
                        // Index into filtered popup is resolved in RebuildPaintingPopupLabels via matching later.
                        _singleFilter = "";
                        RebuildPaintingPopupLabels();
                        for (int j = 0; j < _paintingPopupLabels.Length; j++)
                        {
                            if (_paintingPopupLabels[j].StartsWith(preferredId + " ", StringComparison.Ordinal) ||
                                _paintingPopupLabels[j] == preferredId)
                            {
                                _singlePaintingIndex = j;
                                break;
                            }
                        }
                        break;
                    }
                }
            }

            var jobs = CollectJobs(EnabledFields());
            _stats = BuildStats(jobs);
            Log($"Loaded {_paintings.Count} paintings. Jobs for enabled fields: {jobs.Count}.");
            Repaint();
        }

        void RebuildPaintingPopupLabels()
        {
            string filter = (_singleFilter ?? "").Trim().ToLowerInvariant();
            var labels = new List<string>();
            var filtered = new List<PaintingData>();
            for (int i = 0; i < _paintings.Count; i++)
            {
                var p = _paintings[i];
                if (p == null) continue;
                string id = p.artworkId ?? "";
                string title = p.titleRu ?? "";
                if (!string.IsNullOrEmpty(filter) &&
                    id.ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0 &&
                    title.ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0 &&
                    (p.artistRu ?? "").ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0)
                    continue;
                filtered.Add(p);
                labels.Add($"{id} — {title}");
            }

            // Keep selection stable when filter changes: map by artworkId if possible.
            string selectedId = null;
            var prev = GetSelectedSinglePainting();
            if (prev != null) selectedId = prev.artworkId;

            _filteredPaintings = filtered;
            _paintingPopupLabels = labels.ToArray();
            _singlePaintingIndex = 0;
            if (!string.IsNullOrEmpty(selectedId))
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    if (filtered[i].artworkId == selectedId)
                    {
                        _singlePaintingIndex = i;
                        break;
                    }
                }
            }
        }

        PaintingData GetSelectedSinglePainting()
        {
            if (_filteredPaintings == null || _filteredPaintings.Count == 0) return null;
            int idx = Mathf.Clamp(_singlePaintingIndex, 0, _filteredPaintings.Count - 1);
            return _filteredPaintings[idx];
        }

        List<ClipJob> CollectJobsForPainting(PaintingData painting, PaintingVoiceField[] fields)
        {
            var jobs = new List<ClipJob>();
            if (painting == null || fields == null) return jobs;

            foreach (var field in fields)
            {
                string text = PaintingVoiceoverLibrary.ResolveSpokenText(painting, field);
                if (string.IsNullOrEmpty(text)) continue;

                string key = PaintingVoiceoverLibrary.ResolveKey(painting, field);
                if (string.IsNullOrEmpty(key)) continue;

                jobs.Add(new ClipJob
                {
                    Field = field,
                    Key = key,
                    Text = text,
                    AssetPath = PaintingVoicePaths.AssetPath(field, key),
                });
            }

            jobs.Sort((a, b) =>
            {
                int c = a.Field.CompareTo(b.Field);
                return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
            });
            return jobs;
        }

        static string RawField(PaintingData p, PaintingVoiceField field)
        {
            if (p == null) return "";
            switch (field)
            {
                case PaintingVoiceField.Genre: return p.genreRu;
                case PaintingVoiceField.Period: return p.periodRu;
                case PaintingVoiceField.Artist: return p.artistRu;
                case PaintingVoiceField.Year: return p.year;
                case PaintingVoiceField.Title: return p.titleRu;
                case PaintingVoiceField.Fact: return p.factRu;
                default: return "";
            }
        }

        void PreviewSinglePainting()
        {
            var painting = GetSelectedSinglePainting();
            if (painting == null)
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Select a painting first.", "OK");
                return;
            }

            EditorPrefs.SetString(PrefSingleId, painting.artworkId ?? "");
            var jobs = CollectJobsForPainting(painting, EnabledFields());
            Log($"--- Preview {painting.artworkId} ({jobs.Count} clips) ---");
            int chars = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                chars += j.Text.Length;
                bool exists = File.Exists(AbsFromAsset(j.AssetPath));
                Log($"{(exists ? "OK" : "MISS")}  {PaintingVoicePaths.FolderName(j.Field)}/{j.Key}.mp3  ({j.Text.Length})  {j.Text}");
            }
            Log($"Single painting chars ≈ {chars}");
        }

        void GenerateSinglePainting()
        {
            var painting = GetSelectedSinglePainting();
            if (painting == null)
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Select a painting first.", "OK");
                return;
            }

            EditorPrefs.SetString(PrefSingleId, painting.artworkId ?? "");
            var jobs = CollectJobsForPainting(painting, EnabledFields());
            if (!_force)
                jobs.RemoveAll(j => File.Exists(AbsFromAsset(j.AssetPath)));

            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Painting Voiceovers",
                    "All clips for this painting already exist.\nEnable Force regenerate to overwrite.",
                    "OK");
                // Still rebuild library so clips are wired.
                if (_assignLibrary) RebuildLibraryFromDisk();
                return;
            }

            Log($"Generating {jobs.Count} clip(s) for {painting.artworkId}…");
            RunGenerateJobs(jobs);
        }

        PaintingVoiceField[] EnabledFields()
        {
            var list = new List<PaintingVoiceField>();
            if (_genGenre) list.Add(PaintingVoiceField.Genre);
            if (_genPeriod) list.Add(PaintingVoiceField.Period);
            if (_genArtist) list.Add(PaintingVoiceField.Artist);
            if (_genYear) list.Add(PaintingVoiceField.Year);
            if (_genTitle) list.Add(PaintingVoiceField.Title);
            if (_genFact) list.Add(PaintingVoiceField.Fact);
            return list.ToArray();
        }

        List<ClipJob> CollectJobs(PaintingVoiceField[] fields)
        {
            var jobs = new List<ClipJob>();
            var seen = new HashSet<string>();

            foreach (var field in fields)
            {
                if (PaintingVoicePaths.IsSharedField(field))
                {
                    for (int i = 0; i < _paintings.Count; i++)
                    {
                        var p = _paintings[i];
                        string raw = RawField(p, field);
                        if (string.IsNullOrWhiteSpace(raw)) continue;
                        string key = PaintingVoicePaths.Slug(raw);
                        string text = PaintingVoiceoverLibrary.ResolveSpokenText(field, raw);
                        if (string.IsNullOrEmpty(text)) continue;
                        string dedupe = field + "|" + key;
                        if (!seen.Add(dedupe)) continue;
                        jobs.Add(new ClipJob
                        {
                            Field = field,
                            Key = key,
                            Text = text,
                            AssetPath = PaintingVoicePaths.AssetPath(field, key),
                        });
                    }
                }
                else
                {
                    for (int i = 0; i < _paintings.Count; i++)
                    {
                        var p = _paintings[i];
                        if (string.IsNullOrEmpty(p.artworkId)) continue;
                        string text = PaintingVoiceoverLibrary.ResolveSpokenText(p, field);
                        if (string.IsNullOrEmpty(text)) continue;
                        string key = p.artworkId;
                        string dedupe = field + "|" + key;
                        if (!seen.Add(dedupe)) continue;
                        jobs.Add(new ClipJob
                        {
                            Field = field,
                            Key = key,
                            Text = text,
                            AssetPath = PaintingVoicePaths.AssetPath(field, key),
                        });
                    }
                }
            }

            jobs.Sort((a, b) =>
            {
                int c = a.Field.CompareTo(b.Field);
                return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
            });
            return jobs;
        }

        string BuildStats(List<ClipJob> jobs)
        {
            var byField = new Dictionary<PaintingVoiceField, int>();
            int missing = 0;
            for (int i = 0; i < jobs.Count; i++)
            {
                var j = jobs[i];
                if (!byField.ContainsKey(j.Field)) byField[j.Field] = 0;
                byField[j.Field]++;
                if (!File.Exists(AbsFromAsset(j.AssetPath))) missing++;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Paintings: {_paintings.Count}");
            sb.AppendLine($"Unique clip jobs: {jobs.Count} (missing on disk: {missing})");
            foreach (PaintingVoiceField f in PaintingVoicePaths.AllFields)
            {
                if (!byField.TryGetValue(f, out int n)) continue;
                sb.AppendLine($"  {PaintingVoicePaths.FolderName(f)}: {n}");
            }
            return sb.ToString().TrimEnd();
        }

        void PreviewJobs()
        {
            var jobs = CollectJobs(EnabledFields());
            int chars = 0;
            int show = Mathf.Min(jobs.Count, 40);
            for (int i = 0; i < show; i++)
            {
                var j = jobs[i];
                chars += j.Text.Length;
                bool exists = File.Exists(AbsFromAsset(j.AssetPath));
                Log($"{(exists ? "OK" : "MISS")}  {PaintingVoicePaths.FolderName(j.Field)}/{j.Key}.mp3  ({j.Text.Length})  {j.Text}");
            }
            if (jobs.Count > show)
                Log($"… and {jobs.Count - show} more");
            Log($"Preview chars (shown) ≈ {chars}; total jobs {jobs.Count}");
            _stats = BuildStats(jobs);
        }

        void GenerateJobs(bool onlyMissing)
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_voiceId))
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Set API Key and Voice ID.", "OK");
                return;
            }

            PersistPrefs();
            var fields = EnabledFields();
            if (fields.Length == 0)
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Enable at least one field.", "OK");
                return;
            }

            var jobs = CollectJobs(fields);
            if (onlyMissing && !_force)
                jobs.RemoveAll(j => File.Exists(AbsFromAsset(j.AssetPath)));

            if (jobs.Count == 0)
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Nothing to generate.", "OK");
                return;
            }

            RunGenerateJobs(jobs);
        }

        void RunGenerateJobs(List<ClipJob> jobs)
        {
            if (_busy) return;
            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_voiceId))
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Set API Key and Voice ID.", "OK");
                return;
            }
            if (jobs == null || jobs.Count == 0) return;

            PersistPrefs();
            var fieldsNeeded = new HashSet<PaintingVoiceField>();
            for (int i = 0; i < jobs.Count; i++)
                fieldsNeeded.Add(jobs[i].Field);
            var fieldsArr = new PaintingVoiceField[fieldsNeeded.Count];
            fieldsNeeded.CopyTo(fieldsArr);
            EnsureAudioFolders(fieldsArr);

            _busy = true;
            int written = 0, skipped = 0, failed = 0, chars = 0;

            try
            {
                for (int i = 0; i < jobs.Count; i++)
                {
                    var job = jobs[i];
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "ElevenLabs Voiceovers",
                            $"[{i + 1}/{jobs.Count}] {PaintingVoicePaths.FolderName(job.Field)}/{job.Key}",
                            (float)i / jobs.Count))
                    {
                        Log("Cancelled.");
                        break;
                    }

                    string abs = AbsFromAsset(job.AssetPath);
                    if (!_force && File.Exists(abs))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        byte[] mp3 = RequestTts(job.Text);
                        Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? "");
                        File.WriteAllBytes(abs, mp3);
                        AssetDatabase.ImportAsset(job.AssetPath, ImportAssetOptions.ForceUpdate);
                        ConfigureAudioImporter(job.AssetPath);
                        written++;
                        chars += job.Text.Length;
                        Log($"wrote {job.AssetPath} ({mp3.Length / 1024} KB)");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Log($"FAIL {job.AssetPath}: {ex.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                _busy = false;
            }

            Log($"Done. written={written} skipped={skipped} failed={failed} chars≈{chars}");
            if (_assignLibrary)
                RebuildLibraryFromDisk();
            ReloadPaintings();
        }

        void RebuildLibraryFromDisk()
        {
            EnsureAudioFolders(PaintingVoicePaths.AllFields);
            if (!AssetDatabase.IsValidFolder(PaintingVoicePaths.AudioRoot + "/Resources"))
                AssetDatabase.CreateFolder(PaintingVoicePaths.AudioRoot, "Resources");

            // Migrate legacy library location → Resources (runtime loadable).
            if (!AssetDatabase.LoadAssetAtPath<PaintingVoiceoverLibrary>(PaintingVoicePaths.LibraryAssetPath) &&
                AssetDatabase.LoadAssetAtPath<PaintingVoiceoverLibrary>(PaintingVoicePaths.LegacyLibraryAssetPath) != null)
            {
                string err = AssetDatabase.MoveAsset(
                    PaintingVoicePaths.LegacyLibraryAssetPath,
                    PaintingVoicePaths.LibraryAssetPath);
                if (!string.IsNullOrEmpty(err))
                    Log("Library migrate note: " + err);
            }

            var library = AssetDatabase.LoadAssetAtPath<PaintingVoiceoverLibrary>(PaintingVoicePaths.LibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PaintingVoiceoverLibrary>();
                AssetDatabase.CreateAsset(library, PaintingVoicePaths.LibraryAssetPath);
            }

            foreach (PaintingVoiceField field in PaintingVoicePaths.AllFields)
            {
                var list = new List<PaintingVoiceoverLibrary.KeyedClip>();
                var jobs = CollectJobs(new[] { field });
                for (int i = 0; i < jobs.Count; i++)
                {
                    var job = jobs[i];
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(job.AssetPath);
                    if (clip == null) continue;
                    list.Add(new PaintingVoiceoverLibrary.KeyedClip
                    {
                        key = job.Key,
                        sourceText = job.Text,
                        clip = clip,
                    });
                }
                library.SetList(field, list);
            }

            library.RebuildMaps();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Log($"Rebuilt library → {PaintingVoicePaths.LibraryAssetPath}");
        }

        void FetchVoices()
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                EditorUtility.DisplayDialog("Painting Voiceovers", "Set API Key first.", "OK");
                return;
            }

            try
            {
                string json = GetJson("https://api.elevenlabs.io/v1/voices");
                var parsed = JsonUtility.FromJson<VoicesResponse>(json);
                if (parsed?.voices == null || parsed.voices.Length == 0)
                {
                    Log("No voices returned.");
                    return;
                }

                _voices = new VoiceOption[parsed.voices.Length];
                for (int i = 0; i < parsed.voices.Length; i++)
                {
                    var v = parsed.voices[i];
                    _voices[i] = new VoiceOption
                    {
                        Id = v.voice_id,
                        Label = $"{v.name}  ({v.voice_id})",
                    };
                }

                _voicePopupIndex = 0;
                for (int i = 0; i < _voices.Length; i++)
                {
                    if (_voices[i].Id == _voiceId)
                    {
                        _voicePopupIndex = i;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(_voiceId) && _voices.Length > 0)
                    _voiceId = _voices[0].Id;

                PersistPrefs();
                Log($"Fetched {_voices.Length} voices.");
            }
            catch (Exception ex)
            {
                Log($"Fetch voices failed: {ex.Message}");
            }
        }

        byte[] RequestTts(string text)
        {
            string model = string.IsNullOrEmpty(_modelId) ? DefaultModel : _modelId;
            string url =
                $"https://api.elevenlabs.io/v1/text-to-speech/{Uri.EscapeDataString(_voiceId)}?output_format={OutputFormat}";
            var body = new TtsRequestBody { text = text, model_id = model };
            string json = JsonUtility.ToJson(body);

            using (var client = CreateClient())
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
                using (var response = client.SendAsync(request).GetAwaiter().GetResult())
                {
                    byte[] data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"HTTP {(int)response.StatusCode}: {Trim(Encoding.UTF8.GetString(data), 400)}");
                    return data;
                }
            }
        }

        string GetJson(string url)
        {
            using (var client = CreateClient())
            using (var response = client.GetAsync(url).GetAwaiter().GetResult())
            {
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)response.StatusCode}: {Trim(body, 400)}");
                return body;
            }
        }

        HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            client.DefaultRequestHeaders.Add("xi-api-key", _apiKey);
            return client;
        }

        static void ConfigureAudioImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null) return;
            var settings = importer.defaultSampleSettings;
            // DecompressOnLoad is required for AudioClip.GetData → FMOD Core playback.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.quality = 1f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        [MenuItem("Master Bidder/Reimport Voiceover Clips For Playback", priority = 17)]
        public static void ReimportAllVoiceClips()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { PaintingVoicePaths.AudioRoot });
            int n = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) &&
                        !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Reimport Voiceovers", path, (float)i / Math.Max(1, guids.Length)))
                        break;
                    ConfigureAudioImporter(path);
                    n++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Reimport Voiceovers",
                $"Reimported {n} clips as DecompressOnLoad/PCM (required for in-game FMOD playback).",
                "OK");
        }

        static void EnsureAudioFolders(PaintingVoiceField[] fields)
        {
            if (!AssetDatabase.IsValidFolder("Assets/content"))
                AssetDatabase.CreateFolder("Assets", "content");
            if (!AssetDatabase.IsValidFolder("Assets/content/paintings"))
                AssetDatabase.CreateFolder("Assets/content", "paintings");
            if (!AssetDatabase.IsValidFolder(PaintingVoicePaths.AudioRoot))
                AssetDatabase.CreateFolder("Assets/content/paintings", "audio");

            foreach (var field in fields)
            {
                string folder = PaintingVoicePaths.AssetFolder(field);
                if (AssetDatabase.IsValidFolder(folder)) continue;
                AssetDatabase.CreateFolder(PaintingVoicePaths.AudioRoot, PaintingVoicePaths.FolderName(field));
            }
        }

        static string AbsFromAsset(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)).Replace('\\', '/');
            return assetPath;
        }

        void Log(string message)
        {
            _log.AppendLine(message);
            Debug.Log("[PaintingVoiceovers] " + message);
            Repaint();
        }

        static string Trim(string s, int max) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
    }
}
