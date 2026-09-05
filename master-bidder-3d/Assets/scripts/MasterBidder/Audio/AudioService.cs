using NineSlice3D;
using UnityEngine;

namespace MasterBidder.Audio
{
    /// <summary>
    /// Static facade mirroring MVP <c>sound.js</c>. Routes all gameplay SFX through FMOD via <see cref="AudioManager"/>.
    /// </summary>
    public static class AudioService
    {
        static AudioManager _manager;

        public static bool IsReady => _manager != null;
        public static bool IsMuted => _manager != null && _manager.IsMuted;

        public static void Bind(AudioManager manager) => _manager = manager;

        public static void Unbind(AudioManager manager)
        {
            if (_manager == manager)
                _manager = null;
        }

        /// <summary>
        /// Ensures an AudioManager exists on the given host (typically AppFlow).
        /// </summary>
        public static AudioManager EnsureInitialized(
            MonoBehaviour host,
            AudioCatalog catalog = null,
            PaintingVoiceoverLibrary voiceLibrary = null)
        {
            if (host == null) return _manager;

            var existing = host.GetComponent<AudioManager>();
            if (existing == null)
                existing = Object.FindObjectOfType<AudioManager>();
            if (existing == null)
                existing = host.gameObject.AddComponent<AudioManager>();

            if (catalog != null)
                existing.SetCatalog(catalog);
            else if (existing.Catalog == null)
            {
                var loaded = Resources.Load<AudioCatalog>("AudioCatalog");
                if (loaded != null)
                    existing.SetCatalog(loaded);
            }

            if (voiceLibrary != null)
                existing.SetVoiceLibrary(voiceLibrary);
            else if (existing.VoiceLibrary == null)
            {
                var loaded = Resources.Load<PaintingVoiceoverLibrary>("PaintingVoiceoverLibrary");
                if (loaded != null)
                    existing.SetVoiceLibrary(loaded);
            }

            _manager = existing;
            return existing;
        }

        public static void SetMuted(bool muted) => _manager?.SetMuted(muted);
        public static bool ToggleMute() => _manager != null && _manager.ToggleMute();

        public static void PlayClick() => Play(c => c.click, AudioCatalog.PathClick);
        public static void PlaySelect() => Play(c => c.select, AudioCatalog.PathSelect);
        public static void PlayUpgrade() => Play(c => c.upgrade, AudioCatalog.PathUpgrade);
        public static void PlayError() => Play(c => c.error, AudioCatalog.PathError);
        public static void PlaySkip() => Play(c => c.skip, AudioCatalog.PathSkip);
        public static void PlayCardOpen() => Play(c => c.cardOpen, AudioCatalog.PathCardOpen);
        public static void PlayCardClose() => Play(c => c.cardClose, AudioCatalog.PathCardClose);
        public static void PlayZoomOpen() => Play(c => c.zoomOpen, AudioCatalog.PathZoomOpen);
        public static void PlayZoomClose() => Play(c => c.zoomClose, AudioCatalog.PathZoomClose);

        public static void PlayReveal(int stepIndex, bool fast = false)
        {
            Play(c => c.reveal, AudioCatalog.PathReveal);
            if (!fast)
                SetTensionIntensity(Mathf.Clamp01((stepIndex + 1) / 5f));
        }

        public static void PlayInsight() => Play(c => c.insight, AudioCatalog.PathInsight);

        public static void PlayOutcome(string kind)
        {
            if (kind == "won")
                Play(c => c.outcomeWon, AudioCatalog.PathOutcomeWon);
            else if (kind == "lost")
                Play(c => c.outcomeLost, AudioCatalog.PathOutcomeLost);
        }

        public static void PlayRivalRaise() => Play(c => c.rivalRaise, AudioCatalog.PathRivalRaise);

        public static void PlayClothDown() => Play(c => c.clothDown, AudioCatalog.PathClothDown);
        public static void PlayNextPainting() => Play(c => c.nextPainting, AudioCatalog.PathNextPainting);

        public static void StartTension() => _manager?.StartTension();
        public static void SetTensionIntensity(float intensity01) => _manager?.SetTensionIntensity(intensity01);
        public static void StopTension() => _manager?.StopTension();

        public static void PlayDayPass() => Play(c => c.dayPass, AudioCatalog.PathDayPass);
        public static void PlayDayFail() => Play(c => c.dayFail, AudioCatalog.PathDayFail);
        public static void PlayCampaignEnd() => Play(c => c.campaignEnd, AudioCatalog.PathCampaignEnd);

        public static void PlayVoiceover(PaintingData painting) => _manager?.PlayVoiceover(painting);
        public static void PlayVoiceField(PaintingData painting, PaintingVoiceField field) =>
            _manager?.PlayVoiceField(painting, field);
        public static void PlayVoiceoverClip(AudioClip clip) => _manager?.PlayVoiceoverClip(clip);
        public static void StopVoiceover() => _manager?.StopVoiceover();

        public static void PlayRevealVoice(PaintingData painting, string fieldId)
        {
            if (painting == null || string.IsNullOrEmpty(fieldId)) return;
            PaintingVoiceField? field = null;
            switch (fieldId)
            {
                case "genre": field = PaintingVoiceField.Genre; break;
                case "period": field = PaintingVoiceField.Period; break;
                case "artist": field = PaintingVoiceField.Artist; break;
                case "fact": field = PaintingVoiceField.Fact; break;
                case "title": field = PaintingVoiceField.Title; break;
            }
            if (field.HasValue)
                PlayVoiceField(painting, field.Value);
        }

        static void Play(System.Func<AudioCatalog, FMODUnity.EventReference> selector, string fallbackPath)
        {
            if (_manager == null) return;
            var catalog = _manager.Catalog;
            var evt = catalog != null ? selector(catalog) : default;
            _manager.PlayOneShot(evt, fallbackPath);
        }
    }
}
