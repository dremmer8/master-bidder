using System.Collections.Generic;
using MasterBidder.Campaign;
using MasterBidder.Content;
using MasterBidder.Core;
using MasterBidder.Flow;
using MasterBidder.Services;
using UnityEngine;
using UnityEngine.UI;

namespace MasterBidder.UI
{
    /// <summary>
    /// Runtime UI controller. Instantiates editable prefabs from Assets/content/ui/
    /// (generated via Master Bidder → Generate UI Prefabs) and binds game actions.
    /// </summary>
    public class GameUiShell : MonoBehaviour
    {
        [SerializeField] GameObject gameUiPrefab;
        [SerializeField] GameObject collectorCardPrefab;
        [SerializeField] GameObject upgradeRowPrefab;
        [SerializeField] GameObject boosterRowPrefab;

        AppFlow _flow;
        GameUiBindings _b;
        Canvas _canvas;

        GameObject _intro, _brief, _auction, _report, _end;
        GameObject _collectorPopup, _purchaseCard, _tutorial;

        readonly List<GameObject> _collectorCards = new List<GameObject>();
        readonly List<GameObject> _upgradeRows = new List<GameObject>();
        readonly List<GameObject> _boosterRows = new List<GameObject>();

        float _fundsFlashUntil;
        bool _purchaseCardVisible;
        int _lastRaisedRival = -1;
        float _rivalRaiseUntil;

        static readonly string[] FieldIds = { "genre", "period", "artist", "fact", "title" };

        public bool IsPurchaseCardVisible => _purchaseCardVisible;
        public bool IsCollectorPopupVisible => _collectorPopup != null && _collectorPopup.activeSelf;

        public void Bind(AppFlow flow) => _flow = flow;

        public void BuildIfNeeded()
        {
            if (_canvas != null) return;

            ResolvePrefabsIfNeeded();
            EnsureEventSystem();

            GameObject instance;
            if (gameUiPrefab != null)
            {
                instance = Instantiate(gameUiPrefab, transform);
                instance.name = "GameUI";
            }
            else
            {
                Debug.LogWarning(
                    "[GameUiShell] GameUI prefab missing — building default hierarchy. " +
                    "Run Master Bidder → Generate UI Prefabs to edit UI in the Inspector.");
                instance = GameUiHierarchyFactory.BuildGameUi();
                instance.transform.SetParent(transform, false);
            }

            _b = instance.GetComponent<GameUiBindings>();
            if (_b == null)
            {
                Debug.LogError("[GameUiShell] GameUI root is missing GameUiBindings.");
                Destroy(instance);
                return;
            }

            _canvas = _b.canvas != null ? _b.canvas : instance.GetComponent<Canvas>();
            ApplyBindings(_b);
            WireListeners();
        }

        void ResolvePrefabsIfNeeded()
        {
#if UNITY_EDITOR
            const string ui = "Assets/content/ui";
            if (gameUiPrefab == null)
                gameUiPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/GameUI.prefab");
            if (collectorCardPrefab == null)
                collectorCardPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/CollectorCard.prefab");
            if (upgradeRowPrefab == null)
                upgradeRowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/UpgradeRow.prefab");
            if (boosterRowPrefab == null)
                boosterRowPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ui + "/widgets/BoosterRow.prefab");
#endif
        }

        void ApplyBindings(GameUiBindings b)
        {
            _intro = b.intro;
            _brief = b.brief;
            _auction = b.auction;
            _report = b.report;
            _end = b.end;
            _collectorPopup = b.collectorPopup;
            _purchaseCard = b.purchaseCard;
            _tutorial = b.tutorial;

            if (_collectorPopup != null) _collectorPopup.SetActive(false);
            if (_purchaseCard != null) _purchaseCard.SetActive(false);
            if (_tutorial != null) _tutorial.SetActive(false);
        }

        void WireListeners()
        {
            if (_b.btnContinue != null)
                _b.btnContinue.onClick.AddListener(() => _flow?.OnContinueCareer());
            if (_b.btnStart != null)
                _b.btnStart.onClick.AddListener(() => _flow?.OnStartCareer());
            if (_b.btnReset != null)
                _b.btnReset.onClick.AddListener(() => _flow?.OnResetProgress());
            if (_b.btnEnterHall != null)
                _b.btnEnterHall.onClick.AddListener(() => _flow?.OnEnterHall());
            if (_b.btnStartLot != null)
                _b.btnStartLot.onClick.AddListener(() => _flow?.OnStartLot());
            if (_b.btnBuy != null)
                _b.btnBuy.onClick.AddListener(() => _flow?.OnBuy());
            if (_b.btnSkip != null)
                _b.btnSkip.onClick.AddListener(() => _flow?.OnSkip());
            if (_b.btnFinishDay != null)
                _b.btnFinishDay.onClick.AddListener(() => _flow?.OnFinishDay());
            if (_b.btnPopupStart != null)
                _b.btnPopupStart.onClick.AddListener(() => _flow?.OnCollectorPopupStart());
            if (_b.btnPcContinue != null)
                _b.btnPcContinue.onClick.AddListener(() => _flow?.OnPurchaseCardDismiss());
            if (_b.btnReportContinue != null)
                _b.btnReportContinue.onClick.AddListener(() => _flow?.OnReportContinue());
            if (_b.btnRestart != null)
                _b.btnRestart.onClick.AddListener(() => _flow?.OnRestart());

            if (_b.langDropdown != null)
            {
                _b.langDropdown.value = LocaleService.Language == "en" ? 1 : 0;
                _b.langDropdown.onValueChanged.AddListener(i => _flow?.OnSetLanguage(i == 1 ? "en" : "ru"));
            }
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        public void ShowScreen(GameScreen screen)
        {
            if (_intro) _intro.SetActive(screen == GameScreen.Intro);
            if (_brief) _brief.SetActive(screen == GameScreen.Brief);
            if (_auction) _auction.SetActive(screen == GameScreen.Auction);
            if (_report) _report.SetActive(screen == GameScreen.Report);
            if (_end) _end.SetActive(screen == GameScreen.End);
            if (screen != GameScreen.Auction)
            {
                HideCollectorPopup();
                HidePurchaseCard();
                if (_tutorial) _tutorial.SetActive(false);
            }
        }

        public void ShowCollectorPopup(DayOrder order, CollectorData collector = null)
        {
            if (_collectorPopup == null || order == null || _b == null) return;
            _b.popupName.text = order.NameRu;
            _b.popupTagline.text = order.TaglineRu ?? "";
            _b.popupSpeech.text = LocaleService.T("collectorPopup.speech");
            _b.popupTags.text = order.CriteriaLabel;
            _b.popupWarning.text = LocaleService.T("collectorPopup.warning");
            _b.popupStartLabel.text = LocaleService.T("collectorPopup.start");
            if (_b.popupPortrait != null)
            {
                if (collector?.portrait != null)
                {
                    _b.popupPortrait.sprite = collector.portrait;
                    _b.popupPortrait.color = Color.white;
                }
                else
                {
                    _b.popupPortrait.sprite = null;
                    _b.popupPortrait.color = GameUiStyle.PanelLight;
                }
            }
            _collectorPopup.SetActive(true);
        }

        public void RaiseRandomRival()
        {
            if (_b?.rivalHeads == null || _b.rivalHeads.Length == 0) return;
            ResetRivalHeads();
            int idx = Random.Range(0, _b.rivalHeads.Length);
            _lastRaisedRival = idx;
            _rivalRaiseUntil = Time.unscaledTime + 1.6f;
            if (_b.rivalHeads[idx] != null)
                _b.rivalHeads[idx].color = GameUiStyle.Accent;
        }

        void ResetRivalHeads()
        {
            if (_b?.rivalHeads == null) return;
            for (int i = 0; i < _b.rivalHeads.Length; i++)
            {
                if (_b.rivalHeads[i] != null)
                    _b.rivalHeads[i].color = GameUiStyle.RivalIdle;
            }
            _lastRaisedRival = -1;
        }

        public void HideCollectorPopup()
        {
            if (_collectorPopup != null) _collectorPopup.SetActive(false);
        }

        public void ShowPurchaseCard(PresentedLot lot, int price)
        {
            if (_purchaseCard == null || lot == null || _b == null) return;
            _b.pcTitle.text = lot.TitleRu;
            _b.pcArtist.text = lot.ArtistRu;
            _b.pcMeta.text =
                $"{LocaleService.T("auction.field.period")}: {lot.PeriodRu}\n" +
                $"{LocaleService.T("auction.field.genre")}: {lot.GenreRu}\n" +
                $"{LocaleService.T("rarity." + GameCatalog.RarityToString(lot.Rarity))}\n" +
                $"{price:N0} ₽";
            _b.pcFact.text = lot.FactRu;
            _b.pcContinueLabel.text = LocaleService.T("purchase.continue");
            _purchaseCard.SetActive(true);
            _purchaseCardVisible = true;
        }

        public void HidePurchaseCard()
        {
            if (_purchaseCard != null) _purchaseCard.SetActive(false);
            _purchaseCardVisible = false;
        }

        public void FlashInsufficientFunds()
        {
            _fundsFlashUntil = Time.unscaledTime + 1.4f;
            if (_b?.fundsHint != null)
            {
                _b.fundsHint.text = LocaleService.T("auction.insufficient");
                _b.fundsHint.gameObject.SetActive(true);
            }
        }

        public void Refresh(GameSession session)
        {
            if (_b == null) return;
            if (_b.chromeTitle) _b.chromeTitle.text = LocaleService.T("chrome.title");
            RefreshIntro(session);
            RefreshBrief(session);
            RefreshAuction(session);
            RefreshReport(session);
            RefreshEnd(session);
        }

        void RefreshIntro(GameSession session)
        {
            if (_b?.introTitle == null) return;
            _b.introTitle.text = LocaleService.T("intro.title");
            _b.introSubtitle.text = LocaleService.T("intro.subtitle");
            _b.introLede.text = LocaleService.T("intro.lede");
            _b.introRules.text = "• " + LocaleService.T("intro.rule1") + "\n• " + LocaleService.T("intro.rule2")
                               + "\n• " + LocaleService.T("intro.rule3") + "\n• " + LocaleService.T("intro.rule4");
            bool hasSave = SaveService.HasSave();
            _b.btnContinue.gameObject.SetActive(hasSave);
            _b.continueLabel.text = LocaleService.T("intro.continue");
            _b.startLabel.text = hasSave ? LocaleService.T("intro.newCareer") : LocaleService.T("intro.start");
        }

        void RefreshBrief(GameSession session)
        {
            if (_b?.briefDay == null) return;
            var state = session?.State;
            int day = state?.Day ?? 1;
            int capital = state?.Capital ?? CampaignConfig.StartingCapital;
            _b.briefDay.text = $"{LocaleService.T("brief.day")} {day} / {CampaignConfig.CampaignLength}";
            _b.briefCapital.text = $"{LocaleService.T("brief.capital")} {capital:N0} ₽";
            _b.briefClientHeading.text = LocaleService.T("brief.clientHeading");
            _b.briefWorkshopHeading.text = LocaleService.T("brief.workshop");
            _b.enterLabel.text = LocaleService.T("brief.enterHall");
            _b.resetLabel.text = LocaleService.T("brief.resetProgress");

            if (state?.PendingOrder != null)
            {
                var o = state.PendingOrder;
                var venue = CampaignConfig.GetVenue(state.PendingVenue);
                _b.briefOrderPreview.text =
                    $"{LocaleService.T("brief.orderPreview")}: {o.CriteriaLabel}\n" +
                    $"{venue.LabelRu} · {state.Lots.Count} {LocaleService.T("brief.lots")} · {o.Budget:N0} ₽";
            }
            else _b.briefOrderPreview.text = "";

            RebuildCollectorCards(session);
            RebuildUpgradeRows(session);
        }

        void RebuildCollectorCards(GameSession session)
        {
            ClearList(_collectorCards, _b.collectorList);
            var catalog = _flow?.Catalog;
            if (catalog?.collectors == null || session?.State == null) return;
            string selected = session.State.SelectedBranchId;
            foreach (var c in catalog.collectors)
            {
                if (c == null) continue;
                int progress = session.State.BranchProgress.TryGetValue(c.collectorId, out int p) ? p : 0;
                bool isSelected = c.collectorId == selected;
                var go = collectorCardPrefab != null
                    ? Instantiate(collectorCardPrefab, _b.collectorList)
                    : GameUiHierarchyFactory.BuildCollectorCard();
                if (collectorCardPrefab == null)
                    go.transform.SetParent(_b.collectorList, false);
                go.name = "C_" + c.collectorId;
                var view = go.GetComponent<CollectorCardView>();
                if (view == null) continue;

                if (view.background != null)
                    view.background.color = isSelected
                        ? new Color(GameUiStyle.Accent.r, GameUiStyle.Accent.g, GameUiStyle.Accent.b, 0.35f)
                        : GameUiStyle.PanelLight;

                if (view.portraitRoot != null)
                    view.portraitRoot.SetActive(c.portrait != null);
                if (c.portrait != null && view.portrait != null)
                {
                    view.portrait.sprite = c.portrait;
                    view.portrait.color = Color.white;
                    view.portrait.preserveAspect = true;
                }

                if (view.label != null)
                {
                    view.label.text = $"{c.nameRu}\n{LocaleService.T("brief.mission")} {progress + 1}/{c.LadderLength}";
                    view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                }

                if (view.button != null)
                {
                    view.button.onClick.RemoveAllListeners();
                    string id = c.collectorId;
                    view.button.onClick.AddListener(() => _flow?.SelectBranch(id));
                }

                _collectorCards.Add(go);
            }
        }

        void RebuildUpgradeRows(GameSession session)
        {
            ClearList(_upgradeRows, _b.upgradeList);
            if (session?.State == null) return;
            foreach (var u in CampaignConfig.MetaUpgrades)
            {
                bool owned = session.State.Upgrades.Contains(u.Id);
                bool canBuy = !owned && session.State.Capital >= u.Cost;
                var go = upgradeRowPrefab != null
                    ? Instantiate(upgradeRowPrefab, _b.upgradeList)
                    : GameUiHierarchyFactory.BuildUpgradeRow();
                if (upgradeRowPrefab == null)
                    go.transform.SetParent(_b.upgradeList, false);
                go.name = "U_" + u.Id;
                var view = go.GetComponent<UpgradeRowView>();
                if (view == null) continue;

                if (view.label != null)
                {
                    view.label.text = $"{u.NameRu} — {u.Cost:N0} ₽\n{u.DescRu}";
                    view.label.color = owned ? GameUiStyle.Dim : GameUiStyle.TextColor;
                    view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                }

                if (view.buyLabel != null)
                    view.buyLabel.text = owned ? LocaleService.T("brief.owned") : LocaleService.T("brief.buy");
                if (view.buyButton != null)
                {
                    view.buyButton.interactable = canBuy;
                    view.buyButton.GetComponent<Image>().color = canBuy ? GameUiStyle.Good : GameUiStyle.Dim;
                    view.buyButton.onClick.RemoveAllListeners();
                    string uid = u.Id;
                    view.buyButton.onClick.AddListener(() => _flow?.BuyUpgrade(uid));
                }

                _upgradeRows.Add(go);
            }
        }

        void RefreshAuction(GameSession session)
        {
            if (_b?.aucHud == null) return;
            var state = session?.State;
            if (state == null) return;

            var venue = CampaignConfig.GetVenue(state.CurrentVenue);
            _b.aucHud.text =
                $"{LocaleService.T("auction.day")} {state.Day} · {LocaleService.T("auction.venue")} {venue.LabelRu} · " +
                $"{LocaleService.T("auction.lot")} {state.CurrentLotIndex + 1}/{state.Lots.Count}";

            var order = state.DayOrders.Count > 0 ? state.DayOrders[0] : state.PendingOrder;
            _b.orderCard.text = order != null
                ? $"{order.NameRu}\n{order.CriteriaLabel}\n{LocaleService.T("auction.clientBudget")} {order.Budget:N0} ₽"
                : "";

            var lot = state.CurrentLot;
            int price = lot != null
                ? AuctionRules.ComputeLivePrice(lot, state.RevealStep, AuctionRules.GetPriceStepPct(state))
                : 0;
            float speed = AuctionRules.ComputeSpeedMultiplier(state.RevealStep, AuctionRules.GetSpeedFloor(state));
            _b.livePrice.text = $"{LocaleService.T("auction.currentPrice")}: {price:N0} ₽";
            _b.liveBudget.text = $"{LocaleService.T("auction.budgetLeft")}: {state.ClientBudgetRemaining:N0} ₽";
            _b.liveSpeed.text = $"{LocaleService.T("auction.speed")}{speed:0.00}";

            RefreshRevealFields(state, lot, order);
            _b.familiarBadge.gameObject.SetActive(lot != null && lot.Familiar);
            _b.familiarBadge.text = LocaleService.T("auction.familiar");

            bool showBanner = !string.IsNullOrEmpty(state.LastLotResult);
            _b.resultBanner.gameObject.SetActive(showBanner);
            if (showBanner)
            {
                if (state.LastLotResult == "won")
                {
                    _b.resultBanner.text = LocaleService.T("auction.won");
                    _b.resultBanner.color = GameUiStyle.Good;
                }
                else if (state.LastLotResult == "lost")
                {
                    _b.resultBanner.text = LocaleService.T("auction.lost");
                    _b.resultBanner.color = GameUiStyle.Bad;
                }
                else
                {
                    _b.resultBanner.text = LocaleService.T("auction.skip");
                    _b.resultBanner.color = GameUiStyle.Dim;
                }
            }

            if (_b.fundsHint != null && Time.unscaledTime > _fundsFlashUntil)
                _b.fundsHint.gameObject.SetActive(false);

            bool standby = state.AwaitingLotStart || IsCollectorPopupVisible;
            bool busy = state.LotResolved || state.FastForwarding || _purchaseCardVisible;
            _b.btnStartLot.gameObject.SetActive(state.AwaitingLotStart && !IsCollectorPopupVisible);
            _b.btnSkip.gameObject.SetActive(!state.AwaitingLotStart);
            _b.btnFinishDay.gameObject.SetActive(!state.AwaitingLotStart);
            _b.btnBuy.gameObject.SetActive(!state.AwaitingLotStart);
            _b.btnBuy.interactable = !standby && !busy && (!state.TutorialPaused || state.TutorialStep == TutorialStep.BuyMatch);
            _b.btnSkip.interactable = !standby && !busy && (!state.TutorialPaused || state.TutorialStep == TutorialStep.SkipMiss);
            _b.btnFinishDay.interactable = !state.TutorialPaused && !busy;

            _b.startLotLabel.text = LocaleService.T("auction.startLot");
            _b.buyLabel.text = LocaleService.T("auction.buy");
            _b.skipLabel.text = LocaleService.T("auction.skip");
            _b.finishLabel.text = LocaleService.T("auction.finishDay");

            bool showTut = state.TutorialPaused && state.TutorialStep != TutorialStep.None;
            if (_tutorial != null)
            {
                _tutorial.SetActive(showTut && _auction.activeSelf);
                if (showTut)
                {
                    _b.tutorialText.text = state.TutorialStep == TutorialStep.BuyMatch
                        ? LocaleService.T("tutorial.buyMatch")
                        : LocaleService.T("tutorial.skipMiss");
                }
            }
        }

        void RefreshRevealFields(GameState state, PresentedLot lot, DayOrder order)
        {
            for (int i = 0; i < 5; i++)
            {
                string id = FieldIds[i];
                _b.fieldLabels[i].text = LocaleService.T("auction.field." + id);
                string raw = lot == null ? "—" : FieldValue(lot, id);
                bool revealed = state.RevealStep > i
                                || (!string.IsNullOrEmpty(state.FreeRevealedField) && state.FreeRevealedField == id);
                _b.fieldValues[i].text = revealed ? raw : AuctionRules.MaskValue(raw);
                _b.fieldValues[i].color = revealed ? GameUiStyle.TextColor : GameUiStyle.Dim;

                bool isTarget = order != null && IsOrderTarget(order, id);
                _b.fieldRows[i].color = isTarget
                    ? new Color(GameUiStyle.Accent.r, GameUiStyle.Accent.g, GameUiStyle.Accent.b, 0.22f)
                    : new Color(0, 0, 0, 0.15f);
            }
        }

        static string FieldValue(PresentedLot lot, string id)
        {
            switch (id)
            {
                case "genre": return lot.GenreRu;
                case "period": return lot.PeriodRu;
                case "artist": return lot.ArtistRu;
                case "fact": return lot.FactRu;
                case "title": return lot.TitleRu;
                default: return "—";
            }
        }

        static bool IsOrderTarget(DayOrder order, string fieldId)
        {
            if (order?.CriteriaTags == null) return false;
            for (int i = 0; i < order.CriteriaTags.Length; i++)
            {
                var t = order.CriteriaTags[i];
                if (t.Type == CriterionType.Artwork && fieldId == "title") return true;
                if (t.Type == CriterionType.Genre && fieldId == "genre") return true;
                if (t.Type == CriterionType.Period && fieldId == "period") return true;
                if (t.Type == CriterionType.Artist && fieldId == "artist") return true;
            }
            return false;
        }

        void RefreshReport(GameSession session)
        {
            if (_b?.reportTitle == null) return;
            var state = session?.State;
            var r = state?.PendingResult;
            _b.reportTitle.text = $"{LocaleService.T("report.title")} {state?.Day ?? 0}";
            if (r == null)
            {
                _b.reportBody.text = "";
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(r.OrdersFulfilled
                ? LocaleService.T("report.fulfilled")
                : LocaleService.T("report.unfulfilled"));
            if (r.SavedByCreditLine) sb.AppendLine(LocaleService.T("report.creditLine"));
            sb.AppendLine();
            sb.AppendLine($"{LocaleService.T("report.commission")} {r.TotalCommission:N0} ₽");
            sb.AppendLine($"{LocaleService.T("report.capitalEnd")} {r.ProjectedCapital:N0} ₽");
            sb.AppendLine();
            sb.AppendLine(LocaleService.T("report.ledger"));
            sb.AppendLine($"{LocaleService.T("report.start")}: {r.StartingCapital:N0}");
            sb.AppendLine($"{LocaleService.T("report.net")}: {r.Net:N0}");
            sb.AppendLine($"{LocaleService.T("report.end")}: {r.ProjectedCapital:N0}");
            sb.AppendLine();
            if (r.PurchaseDetails != null)
            {
                for (int i = 0; i < r.PurchaseDetails.Length; i++)
                {
                    var d = r.PurchaseDetails[i];
                    string mark = d.Matched ? LocaleService.T("report.correct") : LocaleService.T("report.incorrect");
                    sb.AppendLine($"• {d.TitleRu} — {mark} ({d.Amount:N0} ₽)");
                }
            }
            _b.reportBody.text = sb.ToString();

            bool showBoosters = r.Pass && state.Day < CampaignConfig.CampaignLength;
            RebuildBoosterRows(session, showBoosters);
            _b.reportContinueLabel.text = r.Pass
                ? LocaleService.T("report.continue")
                : LocaleService.T("report.finish");
        }

        void RebuildBoosterRows(GameSession session, bool show)
        {
            ClearList(_boosterRows, _b.boosterList);
            if (_b.boosterHeading != null)
                _b.boosterHeading.text = show ? LocaleService.T("report.boosters") : "";
            if (!show || session?.State == null) return;
            foreach (var id in session.State.BoosterOffers)
            {
                BoosterDef def = null;
                for (int i = 0; i < CampaignConfig.Boosters.Length; i++)
                {
                    if (CampaignConfig.Boosters[i].Id == id) { def = CampaignConfig.Boosters[i]; break; }
                }
                if (def == null) continue;
                int cost = AuctionRules.GetBoosterCost(def, session.State);
                bool owned = session.State.PendingBoosters.Contains(id);
                bool canBuy = !owned && session.State.Capital >= cost
                              && session.State.PendingBoosters.Count < CampaignConfig.GetMaxDailyBoosters(
                                  session.State.Upgrades.Contains("personal-secretary"));

                var go = boosterRowPrefab != null
                    ? Instantiate(boosterRowPrefab, _b.boosterList)
                    : GameUiHierarchyFactory.BuildBoosterRow();
                if (boosterRowPrefab == null)
                    go.transform.SetParent(_b.boosterList, false);
                go.name = "B_" + id;
                var view = go.GetComponent<BoosterRowView>();
                if (view == null) continue;

                if (view.label != null)
                {
                    view.label.text = $"{def.NameRu} — {cost:N0} ₽\n{def.DescRu}";
                    view.label.horizontalOverflow = HorizontalWrapMode.Wrap;
                }

                if (view.buyLabel != null)
                    view.buyLabel.text = owned ? LocaleService.T("report.ownedBooster") : LocaleService.T("report.buyBooster");
                if (view.buyButton != null)
                {
                    view.buyButton.interactable = canBuy;
                    view.buyButton.GetComponent<Image>().color = canBuy ? GameUiStyle.Good : GameUiStyle.Dim;
                    view.buyButton.onClick.RemoveAllListeners();
                    string bid = id;
                    view.buyButton.onClick.AddListener(() => _flow?.BuyBooster(bid));
                }

                _boosterRows.Add(go);
            }
        }

        void RefreshEnd(GameSession session)
        {
            if (_b?.endTitle == null) return;
            var state = session?.State;
            bool bankrupt = state?.PendingResult != null && !state.PendingResult.Pass;
            _b.endTitle.text = bankrupt ? LocaleService.T("end.bankruptTitle") : LocaleService.T("end.careerTitle");
            _b.restartLabel.text = LocaleService.T("end.restart");
        }

        void Update()
        {
            if (_flow == null) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                if (_purchaseCardVisible)
                {
                    _flow.OnPurchaseCardDismiss();
                    return;
                }
                if (IsCollectorPopupVisible)
                {
                    _flow.OnCollectorPopupStart();
                    return;
                }
                if (_auction != null && _auction.activeSelf)
                {
                    var state = _flow.Session?.State;
                    if (state == null) return;
                    if (state.AwaitingLotStart) _flow.OnStartLot();
                    else _flow.OnBuy();
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_purchaseCardVisible) _flow.OnPurchaseCardDismiss();
            }

            if (_lastRaisedRival >= 0 && Time.unscaledTime > _rivalRaiseUntil)
                ResetRivalHeads();
        }

        static void ClearList(List<GameObject> list, Transform parent)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null) Destroy(list[i]);
            }
            list.Clear();
        }
    }
}
