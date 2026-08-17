// Collector (client) definitions — each one is a campaign branch: a named,
// recurring character with distinct tastes (see GAME_DESIGN.md, Orders & Collectors).
//
// missions[] is that branch's own day-by-day campaign: missions[i].tags is the
// exact set of AND-matched tags used for that branch's (i+1)-th order. Once the
// player has done more orders than missions.length, the branch plateaus forever
// on its last authored day ("mastery") — see getBranchMissionConfig in campaign.js,
// which also derives venue tier / trophy chance / budget multiplier from
// missionIndex scaled against this branch's own missions.length.
//
// tags[].type must be one of 'period' | 'genre' | 'artist' and tags[].value must
// match an existing ARTWORKS periodRu/genreRu/artistRu value (see data.js) so
// matchesCriteria() in engine.js can compare them.
//
// This file is generated/edited by gamedesign.html via design-server.js's
// POST /api/collectors — hand edits are fine, just keep the shape intact.
const COLLECTORS = [
  {
    id: 'baron-svetozarov',
    nameRu: 'Барон Аркадий Светозаров',
    taglineRu: 'Ценит гармонию и человеческое достоинство итальянского Возрождения',
    personalModifier: 1.15,
    baseBudget: 380000,
    missions: [
      { tags: [{ type: 'genre', value: 'Портрет' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
      { tags: [{ type: 'genre', value: 'Портрет' }, { type: 'period', value: 'Высокое Возрождение' }] },
    ],
  },
  {
    id: 'madame-volkonskaya',
    nameRu: 'Мадам Элеонора Волконская',
    taglineRu: 'Одержима французским импрессионизмом и игрой света на воде',
    personalModifier: 1.3,
    baseBudget: 340000,
    missions: [
      { tags: [{ type: 'genre', value: 'Пейзаж' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Импрессионизм' }] },
    ],
  },
  {
    id: 'professor-gortenziev',
    nameRu: 'Профессор Лев Наумович Гортензиев',
    taglineRu: 'Годами изучает голландских мастеров света, быта и тишины',
    personalModifier: 0.9,
    baseBudget: 300000,
    missions: [
      { tags: [{ type: 'period', value: 'Голландское золотое время' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
      { tags: [{ type: 'period', value: 'Голландское золотое время' }, { type: 'genre', value: 'Жанровая сцена' }] },
    ],
  },
  {
    id: 'countess-tenisheva',
    nameRu: 'Графиня Аглая Тенишева',
    taglineRu: 'Влюблена в драматический свет и театральность барокко',
    personalModifier: 1.1,
    baseBudget: 320000,
    missions: [
      { tags: [{ type: 'period', value: 'Барокко' }] },
      { tags: [{ type: 'period', value: 'Барокко' }] },
      { tags: [{ type: 'period', value: 'Барокко' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
      { tags: [{ type: 'period', value: 'Барокко' }, { type: 'genre', value: 'Групповой портрет' }] },
    ],
  },
  {
    id: 'captain-severin',
    nameRu: 'Капитан Фёдор Северин',
    taglineRu: 'Собирает бурные пейзажи романтизма и японскую гравюру укиё-э',
    personalModifier: 0.95,
    baseBudget: 300000,
    missions: [
      { tags: [{ type: 'genre', value: 'Пейзаж' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
      { tags: [{ type: 'genre', value: 'Пейзаж' }, { type: 'period', value: 'Романтизм' }] },
    ],
  },
];
