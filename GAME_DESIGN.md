# Auction House Specialist — Game Design Document

Living design document for the HTML/JS MVP in `mvp/`. Numbers below are the **current implemented tunables**, not leftover pre-build speculation. Change them in `mvp/js/campaign.js` (and collector data via the design editor) when playtesting says so.

## Concept

An educational game disguised as an arcade reflex game. The player is an art-buying agent working the floor of an auction house. Education (art history: titles, authors, periods, genres, facts) is absorbed as a *side effect* of learning to recognize artworks fast enough to win them before rival buyers — never delivered as an explicit quiz or lesson.

**Target audience:** general public interested in art, casual players who enjoy trivia/reflex games.
**Language:** Russian (UI + content) for MVP; architecture should allow later localization.
**Release ambition:** free, public release. Treat asset licensing rigorously from day one (see Content & Licensing).

## Development Path

1. **MVP (current):** browser-based HTML/JS in `mvp/` (`index.html`). 2D-only, no 3D, no voice-over. Playable campaign with economy, collector branches, venues, meta-upgrades, and end-of-day settlement.
2. **Full version:** Unity 3D, adds sculptures/3D scans, full auction-room presentation, voice-over, deeper meta-progression shop.

Do not build Unity/3D features before the HTML MVP validates the core loop.

---

## Player Flow

A run is a single 15-day campaign. Screens, in order:

1. **Intro** — premise and the four non-negotiable rules (first-signal wins / no bidding war; price rises and commission falls during reveal; correctness is deferred to end of day; capital is the only life stat).
2. **Day brief** — pick **one collector** for today (each collector is a campaign branch). Optionally buy permanent upgrades from the workshop, paid from the current capital. Then enter the hall.
3. **Auction floor** — one venue session for that day, driven by the chosen branch's progress (see Venues). Sequential field reveal, live price and commission multiplier, race against faceless rivals, optional skip.
4. **End-of-day report** — first time the player learns which purchases matched the order. Settlement, ledger, then (if the day was survived and the campaign continues) optional one-shot booster for *tomorrow*.
5. **Campaign end or bankruptcy** — survive all 15 days, or go below zero at settlement.

There is no day-retry and no mid-day second venue. One collector, one order, one auction session per day.

---

## Core Gameplay Loop

1. An exhibit (lot) is presented visually. Not-yet-revealed fields are **masked** (bullet placeholders of similar length) so values cannot be read early.
2. The auctioneer reveals information **sequentially, as text**, in this fixed order: `Genre → Period → Author → Interesting Fact → Title`. Reveal is **automatic, on a timer** — not a player-driven action. Cadence: **1.5s per field**.
3. **Price and commission multiplier are live from the start of the reveal.** Each auto-revealed field bumps the price up one step (+12% of the jittered base) and drops the speed multiplier one step toward a floor of **×0.35**. Both effects share a single step counter. Buying early is cheaper and pays better; waiting costs more and pays less. A fully-revealed lot is still worth buying — just less profitable.
4. At any point the player can **Buy** (button or Space) or **Skip**. Skip resolves the lot with no purchase and no capital change.
5. **Buying is a deterministic race**: whoever signals first gets the lot. There is no bidding war. The only thing moving the price is reveal-driven step growth, not competing bids.
6. Purchases that would exceed the current capital are blocked ("Недостаточно средств!") — no buying on credit.
7. **Race outcome** (won / lost to a rival / skipped) is shown immediately, then the next lot starts after a short pause (~1.4s).
8. **Whether the purchase matched the day's order is not revealed during the auction at all** — no colors, icons, or running match counts. That wait is the informational climax of the end-of-day report.
9. **Running capital is always visible** (brief HUD, auction HUD). It is the survival stat.
10. The player can click the lot image at any time to **zoom**.

After the last lot of the session, settlement runs once for the whole day.

### Feedback Timing (important, non-obvious)

Three distinct signals must not be conflated:
- **Race resolution** (did I get the item, skip it, or did a rival?) — always instant.
- **Running capital balance** — always instant/visible.
- **Order-match correctness** (was this the right item, and what did it earn/cost?) — always deferred to the end-of-day report.

---

## Rival Bidders (AI)

- All lots are contested. Deliberate simplification for pacing (rejected the alternative of bots having their own hidden preferences).
- Bots are **faceless/unnamed**: five anonymous heads in a hall strip under the painting. When a rival wins, one random head raises. Personas are deferred.
- Rival reaction is a random delay drawn from that **calendar day's** window, then multiplied by the venue's speed factor:
  - Day 1 window: **5.0–9.0s**. Day 15 window: **1.5–3.8s**. Linear lerp in between.
  - Local ×2.2 (slower). Regular ×1.0. Elite ×0.6 (faster).
- The rival timer is independent of how many fields have been revealed; a fast rival can snatch a lot mid-reveal.

---

## Campaign Structure: Collector Branches

This is the main structural change from the original "1–3 orders + pick a venue" brief.

- The campaign is **15 calendar days** long (`CAMPAIGN_LENGTH`).
- **Each collector is a campaign branch.** On the brief screen the player picks exactly **one** collector for the day and receives **exactly one** order from that collector.
- Each branch has its own authored mission list (`missions[]` in `mvp/js/collectors.js`). The branch's mission counter (`branchProgress`) **never resets** and only advances when that branch is played.
- `missions[i].tags` is the AND-matched criteria for that branch's (i+1)-th order. Mixing branches across the 15 days is the player's campaign strategy: you can deepen one collector or rotate.
- Past the last authored mission, the branch **plateaus on "mastery"**: it keeps reusing the last day's tags, at max venue/budget/trophy for that ladder.
- Venue tier, trophy chance, and branch budget multiplier are **derived from the chosen branch's mission index vs that branch's own ladder length** — not chosen by the player, and not a global day-number gate. See Venues.

Current content: **5 named collectors**, **10 authored days each**. See Orders & Collectors for the roster.

A design editor (`mvp/gamedesign.html`, served by `npm run design` in `mvp/`) authors collector names, modifiers, budgets, and per-day tag lists. Tag values must match artwork `periodRu` / `genreRu` / `artistRu` in `mvp/js/data.js`.

---

## Orders & Collectors (Clients)

- **Always exactly one active order** while buying. A purchase can never end up with "nobody to sell it to."
- Collectors are **named, recurring characters with distinct tastes**.
- **Order criteria are category-based** (period / genre / artist), or a **trophy** order for one specific named artwork. Trophy rolls are off on Local, and otherwise ramp over the last 30% of that branch's ladder (0% → 35%).
- **Matching is binary**: a lot is correct only if it contains **all** of that order's requested tags. No partial credit.
- **Each collector has a personal commission modifier** that multiplies on top of the base formula.
- **Each order carries a budget**, credited to the player's single balance the moment the auction begins (after the brief, so workshop purchases happen *before* the budget injection).
- **There is no fixed wanted quantity.** The collector pays commission on every attributed purchase, bounded only by the budget as a *soft cap* (see Economy).
- **Conflict resolution** is still written as: if a lot matches more than one active order, credit exactly one at random; if it matches none, auto-attribute as incorrect to an active order (preferring the same venue). With the current "one order per day" rule this always credits that single collector.

### Current roster

| Collector | Taste (authored arc) | Modifier | Base budget |
|---|---|---|---|
| Барон Аркадий Светозаров | Portrait → High Renaissance portrait | ×1.15 | 380 000 ₽ |
| Мадам Элеонора Волконская | Landscape → Impressionist landscape | ×1.30 | 340 000 ₽ |
| Профессор Лев Наумович Гортензиев | Dutch Golden Age → genre scenes | ×0.90 | 300 000 ₽ |
| Графиня Аглая Тенишева | Baroque → group portrait | ×1.10 | 320 000 ₽ |
| Капитан Фёдор Северин | Landscape → Romantic landscape | ×0.95 | 300 000 ₽ |

Typical authored shape (10 days): first three days a single tag, then a second AND-tag for the rest of the ladder.

---

## Economy

### One unified balance

- Exactly **one** player-owned capital. It persists across the campaign. Starting capital: **40 000 ₽**.
- During live bidding the player always spends this capital. There is no separate client-money pool.
- Purchases are blocked if they would exceed the current balance. A purchase itself can never be the direct cause of bankruptcy (see Loss Condition).

### Orders & Budget

- Accepting the day's order **immediately adds that order's budget to the balance.**
- Budget = `round(collector.baseBudget × branchBudgetMultiplier × venue.budgetFactor)` to the nearest 100 ₽.
  - Branch multiplier lerps **0.9 → 1.6** across that branch's authored ladder.
  - Venue factors: Local **0.45**, Regular **1.0**, Elite **2.4**.
- At end-of-day settlement: `leftover = max(0, budget − total price of paintings attributed to that order)`. Leftover is **clawed back**. The collector keeps unspent budget and every painting attributed to them (right or wrong).
- Soft cap, not a hard wall: the player can keep buying correct paintings past the budget, paying extra out of pooled capital. Those purchases still earn **full commission**. Nothing beyond the budget is refunded.
- Net effect when spend stays within budget: the injection and the clawback cancel. The only real profit or loss is **commission**.
- **The player never keeps a painting.** Every purchase ends up with the collector it is attributed to. Commission for correct purchases is not reduced just because the budget was exceeded.

### Dynamic Price & Commission

- Each lot has a rarity tier (common / rare / epic) with a base price. At draw time the price is jittered **×0.85–1.15**, then rounded to 100 ₽.
- Each revealed field: price × (1 + step × **0.12**), speed multiplier interpolates from **1.0** at step 0 to the floor at step 5.
- Speed floor: **0.35**, or **0.45** with the `fast-appraisal` upgrade.
- Live HUD shows current price and `×multiplier`.

### Commission Formula

```
commission = rarity_value × speed_multiplier × fit_coefficient × collector_personal_modifier
```

- **`rarity_value`** — common **6 000**, rare **20 000**, epic **55 000**. Rarity also still sets base price.
- **`speed_multiplier`** — from the reveal-step mechanic above.
- **`fit_coefficient`** — **1.0** if the lot matches the order; otherwise the campaign's incorrect-fit value (see below).
- **`collector_personal_modifier`** — the collector's quirk multiplier.

Incorrect-fit coefficient **lerps with calendar day**, not with branch progress: **+0.15 on day 1 → −0.35 on day 15**. Early on, a wrong buy is still a small net-positive commission; later it becomes a real fine and the bankruptcy trigger.

**Local auctions and the insurance booster** clamp an incorrect fit coefficient to **≥ 0**, so a wrong buy cannot produce a fine there / that day. They do not turn a wrong buy into a correct one.

---

## Loss Condition & Campaign Progression

- **No daily rating, no pass/fail score, no per-collector reputation/mood.** Collectors are a money mechanism plus authored taste.
- **The only failure condition is bankruptcy**: if end-of-day settlement would take the balance below zero, the run ends. There is no other way to fail.
- **No day-retry.** The campaign advances day to day, carrying the balance, until day 15 is cleared or the player goes bankrupt.
- Two difficulty clocks run in parallel:
  - **Calendar day (1–15):** rival speed and incorrect-fit coefficient (market heat).
  - **Per-branch mission index:** venue tier, trophy chance, order budget, and which tags are asked for.

---

## Venues: Regular / Local / Elite

Three tiers that differ in risk, reward, **and content**. The player does **not** pick a venue and does **not** pay an Elite ticket. Venue is a consequence of how far the chosen collector branch has been taken.

For a branch of length `L` (currently 10), mission index `i` (0-based):

- **Local** if `i < 0.2L` (first 20%)
- **Regular** if `i < 0.8L` (next 60%)
- **Elite** if `i ≥ 0.8L` (last 20%)

On a 10-day ladder that is missions 1–2 Local, 3–8 Regular, 9–10 Elite, then mastery stays Elite.

| | Local | Regular | Elite |
|---|---|---|---|
| Label | Местный аукцион | Обычный аукцион | Элитный аукцион |
| Rarity pool | common, rare | common, rare, epic | rare, epic |
| Lots / session | 6–7 | 10–12 | 8–10 |
| Budget factor | 0.45 | 1.0 | 2.4 |
| Rival delay factor | ×2.2 (slower) | ×1.0 | ×0.6 (faster) |
| Incorrect-buy fine | clamped ≥ 0 | follows campaign curve | follows campaign curve |
| Trophy orders | never | allowed | allowed |

All three draw from and pay into the **same single balance**.

---

## Money Sinks: Meta-Progression & Boosters

Both spend the same unified capital.

### Permanent upgrades (bought on the day brief, before the order budget is credited)

| Id | Name | Effect | Cost |
|---|---|---|---|
| `fast-appraisal` | Быстрая экспертиза | Speed-multiplier floor +0.10 forever | 80 000 ₽ |
| `expert-reputation` | Репутация эксперта | All commission earned forever ×1.03 | 70 000 ₽ |
| `cool-nerves` | Хладнокровие | Price grows forever ×0.9 per revealed field | 65 000 ₽ |
| `standing-advance` | Постоянный аванс | Order budget(s) forever ×1.08 (stacks multiplicatively with `budget-advance`, and the same multiplier now also inflates the solvency floor so it always shows through) | 60 000 ₽ |
| `legal-counsel` | Юридический советник | The late-campaign incorrect-fit floor is forever 0.1 less harsh | 90 000 ₽ |
| `credit-line` | Кредитная линия | Once per career, a bankrupting day is clamped to 0 instead of ending the run | 120 000 ₽ |
| `calm-hall` | Спокойный зал | Every rival's reaction delay is forever ×1.15 | 85 000 ₽ |
| `expanded-hall` | Расширенный зал | +2 lots per day, forever | 55 000 ₽ |
| `lot-master` | Мастер лотов | Each day, a 10% chance of a free guaranteed epic-rarity lot (rolled once at day start) | 70 000 ₽ |
| `loyal-client` | Постоянный клиент | All booster prices forever ×0.85 | 50 000 ₽ |
| `personal-secretary` | Личный секретарь | +1 to the daily booster offer count (and cap), forever | 95 000 ₽ |
| `investment-portfolio` | Инвестиционный портфель | Capital forever ×1.01 at the start of each day | 100 000 ₽ |

One-time; cannot be rebought.

### Boosters (bought on the end-of-day report, apply to the *next* day only)

Each report re-rolls 3 random boosters (of the 9 below) to offer for sale; all
three stack into a single `activeBoosters` set for the next day, so a lucky
roll can be fully bought out.

| Id | Name | Effect | Cost |
|---|---|---|---|
| `insurance` | Страховка на день | Tomorrow, incorrect-buy commission cannot go below zero | `15 000 + 1 000 × nextDay`, rounded to 100 ₽ |
| `expert-appraiser` | Опытный оценщик | Tomorrow, one random field per lot is free-revealed at lot start — no effect on price or speed multiplier | `20 000 + 1 200 × nextDay`, rounded to 100 ₽ |
| `quiet-start` | Тихий старт | Tomorrow, no rival ever appears on the first lot of the day | `9 000 + 600 × nextDay`, rounded to 100 ₽ |
| `sleepy-rivals` | Сонные соперники | Tomorrow, every rival's reaction delay is ×1.45 | `24 000 + 1 400 × nextDay`, rounded to 100 ₽ |
| `auction-discount` | Скидка аукциона | Tomorrow, price grows ×0.67 per revealed field instead of the campaign rate | `16 000 + 900 × nextDay`, rounded to 100 ₽ |
| `budget-advance` | Аванс от заказчика | Tomorrow's order budget(s) ×1.2 | `14 000 + 900 × nextDay`, rounded to 100 ₽ |
| `commission-bonus` | Комиссионный бонус | All commission earned tomorrow ×1.05 (correct and reduced-rate sales alike) | `10 000 + 700 × nextDay`, rounded to 100 ₽ |
| `lucky-lot` | Счастливый лот | Tomorrow's lot draw guarantees at least one epic-rarity lot, bypassing the venue's own rarity pool if needed | `17 000 + 1 000 × nextDay`, rounded to 100 ₽ |
| `marathon` | Марафон | Tomorrow's venue draws 3 extra lots | `15 000 + 900 × nextDay`, rounded to 100 ₽ |

Cheap enough to be a repeatable tactical choice. Hidden on the final day's report and on bankruptcy.

---

## Market Recurrence ("living market")

- The same physical artwork can appear on later days (the lot pool is redrawn from the venue's rarity filter each session).
- A lot the player **previously bought** is marked **«Знакомый лот»**. Merely seeing a lot and losing/skipping it does not mark it familiar. The badge is the memorization hook and must not be silently omitted for purchased works.

---

## Presentation (MVP)

- Dark desktop-style layout, Russian UI, gold accent. Title in-game: **«Аукционный дом — Симулятор скупщика»**.
- Auction stage, two columns: a **wide painting block** on the left (canvas + rival audience seated in a hall strip under the image + zoom hint); data column on the right.
- Right column, top to bottom: the day's **order in its own gold card** (collector, criteria, budget); live price and commission multiplier; a separate **current-lot panel** (genre / period / artist / fact / title); result banner; Buy / Skip.
- Lot reveal rows whose tag type matches the order (genre / period / artist, or title for a trophy) get a **light** accent so the player can find the important line. The order text itself stays in the gold card — it is not mixed into the lot fields. Correctness of the match is still deferred to the report.
- HUD: calendar day / 15, venue name, lot index, capital.
- Report: per-order spend / leftover clawback / correct vs incorrect counts / commission; per-purchase match ledger; capital movement (start, commissions, clawback, other spend such as workshop overspend beyond budget, end).

---

## Content & Licensing

- **All artwork data must be real**: real titles, real artists, real periods/genres, real facts. No invented art. Accuracy is the educational value proposition.
- **No forgery/misattribution mechanic** in MVP. Explicitly rejected as scope creep; may return post-MVP.
- **Content sourcing:** hand-curated static dataset, not a live API. Current set: **24 public-domain paintings** with Wikimedia Commons image URLs, tagged in Russian (`titleRu`, `artistRu`, `periodRu`, `genreRu`, `factRu`) plus rarity and base price.
  - 6 epic, 10 rare, 8 common.
  - Periods include High / Early / Northern Renaissance, Dutch Golden Age, Baroque, Rococo, Neoclassicism, Romanticism, Realism, Impressionism, Post-Impressionism, Ukiyo-e, Viennese Symbolism, and others as tagged in `mvp/js/data.js`.
- Suggested sources for further curation (verify license per asset even though the game is free): Wikimedia Commons, Met Museum Open Access, Smithsonian Open Access, Rijksmuseum, Europeana.
- **MVP medium:** paintings only (2D images). Sculptures and 3D scans wait for Unity.
- Track license/attribution of every asset from the start.

---

## MVP Scope Summary (HTML/JS)

Shipped in `mvp/`:

- Core loop: masked sequential text reveal (1.5s/field), live price and commission multiplier, race-to-signal Buy (button or Space), Skip, instant race-outcome feedback, always-visible capital, correctness deferred to the report, click-to-zoom.
- Faceless AI rivals that contest every lot; delay scales with calendar day and venue.
- Collector-branch campaign: pick one named collector per day, one binary tag-matched order, personal commission modifier, authored day-by-day tags, mastery plateau, budgets with soft-cap/clawback. Trophy orders on Regular/Elite late in a branch.
- Economy: rarity feeds commission and price; +12%/field price growth; speed multiplier down to ×0.35; formula rarity × speed × fit × collector modifier; incorrect-fit +0.15 → −0.35 across 15 days.
- Bankruptcy-only failure, no day-retry, 15 calendar days, starting capital 40 000 ₽.
- Three venues (Local / Regular / Elite) **assigned by branch progress**, differing in rarity pool, lot count, budget factor, rival speed, and whether fines exist. No entry ticket, no extra Elite session on top of Regular.
- Two permanent upgrades and one next-day insurance booster.
- Familiar-lot marker for previously purchased works.
- Curated static dataset of 24 real public-domain paintings, in Russian.
- Collector-branch design editor (`gamedesign.html` + `design-server.js`).

Exclude (deferred to Unity, or rejected for MVP):

- Sculptures / 3D scans / 3D auction room.
- Voice-over / audio narration.
- Named/personalized AI rivals.
- Forgery/misattribution.
- Live museum API integration.
- Player-chosen venue, Elite paid ticket, or a second venue session on the same day — the original brief had these; the implemented campaign uses branch-derived venue instead.
- Daily rating/score or per-collector persistent reputation/mood — rejected, not deferred.
- Multiple simultaneous orders per day — the engine still *can* attribute across several orders, but the day builder always creates one.

**MVP success criterion** (informal): a 10–15 minute session should feel tense and make the player want "just one more day," and a playtester should be able to finish a 15-day run without bankruptcy by a 2nd or 3rd campaign attempt, with visible improvement in buying speed/accuracy.
