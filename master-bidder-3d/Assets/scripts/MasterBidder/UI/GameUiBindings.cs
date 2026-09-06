using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Wired references on the GameUI prefab root. Edit layout, fonts, spacing, and
    /// colors on the prefab in the Editor; GameUiShell reads these bindings and only
    /// updates dynamic text, sprites, and state-driven tints at runtime.
    /// </summary>
    public class GameUiBindings : MonoBehaviour
    {
        public Canvas canvas;

        [Header("Screens")]
        public GameObject intro;
        public GameObject brief;
        public GameObject auction;
        public GameObject report;
        public GameObject end;

        [Header("Overlays")]
        public GameObject collectorPopup;
        public GameObject purchaseCard;
        public GameObject tutorial;

        [Header("Chrome")]
        public TextMeshProUGUI chromeTitle;
        public TMP_Dropdown langDropdown;

        [Header("Active Client")]
        public GameObject activeClient;
        public Image activePortrait;
        public TextMeshProUGUI activeName;
        public TextMeshProUGUI activeTags;

        [Header("Intro")]
        public TextMeshProUGUI introTitle;
        public TextMeshProUGUI introSubtitle;
        public TextMeshProUGUI introLede;
        public TextMeshProUGUI introRules;
        public Button btnContinue;
        public Button btnStart;
        public TextMeshProUGUI continueLabel;
        public TextMeshProUGUI startLabel;

        [Header("Brief")]
        public TextMeshProUGUI briefDay;
        public TextMeshProUGUI briefCapital;
        public TextMeshProUGUI briefClientHeading;
        public TextMeshProUGUI briefWorkshopHeading;
        public Transform briefEffectsHost;
        public Transform collectorList;
        public Transform upgradeList;
        public Button btnBriefPanelToggle;
        public TextMeshProUGUI briefPanelToggleLabel;
        public Button btnEnterHall;
        public Button btnReset;
        public TextMeshProUGUI enterLabel;
        public TextMeshProUGUI resetLabel;

        [Header("Auction")]
        public TextMeshProUGUI aucHud;
        public Transform effectsUpgrades;
        public Transform effectsBoosters;
        public GameObject effectTooltip;
        public TextMeshProUGUI effectTooltipTitle;
        public TextMeshProUGUI effectTooltipBody;
        public TextMeshProUGUI orderCard;
        public TextMeshProUGUI livePrice;
        public TextMeshProUGUI liveBudget;
        public TextMeshProUGUI liveSpeed;
        public TextMeshProUGUI[] fieldLabels = new TextMeshProUGUI[5];
        public TextMeshProUGUI[] fieldValues = new TextMeshProUGUI[5];
        public Image[] fieldRows = new Image[5];
        public TextMeshProUGUI resultBanner;
        public TextMeshProUGUI fundsHint;
        public TextMeshProUGUI familiarBadge;
        public Button btnStartLot;
        public Button btnBuy;
        public Button btnSkip;
        public Button btnFinishDay;
        public TextMeshProUGUI startLotLabel;
        public TextMeshProUGUI buyLabel;
        public TextMeshProUGUI skipLabel;
        public TextMeshProUGUI finishLabel;
        public Transform audienceRow;
        public Image[] rivalHeads = new Image[15];

        [Header("Collector popup")]
        public TextMeshProUGUI popupName;
        public TextMeshProUGUI popupTagline;
        public TextMeshProUGUI popupSpeech;
        public TextMeshProUGUI popupTags;
        public TextMeshProUGUI popupWarning;
        public Image popupPortrait;
        public Button btnPopupStart;
        public TextMeshProUGUI popupStartLabel;

        [Header("Purchase card")]
        public TextMeshProUGUI pcTitle;
        public TextMeshProUGUI pcArtist;
        public TextMeshProUGUI pcMeta;
        public TextMeshProUGUI pcFact;
        public Button btnPcContinue;
        public TextMeshProUGUI pcContinueLabel;

        [Header("Tutorial")]
        public TextMeshProUGUI tutorialText;

        [Header("Report")]
        public TextMeshProUGUI reportTitle;
        public TextMeshProUGUI reportBody;
        public TextMeshProUGUI boosterHeading;
        public Transform boosterList;
        public Transform purchaseTagList;
        public GameObject reportStamp;
        public TextMeshProUGUI reportStampLabel;
        public TextMeshProUGUI reportStampDetail;
        public Button btnReportPanelToggle;
        public TextMeshProUGUI reportPanelToggleLabel;
        public Button btnReportContinue;
        public TextMeshProUGUI reportContinueLabel;

        [Header("End")]
        public TextMeshProUGUI endTitle;
        public Button btnRestart;
        public TextMeshProUGUI restartLabel;
    }
}
