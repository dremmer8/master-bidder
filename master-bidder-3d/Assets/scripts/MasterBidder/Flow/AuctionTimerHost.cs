using System.Collections;
using MasterBidder.Audio;
using MasterBidder.Campaign;
using MasterBidder.Core;
using UnityEngine;

namespace MasterBidder.Flow
{
    /// <summary>
    /// Drives reveal / rival / skip / resolution timers for <see cref="GameSession"/>.
    /// Lot presentation (cloth) is owned by AppFlow — call <see cref="StartLotTimers"/> after cloth-up.
    /// </summary>
    public class AuctionTimerHost : MonoBehaviour
    {
        GameSession _session;
        Coroutine _revealRoutine;
        Coroutine _rivalRoutine;
        Coroutine _resolutionRoutine;
        Coroutine _skipRoutine;

        /// <summary>
        /// Fired with field id after each normal reveal step.
        /// Return voiceover duration in seconds (0 = use fallback interval).
        /// </summary>
        public System.Func<string, float> OnFieldRevealed;

        public void Bind(GameSession session)
        {
            Unbind();
            _session = session;
            if (_session == null) return;
            _session.OnLotTimersClearRequested += ClearAll;
            _session.OnAdvanceLotRequested += OnAdvance;
        }

        public void Unbind()
        {
            if (_session != null)
            {
                _session.OnLotTimersClearRequested -= ClearAll;
                _session.OnAdvanceLotRequested -= OnAdvance;
            }
            ClearAll();
            _session = null;
        }

        void OnDestroy() => Unbind();

        void OnAdvance()
        {
            // Reset lot state; AppFlow will present cloth then call StartLotTimers.
            _session?.PresentLotLogicReset();
        }

        public void ClearAll()
        {
            if (_revealRoutine != null) { StopCoroutine(_revealRoutine); _revealRoutine = null; }
            if (_rivalRoutine != null) { StopCoroutine(_rivalRoutine); _rivalRoutine = null; }
            if (_resolutionRoutine != null) { StopCoroutine(_resolutionRoutine); _resolutionRoutine = null; }
            if (_skipRoutine != null) { StopCoroutine(_skipRoutine); _skipRoutine = null; }
            AudioService.StopTension();
        }

        public void StartLotTimers()
        {
            if (_session?.State == null) return;
            if (_session.State.CurrentLotIndex >= _session.State.Lots.Count) return;
            if (_session.State.LotResolved) return;
            // Skip already owns the lot timers — do not ClearAll / restart reveal.
            if (_session.State.FastForwarding) return;

            ClearAll();
            AudioService.StopVoiceover();

            if (!string.IsNullOrEmpty(_session.State.FreeRevealedField))
                AudioService.PlayInsight();

            AudioService.StartTension();
            AudioService.SetTensionIntensity(0.15f);
            _revealRoutine = StartCoroutine(RevealRoutine());
            ScheduleRival();
        }

        public void StartSkipFastReveal()
        {
            if (_session?.State == null || !_session.State.FastForwarding) return;
            if (_skipRoutine != null) return;
            ClearAll();
            // Do not StopVoiceover here — AppFlow plays the title line right after.
            _skipRoutine = StartCoroutine(SkipRoutine());
        }

        public void ScheduleResolutionThenAdvance(float delaySeconds)
        {
            if (_resolutionRoutine != null) StopCoroutine(_resolutionRoutine);
            _resolutionRoutine = StartCoroutine(ResolutionRoutine(delaySeconds));
        }

        public void CancelResolution()
        {
            if (_resolutionRoutine != null)
            {
                StopCoroutine(_resolutionRoutine);
                _resolutionRoutine = null;
            }
        }

        IEnumerator ResolutionRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            _session?.AdvanceLot();
            _resolutionRoutine = null;
        }

        IEnumerator RevealRoutine()
        {
            var state = _session.State;
            var fields = CampaignConfig.RevealableFields;
            var tutorial = _session.GetDay1TutorialStep(state.CurrentLotIndex);

            yield return new WaitForSeconds(CampaignConfig.RevealLeadInSeconds);

            for (int i = 0; i < fields.Length; i++)
            {
                if (state.LotResolved) yield break;
                _session.SetRevealStep(i + 1);
                AudioService.PlayReveal(i, fast: false);
                float voiceSeconds = OnFieldRevealed != null ? OnFieldRevealed(fields[i]) : 0f;

                float wait = voiceSeconds > 0.05f
                    ? voiceSeconds + CampaignConfig.RevealVoiceTailSeconds
                    : CampaignConfig.RevealIntervalSeconds;

                if (fields[i] == "genre" && tutorial != TutorialStep.None)
                {
                    if (wait > 0f)
                        yield return new WaitForSeconds(wait);
                    _session.PauseForTutorial(tutorial);
                    yield break;
                }

                yield return new WaitForSeconds(wait);
            }
            _revealRoutine = null;
        }

        IEnumerator SkipRoutine()
        {
            var state = _session.State;
            var fields = CampaignConfig.RevealableFields;
            int startStep = state.RevealStep;
            float interval = CampaignConfig.SkipFastRevealIntervalSeconds;

            // Visual fast-forward only — no reveal SFX / field voiceovers (title is spoken from AppFlow).
            for (int i = startStep; i < fields.Length; i++)
            {
                yield return new WaitForSeconds(interval);
                if (state.LotResolved) yield break;
                _session.SetRevealStep(i + 1);
            }

            if (state.ActiveBoosters.Contains("quiet-start") && state.CurrentLotIndex == 0)
            {
                state.FastForwarding = false;
                state.LotResolved = true;
                state.LastLotResult = "skipped";
                _session.NotifyChanged();
                ScheduleResolutionThenAdvance(CampaignConfig.ResolutionPauseSeconds);
                _skipRoutine = null;
                yield break;
            }

            float rivalDelay = CampaignConfig.SkipRivalPauseSeconds;
            if (state.ActiveBoosters.Contains("sleepy-rivals")) rivalDelay *= 1.45f;
            if (state.Upgrades.Contains("calm-hall")) rivalDelay *= 1.15f;
            yield return new WaitForSeconds(rivalDelay);
            if (!state.LotResolved)
                _session.ApplyRivalWin(clearTimers: false);
            if (state.LotResolved)
                ScheduleResolutionThenAdvance(CampaignConfig.ResolutionPauseSeconds);
            _skipRoutine = null;
        }

        void ScheduleRival()
        {
            var state = _session.State;
            var tutorial = _session.GetDay1TutorialStep(state.CurrentLotIndex);
            if (tutorial != TutorialStep.None) return;
            if (state.ActiveBoosters.Contains("quiet-start") && state.CurrentLotIndex == 0) return;

            float delay = AuctionRules.RandRange(
                _session.Rng,
                state.DayConfig.RivalMinSec,
                state.DayConfig.RivalMaxSec);
            delay *= CampaignConfig.GetVenue(state.CurrentVenue).RivalSpeedFactor;
            if (state.ActiveBoosters.Contains("sleepy-rivals")) delay *= 1.45f;
            if (state.Upgrades.Contains("calm-hall")) delay *= 1.15f;

            _rivalRoutine = StartCoroutine(RivalRoutine(delay));
        }

        IEnumerator RivalRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            _session?.ApplyRivalWin();
            if (_session != null && _session.State.LotResolved)
            {
                ScheduleResolutionThenAdvance(CampaignConfig.ResolutionPauseSeconds);
            }
            _rivalRoutine = null;
        }
    }
}
