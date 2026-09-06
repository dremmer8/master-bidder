using MasterBidder.Services;
using TMPro;
using UnityEngine;

namespace MasterBidder.UI
{
    /// <summary>
    /// Fills GameUI / widget text with Russian sample copy so prefabs are editable
    /// in the Inspector with real content visible. Runtime LocaleService overwrites these.
    /// </summary>
    public static class GameUiSampleContent
    {
        public static void Apply(GameUiBindings b)
        {
            if (b == null) return;

            Set(b.chromeTitle, LocaleService.T("chrome.title"));

            Set(b.introTitle, LocaleService.T("intro.title"));
            Set(b.introSubtitle, LocaleService.T("intro.subtitle"));
            Set(b.introLede, LocaleService.T("intro.lede"));
            Set(b.introRules,
                "• " + LocaleService.T("intro.rule1") + "\n• " + LocaleService.T("intro.rule2") +
                "\n• " + LocaleService.T("intro.rule3") + "\n• " + LocaleService.T("intro.rule4"));
            Set(b.continueLabel, LocaleService.T("intro.continue"));
            Set(b.startLabel, LocaleService.T("intro.start"));

            Set(b.briefDay,
                $"{LocaleService.T("brief.day")} 1 / 15   ·   {LocaleService.T("brief.capital")} 40 000 ₽");
            Set(b.activeName, "Барон Аркадий Светозаров");
            Set(b.activeTags, "Портрет  ·  Высокое Возрождение");
            Set(b.briefClientHeading, LocaleService.T("brief.ordersHeading"));
            Set(b.briefWorkshopHeading, LocaleService.T("brief.workshop"));
            Set(b.briefPanelToggleLabel, LocaleService.T("brief.toUpgrades"));
            Set(b.enterLabel, LocaleService.T("brief.enterHall"));
            Set(b.resetLabel, LocaleService.T("brief.resetProgress"));

            Set(b.aucHud,
                $"{LocaleService.T("auction.day")} 1 · {LocaleService.T("auction.venue")} Обычный аукцион · " +
                $"{LocaleService.T("auction.lot")} 1/10");
            Set(b.livePrice, "48 000 ₽");
            Set(b.liveBudget, $"{LocaleService.T("auction.budgetLeft")}: 332 000 ₽");
            Set(b.resultBanner, LocaleService.T("auction.won"));
            Set(b.fundsHint, LocaleService.T("auction.insufficient"));
            Set(b.familiarBadge, LocaleService.T("auction.familiar"));
            Set(b.startLotLabel, LocaleService.T("auction.startLot"));
            Set(b.buyLabel, LocaleService.T("auction.buy"));
            Set(b.skipLabel, LocaleService.T("auction.skip"));
            Set(b.finishLabel, LocaleService.T("auction.finishDay"));

            string[] fieldIds = { "genre", "period", "artist", "fact", "title" };
            string[] fieldSamples =
            {
                "Портрет",
                "Высокое Возрождение",
                "Рафаэль",
                "Работа написана для римского заказчика и долгое время считалась утраченной.",
                "Портрет молодого человека"
            };
            if (b.fieldLabels != null && b.fieldValues != null)
            {
                for (int i = 0; i < b.fieldLabels.Length && i < fieldIds.Length; i++)
                {
                    Set(b.fieldLabels[i], LocaleService.T("auction.field." + fieldIds[i]));
                    Set(b.fieldValues[i], fieldSamples[i]);
                }
            }

            Set(b.popupName, "Барон Аркадий Светозаров");
            Set(b.popupTagline, "Коллекционер портретов эпохи Возрождения");
            Set(b.popupSpeech, LocaleService.T("collectorPopup.speech"));
            Set(b.popupTags, "Портрет  ·  Высокое Возрождение");
            Set(b.popupWarning, LocaleService.T("collectorPopup.warning"));
            Set(b.popupStartLabel, LocaleService.T("collectorPopup.start"));

            Set(b.pcTitle, "Портрет молодого человека");
            Set(b.pcArtist, "Рафаэль");
            Set(b.pcMeta,
                $"{LocaleService.T("auction.field.period")}: Высокое Возрождение\n" +
                $"{LocaleService.T("auction.field.genre")}: Портрет\n" +
                $"{LocaleService.T("rarity.rare")}\n" +
                "48 000 ₽");
            Set(b.pcFact, "Работа написана для римского заказчика и долгое время считалась утраченной.");
            Set(b.pcContinueLabel, LocaleService.T("purchase.continue"));

            Set(b.tutorialText, LocaleService.T("tutorial.buyMatch"));

            Set(b.reportStampLabel, LocaleService.T("report.stampOk"));
            Set(b.reportStampDetail,
                LocaleService.T("report.fulfilled") + "\n" +
                $"{LocaleService.T("report.commission")} 12 400 ₽");
            Set(b.reportTitle, LocaleService.T("report.tagsHeading"));
            Set(b.boosterHeading, LocaleService.T("report.boosters"));
            Set(b.reportPanelToggleLabel, LocaleService.T("report.toBoosters"));
            Set(b.reportContinueLabel, LocaleService.T("report.continue"));

            Set(b.endTitle, LocaleService.T("end.careerTitle"));
            Set(b.restartLabel, LocaleService.T("end.restart"));

            Set(b.effectTooltipTitle, "Эксперт-оценщик");
            Set(b.effectTooltipBody, "Раньше раскрывает сведения о лоте на торгах.");
        }

        public static void ApplyCollectorCard(CollectorCardView view)
        {
            if (view == null) return;
            Set(view.label, "Барон Аркадий Светозаров\nМиссия 1/10");
        }

        public static void ApplyUpgradeRow(UpgradeRowView view)
        {
            if (view == null) return;
            Set(view.label, "Эксперт-оценщик — 12 000 ₽\nРаньше раскрывает сведения о лоте.");
            Set(view.buyLabel, LocaleService.T("brief.buy"));
        }

        public static void ApplyBoosterRow(BoosterRowView view)
        {
            if (view == null) return;
            Set(view.label, "Спокойный зал — 8 000 ₽\nСоперники реагируют медленнее завтра.");
            Set(view.buyLabel, LocaleService.T("report.buyBooster"));
        }

        public static void ApplyPurchaseTag(PurchaseTagView view)
        {
            if (view == null) return;
            Set(view.title, "Портрет молодого человека");
            Set(view.meta, "48 000 ₽  ·  " + LocaleService.T("report.commission") + " 12 400 ₽");
            Set(view.stamp, LocaleService.T("report.correct"));
            if (view.stamp != null) view.stamp.color = GameUiStyle.Good;
        }

        static void Set(TMP_Text text, string value)
        {
            if (text == null) return;
            text.text = value ?? "";
        }
    }
}
