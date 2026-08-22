// Campaign-wide tunables and per-day difficulty curve.
// See GAME_DESIGN.md for the design rationale behind every section below —
// most numeric constants here are explicitly marked TBD-via-playtesting there.

const CAMPAIGN_LENGTH = 15;
const STARTING_CAPITAL = 40000;

// Commission = rarity_value * speed_multiplier * fit_coefficient * collector_personal_modifier.
// Rarity no longer only sets price (see GAME_DESIGN.md, Commission Formula) — it also
// feeds commission directly through this table.
const RARITY_COMMISSION_VALUE = { common: 6000, rare: 20000, epic: 55000 };

// Each auto-revealed field bumps price up one step and drops the speed multiplier
// one step, in lockstep. 5 revealable fields per lot (title/artist/period/genre/fact).
const REVEALABLE_FIELDS = ['genre', 'period', 'artist', 'fact', 'title'];
const PRICE_STEP_PCT = 0.12; // price grows 12% per revealed field
const SPEED_MULTIPLIER_FLOOR = 0.35; // multiplier at step 0 is always 1.0

// The fit coefficient for an INCORRECT purchase ramps smoothly across the
// campaign: a small net-positive early on, crossing to a real fine (negative)
// later — this ramp is what turns "buying wrong" into the bankruptcy trigger.
const INCORRECT_FIT_START = 0.15; // day 1
const INCORRECT_FIT_END = -0.35; // final day

const REVEAL_INTERVAL_MS = 2250; // time between each auctioneer field reveal
const RESOLUTION_PAUSE_MS = 2100; // pause after a lot resolves before the next one
const SKIP_FAST_REVEAL_INTERVAL_MS = 380; // accelerated reveal when skipping a lot
const SKIP_RIVAL_PAUSE_MS = 650; // beat after last reveal before rival buys

// Each collector branch shares the same order-difficulty ladder (genre → period →
// artist); see ORDER_PHASE_* below and getOrderTagsForMission(). Past the ladder
// ceiling the branch plateaus on the last phase ("mastery").
const BRANCH_BUDGET_MULTIPLIER_START = 0.9; // missionIndex 0
const BRANCH_BUDGET_MULTIPLIER_END = 1.6; // missionIndex >= ladder ceiling

// Shared order ladder — every collector uses the same phase lengths; only the
// tag values differ (collector.orderGenre / orderPeriod / orderArtist).
const ORDER_PHASE_GENRE_DAYS = 3;
const ORDER_PHASE_PERIOD_DAYS = 3;
const ORDER_PHASE_ARTIST_DAYS = 4;
const ORDER_LADDER_LENGTH =
  ORDER_PHASE_GENRE_DAYS + ORDER_PHASE_PERIOD_DAYS + ORDER_PHASE_ARTIST_DAYS;

// Venue definitions — differ in more than risk: content itself is scaled per venue
// (see GAME_DESIGN.md, Venues: Regular / Local / Elite Auctions). Which tier a
// branch's order uses is now driven by that branch's own missionIndex (see
// getBranchMissionConfig) instead of a manual player choice.
const VENUES = {
  regular: {
    key: 'regular',
    labelRu: 'Обычный аукцион',
    rarityPool: ['common', 'rare', 'epic'],
    budgetFactor: 1,
    lotsCount: () => 10 + Math.floor(Math.random() * 3), // 10-12
    guaranteedNonNegativeFine: false,
    rivalSpeedFactor: 1,
  },
  local: {
    key: 'local',
    labelRu: 'Местный аукцион',
    descRu:
      'Коллекционеры-любители. Ниже редкость, проще заказы, медленнее конкуренты — и штрафа тут не бывает вообще.',
    rarityPool: ['common', 'rare'],
    budgetFactor: 0.45,
    lotsCount: () => 6 + Math.floor(Math.random() * 2), // 6-7
    guaranteedNonNegativeFine: true,
    rivalSpeedFactor: 2.2, // rivals much slower to react
  },
  elite: {
    key: 'elite',
    labelRu: 'Элитный аукцион',
    descRu: 'Выше редкость лотов, крупнее бюджеты заказов, но и конкуренты заметно быстрее.',
    rarityPool: ['rare', 'epic'],
    budgetFactor: 2.4,
    lotsCount: () => 8 + Math.floor(Math.random() * 3), // 8-10
    guaranteedNonNegativeFine: false,
    rivalSpeedFactor: 0.6, // rivals react faster than the day's normal curve
  },
};

function lerp(a, b, t) {
  return a + (b - a) * t;
}

// Returns the campaign-wide economy config for a given day (1-based). This is
// deliberately branch-agnostic: rivals get faster and mistakes get costlier
// for everyone as the whole art market heats up over the campaign, regardless
// of which collector branch the player is working that day.
// state is optional (only 'legal-counsel' reads it, to soften the late-campaign
// incorrect-fit floor forever).
function getWorldConfig(day, state) {
  const t = (day - 1) / (CAMPAIGN_LENGTH - 1);

  const rivalMinSec = lerp(7.5, 2.25, t);
  const rivalMaxSec = lerp(13.5, 5.7, t);
  const incorrectFitEnd = INCORRECT_FIT_END + (state && state.upgrades.has('legal-counsel') ? 0.1 : 0);
  const incorrectFitCoefficient = lerp(INCORRECT_FIT_START, incorrectFitEnd, t);

  return { day, rivalMinSec, rivalMaxSec, incorrectFitCoefficient };
}

// Returns the difficulty config for a collector branch's Nth order
// (missionIndex, 0-based, from state.branchProgress — never resets).
// ladderLength is ORDER_LADDER_LENGTH — the shared genre→period→artist ladder
// (see ORDER_PHASE_*). Venue/trophy/budget curve scales to that length.
// thresholds below (20% local / 60% regular / 20% elite, trophy ramp over the
// last 30%) reproduce the shape of the original fixed 10-day ladder at any length.
function getBranchMissionConfig(missionIndex, ladderLength) {
  const length = Math.max(1, ladderLength);
  const t = Math.min(missionIndex, length - 1) / Math.max(1, length - 1);

  let venueTier;
  if (missionIndex < length * 0.2) venueTier = 'local';
  else if (missionIndex < length * 0.8) venueTier = 'regular';
  else venueTier = 'elite';

  const trophyStart = Math.floor(length * 0.7);
  const trophyChance =
    missionIndex < trophyStart ? 0 : lerp(0, 0.35, (missionIndex - trophyStart) / Math.max(1, length - 1 - trophyStart));

  const branchBudgetMultiplier = lerp(BRANCH_BUDGET_MULTIPLIER_START, BRANCH_BUDGET_MULTIPLIER_END, t);

  return { missionIndex, trophyChance, venueTier, branchBudgetMultiplier };
}

// Returns the AND-matched tag set for a branch's Nth order (missionIndex, 0-based).
// Phase 1: genre only · phase 2: period only · phase 3: artist only.
function getOrderTagsForMission(missionIndex, collector) {
  const idx = Math.min(missionIndex, ORDER_LADDER_LENGTH - 1);
  if (idx < ORDER_PHASE_GENRE_DAYS) {
    return [{ type: 'genre', value: collector.orderGenre }];
  }
  if (idx < ORDER_PHASE_GENRE_DAYS + ORDER_PHASE_PERIOD_DAYS) {
    return [{ type: 'period', value: collector.orderPeriod }];
  }
  return [{ type: 'artist', value: collector.orderArtist }];
}

function getOrderPhaseForMission(missionIndex) {
  const idx = Math.min(missionIndex, ORDER_LADDER_LENGTH - 1);
  if (idx < ORDER_PHASE_GENRE_DAYS) return 'genre';
  if (idx < ORDER_PHASE_GENRE_DAYS + ORDER_PHASE_PERIOD_DAYS) return 'period';
  return 'artist';
}

function getCollectorBranchProgress(missionIndex) {
  const total = ORDER_LADDER_LENGTH;
  const completed = Math.min(missionIndex, total);
  const remaining = Math.max(0, total - missionIndex);
  const mastered = missionIndex >= total;
  const currentOrder = mastered ? total : missionIndex + 1;
  return { total, completed, remaining, mastered, currentOrder };
}

// Permanent, one-time meta-progression upgrades (see GAME_DESIGN.md, Money Sinks).
const META_UPGRADES = [
  {
    id: 'fast-appraisal',
    nameRu: 'Быстрая экспертиза',
    icon: '⚡',
    descRu: 'Порог падения множителя комиссии выше на 0.1 навсегда (меньше штраф за долгие раздумья).',
    cost: 80000,
  },
  {
    id: 'expert-reputation',
    nameRu: 'Репутация эксперта',
    icon: '⭐',
    descRu: 'Вся заработанная комиссия навсегда увеличена на 3%.',
    cost: 70000,
  },
  {
    id: 'cool-nerves',
    nameRu: 'Хладнокровие',
    icon: '🧊',
    descRu: 'Цена лота навсегда растёт на 10% медленнее за каждый раскрытый признак.',
    cost: 65000,
  },
  {
    id: 'standing-advance',
    nameRu: 'Постоянный аванс',
    icon: '💵',
    descRu: 'Бюджет заказа(ов) навсегда увеличен на 8%.',
    cost: 60000,
  },
  {
    id: 'legal-counsel',
    nameRu: 'Юридический советник',
    icon: '⚖️',
    descRu: 'Штраф за неверную покупку на поздних днях кампании навсегда мягче.',
    cost: 90000,
  },
  {
    id: 'credit-line',
    nameRu: 'Кредитная линия',
    icon: '🏦',
    descRu: 'Один раз за карьеру капитал не уходит в минус: обнуляется вместо банкротства.',
    cost: 120000,
  },
  {
    id: 'calm-hall',
    nameRu: 'Спокойный зал',
    icon: '🕊️',
    descRu: 'Соперники навсегда реагируют на 15% медленнее на каждом лоте.',
    cost: 85000,
  },
  {
    id: 'expanded-hall',
    nameRu: 'Расширенный зал',
    icon: '🏛️',
    descRu: 'В подборке навсегда на 2 лота больше каждый день.',
    cost: 55000,
  },
  {
    id: 'lot-master',
    nameRu: 'Мастер лотов',
    icon: '🎯',
    descRu: 'Каждый день есть 10% шанс, что в подборку бесплатно добавится гарантированный эпический лот.',
    cost: 70000,
  },
  {
    id: 'loyal-client',
    nameRu: 'Постоянный клиент',
    icon: '🎟️',
    descRu: 'Цена всех бустеров навсегда ниже на 15%.',
    cost: 50000,
  },
  {
    id: 'personal-secretary',
    nameRu: 'Личный секретарь',
    icon: '🧑‍💼',
    descRu: 'Каждый вечер предлагается на 1 бустер больше — и все их можно купить.',
    cost: 95000,
  },
  {
    id: 'investment-portfolio',
    nameRu: 'Инвестиционный портфель',
    icon: '💹',
    descRu: 'В начале каждого дня капитал навсегда растёт на 1%.',
    cost: 100000,
  },
];

// Each end-of-day report re-rolls this many random boosters to offer for sale
// (see Game.finishDay -> state.boosterOffers) — the player can afford to buy
// every one shown, so this also doubles as the per-day cap (see Game.buyBooster).
// 'personal-secretary' bumps this by 1 forever — see getMaxDailyBoosters.
const MAX_DAILY_BOOSTERS = 3;

function getMaxDailyBoosters(state) {
  return MAX_DAILY_BOOSTERS + (state.upgrades.has('personal-secretary') ? 1 : 0);
}

// One-day boosters, bought at the end of a day for the *next* day only.
// All of them read state.activeBoosters (a Set of these ids, populated for the
// upcoming day only in Game.continueAfterReport) — see engine.js call sites.
const BOOSTERS = [
  {
    id: 'insurance',
    nameRu: 'Страховка на день',
    icon: '🛡️',
    descRu: 'Завтра комиссия за ошибочную покупку не может стать штрафом (не уйдёт ниже нуля).',
    cost: (day) => Math.round((15000 + day * 1000) / 100) * 100,
  },
  {
    id: 'expert-appraiser',
    nameRu: 'Опытный оценщик',
    icon: '🔍',
    descRu: 'Завтра на каждом лоте один случайный признак раскрыт бесплатно с самого начала — без влияния на цену и скорость.',
    cost: (day) => Math.round((20000 + day * 1200) / 100) * 100,
  },
  {
    id: 'quiet-start',
    nameRu: 'Тихий старт',
    icon: '🤫',
    descRu: 'Завтра на первом лоте дня соперник вообще не подключается — гарантированная разминка без риска.',
    cost: (day) => Math.round((9000 + day * 600) / 100) * 100,
  },
  {
    id: 'sleepy-rivals',
    nameRu: 'Сонные соперники',
    icon: '😴',
    descRu: 'Завтра соперники реагируют на 45% медленнее на каждом лоте — больше времени на решение.',
    cost: (day) => Math.round((24000 + day * 1400) / 100) * 100,
  },
  {
    id: 'auction-discount',
    nameRu: 'Скидка аукциона',
    icon: '🏷️',
    descRu: 'Завтра цена лота растёт на треть медленнее за каждый раскрытый признак.',
    cost: (day) => Math.round((16000 + day * 900) / 100) * 100,
  },
  {
    id: 'budget-advance',
    nameRu: 'Аванс от заказчика',
    icon: '💰',
    descRu: 'Завтра бюджет заказа(ов) увеличен на 20%.',
    cost: (day) => Math.round((14000 + day * 900) / 100) * 100,
  },
  {
    id: 'commission-bonus',
    nameRu: 'Комиссионный бонус',
    icon: '📈',
    descRu: 'Вся комиссия, заработанная завтра, увеличена на 5%.',
    cost: (day) => Math.round((10000 + day * 700) / 100) * 100,
  },
  {
    id: 'lucky-lot',
    nameRu: 'Счастливый лот',
    icon: '🍀',
    descRu: 'В завтрашней подборке гарантированно будет хотя бы один эпический лот, даже если площадка обычно их не пускает.',
    cost: (day) => Math.round((17000 + day * 1000) / 100) * 100,
  },
  {
    id: 'marathon',
    nameRu: 'Марафон',
    icon: '🏃',
    descRu: 'Завтра в зале на 3 лота больше — больше попыток закрыть заказ.',
    cost: (day) => Math.round((15000 + day * 900) / 100) * 100,
  },
];
