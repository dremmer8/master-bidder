using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FMOD;
using FMODUnity;
using NineSlice3D;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MasterBidder.Audio
{
    /// <summary>
    /// Scene bootstrap for FMOD SFX + painting voiceover clips.
    /// Voiceovers play via FMOD Core (Unity Audio is disabled by FMOD setup).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        const string MutePrefKey = "mb-sound-muted";

        [SerializeField] AudioCatalog catalog;
        [SerializeField] PaintingVoiceoverLibrary voiceLibrary;
        [SerializeField] bool addListenerToMainCamera = true;
        [SerializeField] float voiceoverGapSeconds = 0.12f;
        [SerializeField] [Range(0f, 1f)] float voiceoverVolume = 1f;

        FMOD.Studio.EventInstance _tension;
        Sound _voiceSound;
        Channel _voiceChannel;
        readonly Queue<AudioClip> _voiceQueue = new Queue<AudioClip>();
        float _nextVoiceAt;
        float _voiceDeadline;
        bool _voiceHeardPlaying;
        bool _muted;
        bool _warnedMissing;

        public AudioCatalog Catalog => catalog;
        public PaintingVoiceoverLibrary VoiceLibrary => voiceLibrary;
        public bool IsMuted => _muted;

        public static AudioManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _muted = PlayerPrefs.GetInt(MutePrefKey, 0) == 1;
            voiceLibrary?.RebuildMaps();
            EnsureListener();
            ApplyMuteToBus();
            AudioService.Bind(this);
            EnsureVoiceLibrary();
        }

        void OnDestroy()
        {
            StopTension();
            StopVoiceover();
            if (Instance == this)
            {
                AudioService.Unbind(this);
                Instance = null;
            }
        }

        public void SetCatalog(AudioCatalog value) => catalog = value;

        public void SetVoiceLibrary(PaintingVoiceoverLibrary value)
        {
            voiceLibrary = value;
            voiceLibrary?.RebuildMaps();
        }

        public void SetMuted(bool muted)
        {
            _muted = muted;
            PlayerPrefs.SetInt(MutePrefKey, muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMuteToBus();
            if (muted)
            {
                StopTension();
                StopVoiceover();
            }
        }

        public bool ToggleMute()
        {
            SetMuted(!_muted);
            return _muted;
        }

        void ApplyMuteToBus()
        {
            string path = catalog != null && !string.IsNullOrEmpty(catalog.masterBusPath)
                ? catalog.masterBusPath
                : "bus:/";
            try
            {
                if (RuntimeManager.StudioSystem.getBus(path, out var bus) == RESULT.OK)
                    bus.setVolume(_muted ? 0f : 1f);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioManager] Mute bus failed: {ex.Message}");
            }
        }

        void EnsureListener()
        {
            if (!addListenerToMainCamera) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (cam.GetComponent<StudioListener>() == null)
                cam.gameObject.AddComponent<StudioListener>();
        }

        public void PlayOneShot(EventReference evt, string fallbackPath)
        {
            if (_muted) return;
            try
            {
                if (!evt.IsNull)
                {
                    RuntimeManager.PlayOneShot(evt);
                    return;
                }

                if (string.IsNullOrEmpty(fallbackPath)) return;
                if (RuntimeManager.StudioSystem.getEvent(fallbackPath, out _) != RESULT.OK)
                {
                    WarnMissingOnce(fallbackPath);
                    return;
                }

                RuntimeManager.PlayOneShot(fallbackPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioManager] PlayOneShot failed ({fallbackPath}): {ex.Message}");
            }
        }

        void WarnMissingOnce(string path)
        {
            if (_warnedMissing) return;
            _warnedMissing = true;
            Debug.LogWarning(
                $"[AudioManager] FMOD event missing: '{path}'. Create events in FMOD Studio, build banks, assign AudioCatalog. Further missing-event warnings suppressed.");
        }

        public void StartTension()
        {
            if (_muted || catalog == null) return;
            StopTension();

            var evt = catalog.tension;
            string path = AudioCatalog.PathTension;
            try
            {
                if (!evt.IsNull)
                    _tension = RuntimeManager.CreateInstance(evt);
                else if (RuntimeManager.StudioSystem.getEvent(path, out _) == RESULT.OK)
                    _tension = RuntimeManager.CreateInstance(path);
                else
                {
                    WarnMissingOnce(path);
                    return;
                }

                _tension.start();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioManager] StartTension failed: {ex.Message}");
            }
        }

        public void SetTensionIntensity(float intensity01)
        {
            if (!_tension.isValid()) return;
            intensity01 = Mathf.Clamp01(intensity01);
            _tension.setParameterByName("intensity", intensity01);
            _tension.setParameterByName("Intensity", intensity01);
        }

        public void StopTension()
        {
            if (!_tension.isValid()) return;
            _tension.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            _tension.release();
            _tension.clearHandle();
        }

        public void PlayVoiceover(PaintingData painting)
        {
            if (painting == null || _muted) return;
            EnsureVoiceLibrary();
            if (voiceLibrary == null) return;

            var clips = new List<AudioClip>(PaintingVoicePaths.AllFields.Length);
            for (int i = 0; i < PaintingVoicePaths.AllFields.Length; i++)
            {
                var field = PaintingVoicePaths.AllFields[i];
                var clip = voiceLibrary.FindForPainting(painting, field);
                if (clip != null) clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning($"[AudioManager] No modular voice clips for '{painting.artworkId}'.");
                return;
            }

            StopVoiceover();
            for (int i = 0; i < clips.Count; i++)
                _voiceQueue.Enqueue(clips[i]);
            _nextVoiceAt = 0f;
            PlayNextQueuedVoice();
        }

        /// <summary>Returns clip length in seconds (0 if missing / muted).</summary>
        public float PlayVoiceField(PaintingData painting, PaintingVoiceField field)
        {
            if (painting == null || _muted) return 0f;
            EnsureVoiceLibrary();
            if (voiceLibrary == null) return 0f;
            var clip = voiceLibrary.FindForPainting(painting, field);
            if (clip == null)
            {
                Debug.LogWarning(
                    $"[AudioManager] Missing voice clip for {painting.artworkId} / {field} " +
                    $"(key='{PaintingVoiceoverLibrary.ResolveKey(painting, field)}').");
                return 0f;
            }

            StopVoiceover();
            PlayVoiceoverClip(clip);
            return Mathf.Max(0f, clip.length);
        }

        public float GetVoiceFieldLength(PaintingData painting, PaintingVoiceField field)
        {
            if (painting == null) return 0f;
            EnsureVoiceLibrary();
            var clip = voiceLibrary?.FindForPainting(painting, field);
            return clip != null ? Mathf.Max(0f, clip.length) : 0f;
        }

        public bool IsVoicePlaying =>
            _voiceChannel.hasHandle() || _voiceQueue.Count > 0;

        public void PlayVoiceoverClip(AudioClip clip)
        {
            if (_muted || clip == null) return;
            ReleaseCurrentVoice();

            try
            {
                if (clip.loadState != AudioDataLoadState.Loaded)
                    clip.LoadAudioData();

                int channels = clip.channels;
                int frequency = clip.frequency;
                int sampleCount = clip.samples * channels;
                if (channels <= 0 || frequency <= 0 || sampleCount <= 0)
                {
                    Debug.LogWarning($"[AudioManager] Invalid clip data for '{clip.name}'.");
                    return;
                }

                var samples = new float[sampleCount];
                if (!clip.GetData(samples, 0))
                {
                    Debug.LogWarning(
                        $"[AudioManager] GetData failed for '{clip.name}'. " +
                        "Reimport via Master Bidder → Painting Voiceovers → Reimport Voiceover Clips For Playback.");
                    return;
                }

                var pcm = new byte[sampleCount * sizeof(short)];
                for (int i = 0; i < sampleCount; i++)
                {
                    short s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
                    pcm[i * 2] = (byte)(s & 0xff);
                    pcm[i * 2 + 1] = (byte)((s >> 8) & 0xff);
                }

                var exinfo = new CREATESOUNDEXINFO();
                exinfo.cbsize = Marshal.SizeOf(typeof(CREATESOUNDEXINFO));
                exinfo.length = (uint)pcm.Length;
                exinfo.numchannels = channels;
                exinfo.defaultfrequency = frequency;
                exinfo.format = SOUND_FORMAT.PCM16;

                // OPENRAW is required: without it FMOD treats the buffer as a file container (wav/mp3), not PCM.
                RESULT result = RuntimeManager.CoreSystem.createSound(
                    pcm,
                    MODE.OPENMEMORY | MODE.OPENRAW | MODE.CREATESAMPLE | MODE.LOOP_OFF | MODE._2D,
                    ref exinfo,
                    out _voiceSound);

                if (result != RESULT.OK)
                {
                    Debug.LogWarning($"[AudioManager] createSound failed for '{clip.name}': {result}");
                    _voiceSound.clearHandle();
                    return;
                }

                RuntimeManager.CoreSystem.getMasterChannelGroup(out var master);
                result = RuntimeManager.CoreSystem.playSound(_voiceSound, master, false, out _voiceChannel);
                if (result != RESULT.OK)
                {
                    Debug.LogWarning($"[AudioManager] playSound failed for '{clip.name}': {result}");
                    ReleaseCurrentVoice();
                    return;
                }

                _voiceChannel.setVolume(voiceoverVolume);
                _voiceHeardPlaying = false;
                _voiceDeadline = Time.unscaledTime + Mathf.Max(0.25f, clip.length + 0.5f);
            }
            catch (Exception ex)
            {
                ReleaseCurrentVoice();
                Debug.LogWarning($"[AudioManager] Voiceover failed: {ex.Message}");
            }
        }

        public void StopVoiceover()
        {
            _voiceQueue.Clear();
            ReleaseCurrentVoice();
        }

        void ReleaseCurrentVoice()
        {
            if (_voiceChannel.hasHandle())
            {
                _voiceChannel.stop();
                _voiceChannel.clearHandle();
            }

            if (_voiceSound.hasHandle())
            {
                _voiceSound.release();
                _voiceSound.clearHandle();
            }

            _voiceHeardPlaying = false;
            _voiceDeadline = 0f;
        }

        void EnsureVoiceLibrary()
        {
            if (voiceLibrary != null)
            {
                voiceLibrary.RebuildMaps();
                return;
            }

            voiceLibrary = Resources.Load<PaintingVoiceoverLibrary>("PaintingVoiceoverLibrary");
            if (voiceLibrary == null)
            {
                var all = Resources.FindObjectsOfTypeAll<PaintingVoiceoverLibrary>();
                if (all != null && all.Length > 0)
                    voiceLibrary = all[0];
            }

            if (voiceLibrary == null)
            {
                Debug.LogWarning(
                    "[AudioManager] PaintingVoiceoverLibrary not found under Resources. " +
                    "Run Master Bidder → Painting Voiceovers → Rebuild Library Only.");
                return;
            }

            voiceLibrary.RebuildMaps();
            Debug.Log($"[AudioManager] Voice library loaded ({voiceLibrary.genres.Count} genres, {voiceLibrary.titles.Count} titles).");
        }

        void PlayNextQueuedVoice()
        {
            if (_voiceQueue.Count == 0) return;
            if (Time.unscaledTime < _nextVoiceAt) return;
            var clip = _voiceQueue.Dequeue();
            PlayVoiceoverClip(clip);
        }

        void Update()
        {
            if (_voiceChannel.hasHandle())
            {
                _voiceChannel.isPlaying(out bool playing);
                if (playing)
                {
                    _voiceHeardPlaying = true;
                    return;
                }

                // FMOD may report not-playing for a frame or two before the mixer starts the channel.
                if (!_voiceHeardPlaying && Time.unscaledTime < _voiceDeadline)
                    return;

                ReleaseCurrentVoice();
                _nextVoiceAt = Time.unscaledTime + voiceoverGapSeconds;
            }

            if (_voiceQueue.Count > 0 && Time.unscaledTime >= _nextVoiceAt && !_voiceChannel.hasHandle())
                PlayNextQueuedVoice();
        }
    }
}
