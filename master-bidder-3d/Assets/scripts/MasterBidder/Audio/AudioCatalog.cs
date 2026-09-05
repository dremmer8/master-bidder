using FMODUnity;
using UnityEngine;

namespace MasterBidder.Audio
{
    /// <summary>
    /// FMOD event bindings for Master Bidder SFX.
    /// Create matching events in FMOD Studio (paths below), build banks, then assign here.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Master Bidder/Audio Catalog", order = 130)]
    public class AudioCatalog : ScriptableObject
    {
        [Header("UI")]
        public EventReference click;
        public EventReference select;
        public EventReference upgrade;
        public EventReference error;
        public EventReference skip;
        public EventReference cardOpen;
        public EventReference cardClose;
        public EventReference zoomOpen;
        public EventReference zoomClose;

        [Header("Auction")]
        public EventReference reveal;
        public EventReference insight;
        public EventReference outcomeWon;
        public EventReference outcomeLost;
        public EventReference rivalRaise;
        public EventReference tension;
        public EventReference clothDown;
        public EventReference nextPainting;

        [Header("Meta")]
        public EventReference dayPass;
        public EventReference dayFail;
        public EventReference campaignEnd;

        [Header("Voiceover")]
        [Tooltip("Optional FMOD event with a programmer instrument. If empty, voiceovers play via FMOD Core from AudioClip.")]
        public EventReference voiceover;

        [Header("Mixer")]
        [Tooltip("Bus path used for mute (default master).")]
        public string masterBusPath = "bus:/";

        // Paths match FMOD Studio MasterEventFolder (root) event names.
        public const string PathClick = "event:/click";
        public const string PathSelect = "event:/ui/select";
        public const string PathUpgrade = "event:/ui/upgrade";
        public const string PathError = "event:/ui/error";
        public const string PathSkip = "event:/skip";
        public const string PathCardOpen = "event:/ui/card_open";
        public const string PathCardClose = "event:/ui/card_close";
        public const string PathZoomOpen = "event:/ui/zoom_open";
        public const string PathZoomClose = "event:/ui/zoom_close";
        public const string PathReveal = "event:/next_fact";
        public const string PathInsight = "event:/next_fact";
        public const string PathOutcomeWon = "event:/buy";
        public const string PathOutcomeLost = "event:/auction/outcome_lost";
        public const string PathRivalRaise = "event:/auction/rival_raise";
        public const string PathTension = "event:/auction/tension";
        public const string PathClothDown = "event:/cloth_down";
        public const string PathNextPainting = "event:/next_painting";
        public const string PathDayPass = "event:/meta/day_pass";
        public const string PathDayFail = "event:/meta/day_fail";
        public const string PathCampaignEnd = "event:/meta/campaign_end";
        public const string PathVoiceover = "event:/voice/painting";
    }
}
