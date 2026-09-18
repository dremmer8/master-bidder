using System.Collections.Generic;
using UnityEngine;

namespace MasterBidder.Services
{
    /// <summary>
    /// RU/EN UI chrome strings. Content tags (genre/period/artist) stay Russian for matching.
    /// </summary>
    public static class LocaleService
    {
        const string PrefKey = "mb-locale";

        static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["intro.title"] = "Аукционный дом",
            ["intro.subtitle"] = "Симулятор скупщика произведений искусства",
            ["intro.lede"] =
                "Вы — агент по закупкам на арт-аукционе. Коллекционеры дают заказы — опознайте нужный лот и купите быстрее конкурентов.",
            ["intro.rule1"] = "Кто сигнализирует первым — тот и покупает лот. Торга нет.",
            ["intro.rule2"] = "Пока идёт раскрытие сведений, цена растёт, комиссия падает.",
            ["intro.rule3"] = "Правильную покупку вы узнаете только в конце дня.",
            ["intro.rule4"] = "Капитал — это жизнь: закончились деньги — карьера окончена.",
            ["intro.start"] = "Начать карьеру",
            ["intro.continue"] = "Продолжить карьеру",
            ["intro.newCareer"] = "Новая карьера",

            ["brief.day"] = "День",
            ["brief.capital"] = "Капитал:",
            ["brief.clientHeading"] = "Заказчик на сегодня",
            ["brief.ordersHeading"] = "Заказы",
            ["brief.workshop"] = "Улучшения",
            ["brief.enterHall"] = "Начать торги",
            ["brief.toUpgrades"] = "К улучшениям",
            ["brief.toOrders"] = "К заказам",
            ["brief.resetProgress"] = "Обнулить прогресс",
            ["brief.mission"] = "Миссия",
            ["brief.owned"] = "Куплено",
            ["brief.buy"] = "Купить",
            ["brief.lots"] = "лотов",
            ["brief.orderPreview"] = "Заказ",

            ["collectorPopup.speech"] = "Пожалуйста, покупайте для меня только картины со следующими параметрами:",
            ["collectorPopup.warning"] = "Покупка неподходящих картин приведет к штрафу и растрате бюджета.",
            ["collectorPopup.start"] = "Начать торги (Пробел)",

            ["auction.day"] = "День",
            ["auction.venue"] = "Площадка:",
            ["auction.lot"] = "Лот",
            ["auction.clientBudget"] = "Бюджет заказчика:",
            ["auction.currentPrice"] = "Текущая цена",
            ["auction.budgetLeft"] = "Остаток бюджета",
            ["auction.currentLot"] = "Текущий лот",
            ["auction.field.genre"] = "Жанр",
            ["auction.field.period"] = "Стиль",
            ["auction.field.artist"] = "Автор",
            ["auction.field.fact"] = "Интересный факт",
            ["auction.field.title"] = "Название",
            ["auction.startLot"] = "Начать торги (Пробел)",
            ["auction.buy"] = "КУПИТЬ! (Пробел)",
            ["auction.skip"] = "Пропустить",
            ["auction.finishDay"] = "Закончить день",
            ["auction.familiar"] = "Знакомый лот",
            ["auction.insufficient"] = "Недостаточно бюджета заказчика!",
            ["auction.won"] = "Лот ваш!",
            ["auction.lost"] = "Лот ушёл другому покупателю!",
            ["auction.waiting"] = "Конкуренты уже присматриваются…",
            ["auction.speed"] = "Комиссия ×",

            ["tutorial.buyMatch"] = "Этот лот подходит под заказ. Нажмите КУПИТЬ (Пробел), пока соперники не опередили вас.",
            ["tutorial.skipMiss"] = "Этот лот не подходит. Нажмите Пропустить — не тратьте бюджет заказчика.",

            ["purchase.continue"] = "Продолжить",
            ["purchase.collapse"] = "Свернуть",

            ["report.title"] = "Итоги дня",
            ["report.continue"] = "К заказам",
            ["report.finish"] = "Закончить",
            ["report.toBoosters"] = "К бустерам",
            ["report.toTags"] = "К ярлыкам",
            ["report.tagsHeading"] = "Ярлыки покупок",
            ["report.commission"] = "Комиссия:",
            ["report.capitalEnd"] = "Капитал:",
            ["report.fulfilled"] = "Заказ выполнен",
            ["report.unfulfilled"] = "Заказ не выполнен",
            ["report.correct"] = "Подошло",
            ["report.incorrect"] = "Не подошло",
            ["report.stampOk"] = "ПЕЧАТЬ\nЗАКАЗЧИКА",
            ["report.stampBad"] = "ПЕЧАТЬ\nЗАКАЗЧИКА",
            ["report.noPurchases"] = "Покупок за день не было.",
            ["report.boosters"] = "Бустеры на завтра",
            ["report.buyBooster"] = "Купить",
            ["report.ownedBooster"] = "Взято",
            ["report.ledger"] = "Движение капитала",
            ["report.start"] = "Начало дня",
            ["report.net"] = "Комиссии (нетто)",
            ["report.end"] = "Конец дня",
            ["report.creditLine"] = "Кредитная линия спасла карьеру",

            ["end.careerTitle"] = "Карьера завершена",
            ["end.bankruptTitle"] = "Банкротство",
            ["end.restart"] = "Начать заново",

            ["chrome.title"] = "Аукционный дом",
            ["chrome.catalog"] = "Каталог",
            ["chrome.licenses"] = "Лицензии",
            ["catalog.title"] = "Каталог картин",
            ["catalog.search"] = "Поиск…",
            ["catalog.close"] = "✕",
            ["catalog.unknown"] = "?",
            ["catalog.page"] = "Стр. {0} / {1}",
            ["catalog.empty"] = "Ничего не найдено",
            ["licenses.title"] = "Лицензии",
            ["licenses.close"] = "✕",
            ["licenses.regular.owned"] = "Обычный аукцион — получена",
            ["licenses.regular.locked"] = "Обычный аукцион — не получена",
            ["licenses.elite.owned"] = "Элитный аукцион — получена",
            ["licenses.elite.locked"] = "Элитный аукцион — не получена",
            ["licenses.exam.regular"] = "Сдать экзамен ({0} ₽)",
            ["licenses.exam.elite"] = "Сдать экзамен ({0} ₽)",
            ["licenses.hint.needPaintings"] = "Сначала посмотрите картины на аукционе — без них экзамен недоступен.",
            ["licenses.hint.needRegular"] = "Сначала сдайте экзамен на обычную лицензию.",
            ["licenses.hint.needMoney"] = "Недостаточно капитала для попытки.",
            ["licenses.hint.ready"] = "Экзамен: 10 карточек, нужно ≥80%. Закрытие сжигает попытку.",
            ["exam.progress"] = "Карточка {0} / {1}",
            ["exam.next"] = "Далее",
            ["exam.submit"] = "Сдать",
            ["exam.abandon"] = "Прервать",
            ["exam.pass"] = "Экзамен сдан! Верных ответов: {0}/{1}",
            ["exam.fail"] = "Не сдано. Верных ответов: {0}/{1}. Нужно ≥{2}.",
            ["exam.ok"] = "Закрыть",
            ["exam.input.placeholder"] = "Введите ответ…",
            ["rarity.common"] = "Обычная",
            ["rarity.rare"] = "Редкая",
            ["rarity.epic"] = "Эпическая",

            ["hangTag.title"] = "Название",
            ["hangTag.style"] = "Стиль",
            ["hangTag.genre"] = "Жанр",
            ["hangTag.author"] = "Автор",
            ["hangTag.owner"] = "Владелец",
            ["hangTag.broker"] = "Маклер",
        };

        static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["intro.title"] = "Auction House",
            ["intro.subtitle"] = "Art buyer simulator",
            ["intro.lede"] =
                "You are a procurement agent at an art auction. Collectors give orders — identify the right lot and buy before your rivals.",
            ["intro.rule1"] = "Whoever signals first buys the lot. No bidding war.",
            ["intro.rule2"] = "As details are revealed, price rises and commission falls.",
            ["intro.rule3"] = "You only learn if a purchase was correct at end of day.",
            ["intro.rule4"] = "Capital is your lifeline: run out and the career ends.",
            ["intro.start"] = "Start career",
            ["intro.continue"] = "Continue career",
            ["intro.newCareer"] = "New career",

            ["brief.day"] = "Day",
            ["brief.capital"] = "Capital:",
            ["brief.clientHeading"] = "Today's client",
            ["brief.ordersHeading"] = "Orders",
            ["brief.workshop"] = "Upgrades",
            ["brief.enterHall"] = "Start bidding",
            ["brief.toUpgrades"] = "To upgrades",
            ["brief.toOrders"] = "To orders",
            ["brief.resetProgress"] = "Reset progress",
            ["brief.mission"] = "Mission",
            ["brief.owned"] = "Owned",
            ["brief.buy"] = "Buy",
            ["brief.lots"] = "lots",
            ["brief.orderPreview"] = "Order",

            ["collectorPopup.speech"] = "Please buy only paintings that match these parameters:",
            ["collectorPopup.warning"] = "Wrong purchases waste budget and can fine your commission.",
            ["collectorPopup.start"] = "Start bidding (Space)",

            ["auction.day"] = "Day",
            ["auction.venue"] = "Venue:",
            ["auction.lot"] = "Lot",
            ["auction.clientBudget"] = "Client budget:",
            ["auction.currentPrice"] = "Current price",
            ["auction.budgetLeft"] = "Budget left",
            ["auction.currentLot"] = "Current lot",
            ["auction.field.genre"] = "Genre",
            ["auction.field.period"] = "Style",
            ["auction.field.artist"] = "Artist",
            ["auction.field.fact"] = "Interesting fact",
            ["auction.field.title"] = "Title",
            ["auction.startLot"] = "Start bidding (Space)",
            ["auction.buy"] = "BUY! (Space)",
            ["auction.skip"] = "Skip",
            ["auction.finishDay"] = "End day",
            ["auction.familiar"] = "Familiar lot",
            ["auction.insufficient"] = "Insufficient client budget!",
            ["auction.won"] = "Lot is yours!",
            ["auction.lost"] = "A rival took the lot!",
            ["auction.waiting"] = "Rivals are watching…",
            ["auction.speed"] = "Commission ×",

            ["tutorial.buyMatch"] = "This lot matches the order. Press BUY (Space) before rivals beat you.",
            ["tutorial.skipMiss"] = "This lot does not match. Press Skip — don't waste the client budget.",

            ["purchase.continue"] = "Continue",
            ["purchase.collapse"] = "Collapse",

            ["report.title"] = "Day report",
            ["report.continue"] = "To orders",
            ["report.finish"] = "Finish",
            ["report.toBoosters"] = "To boosters",
            ["report.toTags"] = "To tags",
            ["report.tagsHeading"] = "Purchase tags",
            ["report.commission"] = "Commission:",
            ["report.capitalEnd"] = "Capital:",
            ["report.fulfilled"] = "Order fulfilled",
            ["report.unfulfilled"] = "Order not fulfilled",
            ["report.correct"] = "Matched",
            ["report.incorrect"] = "Missed",
            ["report.stampOk"] = "CLIENT\nSTAMP",
            ["report.stampBad"] = "CLIENT\nSTAMP",
            ["report.noPurchases"] = "No purchases today.",
            ["report.boosters"] = "Boosters for tomorrow",
            ["report.buyBooster"] = "Buy",
            ["report.ownedBooster"] = "Taken",
            ["report.ledger"] = "Capital movement",
            ["report.start"] = "Day start",
            ["report.net"] = "Commissions (net)",
            ["report.end"] = "Day end",
            ["report.creditLine"] = "Credit line saved the career",

            ["end.careerTitle"] = "Career complete",
            ["end.bankruptTitle"] = "Bankruptcy",
            ["end.restart"] = "Start over",

            ["chrome.title"] = "Auction House",
            ["chrome.catalog"] = "Catalog",
            ["chrome.licenses"] = "Licenses",
            ["catalog.title"] = "Painting catalog",
            ["catalog.search"] = "Search…",
            ["catalog.close"] = "✕",
            ["catalog.unknown"] = "?",
            ["catalog.page"] = "Page {0} / {1}",
            ["catalog.empty"] = "No matches",
            ["licenses.title"] = "Licenses",
            ["licenses.close"] = "✕",
            ["licenses.regular.owned"] = "Regular auction — owned",
            ["licenses.regular.locked"] = "Regular auction — locked",
            ["licenses.elite.owned"] = "Elite auction — owned",
            ["licenses.elite.locked"] = "Elite auction — locked",
            ["licenses.exam.regular"] = "Take exam ({0})",
            ["licenses.exam.elite"] = "Take exam ({0})",
            ["licenses.hint.needPaintings"] = "See paintings at auction first — the exam needs them.",
            ["licenses.hint.needRegular"] = "Pass the Regular license exam first.",
            ["licenses.hint.needMoney"] = "Not enough capital for an attempt.",
            ["licenses.hint.ready"] = "Exam: 10 cards, need ≥80%. Closing forfeits the attempt.",
            ["exam.progress"] = "Card {0} / {1}",
            ["exam.next"] = "Next",
            ["exam.submit"] = "Submit",
            ["exam.abandon"] = "Abort",
            ["exam.pass"] = "Passed! Correct: {0}/{1}",
            ["exam.fail"] = "Failed. Correct: {0}/{1}. Need ≥{2}.",
            ["exam.ok"] = "Close",
            ["exam.input.placeholder"] = "Type your answer…",
            ["rarity.common"] = "Common",
            ["rarity.rare"] = "Rare",
            ["rarity.epic"] = "Epic",

            ["hangTag.title"] = "Title",
            ["hangTag.style"] = "Style",
            ["hangTag.genre"] = "Genre",
            ["hangTag.author"] = "Author",
            ["hangTag.owner"] = "Owner",
            ["hangTag.broker"] = "Broker",
        };

        public static string Language { get; private set; } = "ru";

        public static void Init()
        {
            Language = PlayerPrefs.GetString(PrefKey, "ru");
            if (Language != "en" && Language != "ru") Language = "ru";
        }

        public static void SetLanguage(string lang)
        {
            Language = lang == "en" ? "en" : "ru";
            PlayerPrefs.SetString(PrefKey, Language);
            PlayerPrefs.Save();
        }

        public static string T(string key)
        {
            var table = Language == "en" ? En : Ru;
            if (table.TryGetValue(key, out var value)) return value;
            if (Ru.TryGetValue(key, out value)) return value;
            return key;
        }
    }
}
