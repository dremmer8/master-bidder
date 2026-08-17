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

const REVEAL_INTERVAL_MS = 1500; // time between each auctioneer field reveal
const RESOLUTION_PAUSE_MS = 1400; // pause after a lot resolves before the next one

// Each collector branch defines its own campaign length via collector.missions
// (see js/collectors.js, edited through gamedesign.html) — missionIndex is
// 0-based and never resets; past the branch's own last authored day it just
// plateaus at max difficulty ("mastery") forever, reusing that last day's tags.
const BRANCH_BUDGET_MULTIPLIER_START = 0.9; // missionIndex 0
const BRANCH_BUDGET_MULTIPLIER_END = 1.6; // missionIndex >= ladder ceiling
const GALLERY_CONNECTIONS_BUDGET_BONUS = 0.15; // 'gallery-connections' upgrade effect

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
function getWorldConfig(day) {
  const t = (day - 1) / (CAMPAIGN_LENGTH - 1);

  const rivalMinSec = lerp(5, 1.5, t);
  const rivalMaxSec = lerp(9, 3.8, t);
  const incorrectFitCoefficient = lerp(INCORRECT_FIT_START, INCORRECT_FIT_END, t);

  return { day, rivalMinSec, rivalMaxSec, incorrectFitCoefficient };
}

// Returns the difficulty config for a collector branch's Nth order
// (missionIndex, 0-based, from state.branchProgress — never resets).
// ladderLength is that branch's own authored day count (collector.missions.length),
// so the venue/trophy/budget curve scales to however many days the designer set up
// for this specific collector, instead of a fixed campaign-wide constant. The
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

// Permanent, one-time meta-progression upgrades (see GAME_DESIGN.md, Money Sinks).
const META_UPGRADES = [
  {
    id: 'gallery-connections',
    nameRu: 'Связи в галерее',
    descRu: 'Бюджеты заказчиков растут на 15% быстрее по мере роста доверия к вам навсегда.',
    cost: 60000,
  },
  {
    id: 'fast-appraisal',
    nameRu: 'Быстрая экспертиза',
    descRu: 'Порог падения множителя комиссии выше на 0.1 навсегда (меньше штраф за долгие раздумья).',
    cost: 80000,
  },
];

// One-day boosters, bought at the end of a day for the *next* day only.
const BOOSTERS = [
  {
    id: 'insurance',
    nameRu: 'Страховка на день',
    descRu: 'Завтра комиссия за ошибочную покупку не может стать штрафом (не уйдёт ниже нуля).',
    cost: (day) => Math.round((15000 + day * 1000) / 100) * 100,
  },
];
