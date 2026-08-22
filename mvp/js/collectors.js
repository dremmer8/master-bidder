// Collector (client) definitions — each one is a campaign branch: a named,
// recurring character with distinct tastes (see GAME_DESIGN.md, Orders & Collectors).
//
// orderGenre / orderPeriod / orderArtist — the collector's thematic focus. The
// shared ladder in campaign.js applies the same phase structure to everyone:
// ORDER_PHASE_GENRE_DAYS of genre-only orders, then period-only, then artist-only.
//
// portraitSource — Wikimedia Commons URL for the collector's portrait.
// Run `npm run fetch-collectors` after changing portraitSource.
//
// This file is generated/edited by gamedesign.html via design-server.js's
// POST /api/collectors — hand edits are fine, just keep the shape intact.
const COLLECTORS = [
  {
    id: 'baron-svetozarov',
    nameRu: 'Изабелла д\'Эсте',
    taglineRu: 'Ценит гармонию и человеческое достоинство итальянского Возрождения',
    portraitSource:
      'https://commons.wikimedia.org/wiki/Special:FilePath/Tizian_-_Portr%C3%A4t_der_Isabella_d%27Este.jpg?width=800',
    personalModifier: 1.15,
    baseBudget: 380000,
    orderGenre: 'Портрет',
    orderPeriod: 'Высокое Возрождение',
    orderArtist: 'Леонардо да Винчи',
  },
  {
    id: 'madame-volkonskaya',
    nameRu: 'Изабелла Стюарт Гарднер',
    taglineRu: 'Одержима французским импрессионизмом и игрой света на воде',
    portraitSource:
      'https://commons.wikimedia.org/wiki/Special:FilePath/John_Singer_Sargent_-_Isabella_Stewart_Gardner,_1888.jpg?width=800',
    personalModifier: 1.3,
    baseBudget: 340000,
    orderGenre: 'Пейзаж',
    orderPeriod: 'Импрессионизм',
    orderArtist: 'Клод Моне',
  },
  {
    id: 'professor-gortenziev',
    nameRu: 'Леопольд Вильгельм Австрийский',
    taglineRu: 'Годами изучает голландских мастеров света, быта и тишины',
    portraitSource:
      'https://commons.wikimedia.org/wiki/Special:FilePath/Leopold_Wilhelm_of_Austria.jpg?width=800',
    personalModifier: 0.9,
    baseBudget: 300000,
    orderGenre: 'Жанровая сцена',
    orderPeriod: 'Голландское золотое время',
    orderArtist: 'Рембрандт ван Рейн',
  },
  {
    id: 'countess-tenisheva',
    nameRu: 'Княгиня Мария Тенишева',
    taglineRu: 'Влюблена в драматический свет и театральность барокко',
    portraitSource:
      'https://commons.wikimedia.org/wiki/Special:FilePath/Maria_Tenisheva_by_A.P.Sokolov_(1898,_GRM).jpg?width=800',
    personalModifier: 1.1,
    baseBudget: 320000,
    orderGenre: 'Групповой портрет',
    orderPeriod: 'Барокко',
    orderArtist: 'Питер Пауль Рубенс',
  },
  {
    id: 'captain-severin',
    nameRu: 'Теодор Дюре',
    taglineRu: 'Собирает бурные пейзажи романтизма и японскую гравюру укиё-э',
    portraitSource:
      'https://commons.wikimedia.org/wiki/Special:FilePath/Edouard_Manet_018.jpg?width=800',
    personalModifier: 0.95,
    baseBudget: 300000,
    orderGenre: 'Пейзаж',
    orderPeriod: 'Романтизм',
    orderArtist: 'Каспар Давид Фридрих',
  },
];

COLLECTORS.forEach((collector) => {
  if (collector.portraitSource) {
    collector.portraitUrl = `assets/collectors/${collector.id}.webp`;
  }
});
