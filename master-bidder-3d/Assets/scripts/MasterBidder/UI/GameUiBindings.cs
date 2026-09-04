using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Wired references on the GameUI prefab root. Edit the prefab in the Editor;
    /// GameUiShell reads these at runtime after Instantiate.
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
        public Text chromeTitle;
        public Dropdown langDropdown;

        [Header("Intro")]
        public Text introTitle;
        public Text introSubtitle;
        public Text introLede;
        public Text introRules;
        public Button btnContinue;
        public Button btnStart;
        public Text continueLabel;
        public Text startLabel;

        [Header("Brief")]
        public Text briefDay;
        public Text briefCapital;
        public Text briefClientHeading;
        public Text briefWorkshopHeading;
        public Text briefOrderPreview;
        public Transform collectorList;
        public Transform upgradeList;
        public Button btnEnterHall;
        public Button btnReset;
        public Text enterLabel;
        public Text resetLabel;

        [Header("Auction")]
        public Text aucHud;
        public Text orderCard;
        public Text livePrice;
        public Text liveBudget;
        public Text liveSpeed;
        public Text[] fieldLabels = new Text[5];
        public Text[] fieldValues = new Text[5];
        public Image[] fieldRows = new Image[5];
        public Text resultBanner;
        public Text fundsHint;
        public Text familiarBadge;
        public Button btnStartLot;
        public Button btnBuy;
        public Button btnSkip;
        public Button btnFinishDay;
        public Text startLotLabel;
        public Text buyLabel;
        public Text skipLabel;
        public Text finishLabel;
        public Transform audienceRow;
        public Image[] rivalHeads = new Image[15];

        [Header("Collector popup")]
        public Text popupName;
        public Text popupTagline;
        public Text popupSpeech;
        public Text popupTags;
        public Text popupWarning;
        public Image popupPortrait;
        public Button btnPopupStart;
        public Text popupStartLabel;

        [Header("Purchase card")]
        public Text pcTitle;
        public Text pcArtist;
        public Text pcMeta;
        public Text pcFact;
        public Button btnPcContinue;
        public Text pcContinueLabel;

        [Header("Tutorial")]
        public Text tutorialText;

        [Header("Report")]
        public Text reportTitle;
        public Text reportBody;
        public Text boosterHeading;
        public Transform boosterList;
        public Button btnReportContinue;
        public Text reportContinueLabel;

        [Header("End")]
        public Text endTitle;
        public Button btnRestart;
        public Text restartLabel;
    }
}
