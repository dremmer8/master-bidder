# Auction House Specialist — Game Design Document

## Concept

An educational game disguised as an arcade reflex game. The player is an art-buying agent working the floor of an auction house. Education (art history: titles, authors, periods, genres, facts) is absorbed as a *side effect* of learning to recognize artworks fast enough to win them before rival buyers — never delivered as an explicit quiz or lesson.

**Target audience:** general public interested in art, casual players who enjoy trivia/reflex games.
**Language:** Russian (UI + content) for MVP; architecture should allow later localization.
**Release ambition:** free, public release. Treat asset licensing rigorously from day one (see Content & Licensing).

## Development Path

1. **MVP first**: browser-based (HTML/JS), 2D-only, no 3D, no voice-over.
2. **Full version**: Unity 3D, adds sculptures/3D scans, full auction-room presentation, voice-over, deeper meta-progression shop.

Do not build Unity/3D features before the HTML MVP validates the core loop.

---

## Core Gameplay Loop

1. An exhibit (lot) is presented visually.
2. The auctioneer reveals information **sequentially, as text** (MVP has no voice-over), in this fixed order: `Genre → Period → Author → Interesting Fact → Title`. Reveal is **automatic, on a timer** — not a player-driven action. Cadence is the existing ~1.5s/field as a starting point for tuning (TBD via playtesting).
3. **Price and commission are live, ticking values, visible from the start of the reveal.** Each auto-revealed field bumps the price up one step and drops the commission "speed multiplier" one step (see Economy: Dynamic Price & Commission). This replaces the old behavior where price was a hidden field revealed last — its growth over time is now the core urgency signal, so it has to be visible throughout. *(Exact visual treatment is an inferred implementation detail, not explicitly speced — flag for confirmation during build.)*
4. At any point during the reveal, the player can signal "Buy!".
5. **Buying is a deterministic race**: whoever signals first gets the lot. There is still no bidding war between the player and rivals — the only thing moving the price is the reveal-driven step growth, not competing bids.
6. **Race outcome (won/lost the lot to a rival) is shown immediately** — this must stay instant, since the game needs to move on to the next lot regardless of correctness.
7. **Whether the purchase was a "correct" match for an active client order is NOT revealed during the day at all** — no immediate feedback, no running numbers, no hints. Fully suspended until the end-of-day report, same as before.
8. **The player's running capital balance is always visible.** It is now the core survival stat (see Loss Condition), so unlike order-match correctness, it must never be hidden or deferred.
9. Player is seated in the audience (fixed camera position by game logic) but can zoom in at any time to inspect the exhibit more closely.

### Feedback Timing (important, non-obvious)

Three distinct signals must not be conflated in implementation:
- **Race resolution** (did I get the item, or did a rival?) — always instant/real-time.
- **Running capital balance** — always instant/visible, continuously.
- **Order-match correctness** (was this the right item for a client, and what did it earn/cost?) — always deferred to the end-of-day report, with zero interim feedback of any kind (no colors, no icons, no partial numbers).

This split is deliberate: it keeps the auction pacing snappy and the survival stakes legible in real time, while still making the end-of-day report the informational climax of the day.

---

## Rival Bidders (AI)

- All lots are contested by all bots — no lot goes uncontested. Deliberate simplification for pacing/dynamism (rejected the alternative of bots having their own hidden preferences).
- Bots are faceless/unnamed for MVP (no personas). Personas may be added later, but this is explicitly deferred, not committed.
- Difficulty scaling: bots get faster (shorter effective reaction window) as the campaign progresses. Exact curve is a tuning detail, TBD via playtesting.
- **Elite auctions apply an additional speed boost on top of that day's normal rival curve** — Elite is meant to be genuinely harder, not just gated by a ticket price (see Venues).

---

## Orders & Collectors (Clients)

- Each day, the player has **1–3 active orders** from collectors, drawn from a pool of **10–12 lots** presented that day. **At least one order is always active** whenever the player is buying — a purchase can never end up with "nobody to sell it to."
- Collectors are **named, recurring characters with distinct tastes** (e.g., "obsessed with Impressionism") — deliberate content investment for memorable order criteria and narrative texture.
- **Order criteria are category-based** (era / genre / author / style), or a trophy order for one specific named artwork.
- **Matching is binary**: a lot is "correct" for an order only if it contains **all** of that order's requested tags. No partial credit.
- **Each collector has a personal commission modifier** — a quirk/trait that multiplies on top of the base commission formula (e.g., a generous collector pays above the base rate, a stingy one below, one who "loves speed" rewards fast buys extra). This is what makes two "correct" purchases of the same rarity worth different money for two different collectors.
- **Each order carries a budget**, credited to the player's single balance the moment the order is accepted (see Economy: Orders & Budget).
- **There is no fixed "wanted quantity."** A collector is simply happier — and pays more total commission — the more correct purchases they get, bounded only by their budget (soft cap, see Economy).
- **Conflict resolution**: if a lot matches more than one active order, the game randomly credits exactly one interested collector, as before. If a lot matches **none** of the active orders, it is still auto-attributed (via the same resolution logic) as an incorrect purchase against one of the active orders — the player is never left holding a painting nobody takes (see Economy: paintings are never kept by the player).

---

## Economy

This is the trickiest part of the design — read carefully, it has been substantially reworked from the original MVP model.

### One unified balance

- There is exactly **one** player-owned capital balance. It persists across the whole campaign (no daily reset), and there is **no separate "client money" pool anymore** — during live bidding the player is always spending their own real capital.
- **Purchases are blocked if they would exceed the current balance** — no buying on credit, ever. This is the reason a purchase itself can never be the direct cause of bankruptcy (see Loss Condition).

### Orders & Budget

- Accepting an order **immediately adds that order's budget to the player's balance.**
- At end-of-day settlement, for each order: `leftover = max(0, budget − total price of paintings attributed to that order)`. That leftover is **clawed back out of the balance** — the collector keeps any unspent budget, exactly as they keep every painting bought against their order (right or wrong).
- This makes the order budget a **soft cap, not a hard wall**: the player can keep buying correct paintings for an order past its budget, paying the extra out of their own pooled balance. Those purchases still earn **full commission** per the formula below — they simply don't get the "free" budget cushion refunded; nothing beyond the budget comes back.
- Net effect: as long as spend stays within budget, the budget injection and its end-of-day clawback cancel out exactly. The only real profit or loss an order produces is the **commission earned** on what was bought for it.
- **The player never keeps a painting, under any circumstance.** Every purchase — correct or incorrect — ends up with whichever collector it's attributed to at settlement. Commission for correct purchases is **not** reduced just because an order's budget has been exceeded — a discount here was considered and explicitly rejected as not worth the added complexity.

### Dynamic Price & Commission (replaces static per-lot pricing)

- Each lot still has a rarity tier (common / rare / epic) with a base price for that tier.
- The field-by-field reveal now drives price and commission together: **each revealed field bumps the price up one step, and drops a single "speed multiplier" down one step.** There is exactly one multiplier governing both effects — not two independent systems. Buying early (fewer fields revealed) is cheaper and pays better; waiting for more information costs more and pays less.
- The speed multiplier has a floor (**~×0.3–0.4, TBD via playtesting**), so a fully-revealed lot is still worth buying — just far less profitable than a fast buy.
- Step sizes and reveal cadence: **TBD via playtesting**, starting from the existing ~1.5s/field cadence.

### Commission Formula

```
commission = rarity_value × speed_multiplier × fit_coefficient × collector_personal_modifier
```

- **`rarity_value`** — fixed per rarity tier. Rarity now feeds commission directly, in addition to still setting the base price. *(This explicitly supersedes the earlier MVP decision that rarity was price-only and had no other effect — that decision is reversed by this pack.)*
- **`speed_multiplier`** — from the reveal-step mechanic above.
- **`fit_coefficient`** — binary: full value if the lot matches the order's requested tags, a much smaller value if it doesn't.
- **`collector_personal_modifier`** — the individual collector's quirk multiplier (see Orders & Collectors).
- For **incorrect** purchases, the smaller `fit_coefficient` itself **scales down smoothly across the campaign**: early on it's still a small net-positive commission; from some later day onward (**exact day TBD via playtesting**) it crosses zero and becomes a real fine (negative commission). This smooth ramp — not a hard difficulty switch — is what turns "buying wrong" from a minor inefficiency early in the campaign into the actual bankruptcy trigger late in it.

---

## Loss Condition & Campaign Progression

*(replaces the old "Rating & Progression" section — rating is removed entirely)*

- **There is no daily rating, no pass/fail score, and no persistent reputation/mood tracked per collector.** Collectors are a pure money mechanism now — this was a deliberate simplification, considered and confirmed rather than an oversight.
- **The only failure condition is bankruptcy**: if end-of-day settlement would take the balance below zero, the run ends. There is no other way to fail.
- **Day-retry is removed.** Days no longer have their own pass/fail checkpoint — the campaign advances day to day continuously, carrying the balance forward, until either the campaign ends or the player goes bankrupt. (The old "no farming via failure" rule is moot, since there's nothing left to retry.)
- **Campaign length remains 15 days**, unchanged, as the backbone for day-based difficulty: narrowing order criteria, rising rival speed, and the Elite-tier unlock day (see Venues). Exact day thresholds for these curves: **TBD via playtesting**, same status as before this pack.

---

## Venues: Regular / Local / Elite Auctions

Three tiers of auction, differing in risk and reward — not just a cosmetic skin on the same content.

- **Regular** — today's default auction. No entry fee, always available. Content and rival speed follow the normal day-based difficulty curve.
- **Local** — hobbyist/amateur collectors. Always available for the **entire campaign**, not just an early-game tutorial — it remains a useful safe harbor even late in the run. No entry fee. **The fine mechanic does not exist here**: a wrong purchase pays a small but never-negative commission, so the balance can never be pushed toward bankruptcy at a Local auction. Content is deliberately easier across the board, not just risk-free: lower rarity pool, simpler single-tag order criteria, slower/gentler rival timing.
- **Elite** — unlocks at a specific later campaign day (**exact day TBD via playtesting**; a global progression gate, not a per-player unlock). Requires a **one-time paid entry ticket per attempt** — an *additional* session on top of that day's Regular auction, not a replacement for it. Content is scaled up: higher rarity lot pool, bigger order budgets (bigger absolute commissions), **and** rival bidders faster/more aggressive than that day's normal curve. The ticket is the gate; the harder rivals and richer rewards are what make it genuinely "elite," not just exclusive.
- All three venues draw from and pay into the **same single balance** — there is no separate currency per venue.

---

## Money Sinks: Meta-Progression & Boosters

Both are new spends drawn from the same unified balance as everything else — every purchase here is a real bet against the bankruptcy threshold, not a side currency.

- **Meta-progression**: permanent upgrades for the player's character, kept for the rest of the run once bought. Thematically tied to the "auction agent" role (e.g., higher base speed multiplier, cheaper Elite tickets, fine resistance) — framed as mechanical skill growth rather than cosmetics, to reinforce the game's theme.
- **Boosters**: one-day, one-shot effects bought at the end of a day, applying to the *next* day only (e.g., fine immunity for a day, an extra active-order slot, more favorable reveal pacing). Cheap enough to be a repeatable tactical choice, not a rare luxury.
- Exact costs/effects for both: **TBD via playtesting.**

---

## Market Recurrence ("living market")

- The same physical artwork can be bought and sold multiple times and **may reappear in later days**, having changed hands, consistent with how a real art market behaves.
- When a previously-encountered artwork reappears, it should be **visibly marked as "familiar"** to the player (a light narrative/UI marker) — this is the mechanism intended to reinforce memorization of specific works, so it must not be silently omitted.

---

## Content & Licensing

- **All artwork data must be real**: real titles, real artists, real periods/genres, real facts. No invented or fictionalized art. This is the entire educational value proposition — accuracy is non-negotiable.
- **No forgery/misattribution mechanic** in MVP (nothing shown is ever fake or mislabeled). This was explicitly considered and rejected for MVP as scope creep; may be revisited as a post-MVP enrichment layer.
- **Content sourcing strategy**: a **hand-curated, static dataset** (not a live API pull). Rationale: factual accuracy matters too much to risk on automated enrichment from museum APIs; a curated list (target ~30–50 works for MVP) can be manually fact-checked.
- Suggested public-domain/open-license sources to curate from (needs licensing verification per asset before use, even though the game is free): Wikimedia Commons, Met Museum Open Access, Smithsonian Open Access, Rijksmuseum API, Europeana. This list is a starting point for research, not a final decision.
- **MVP content medium**: paintings only (2D images). Sculptures and 3D scans are explicitly out of scope until the Unity phase.
- Because the game is intended for **public release** (even though free/non-commercial), track the license/attribution of every chosen asset from the start — do not defer this to "later."

---

## MVP Scope Summary (HTML/JS)

Include:
- Core loop: sequential text reveal, live ticking price/commission during reveal, race-to-signal buying, instant race-outcome feedback, always-visible running balance, fully deferred correctness feedback (end-of-day report only).
- Faceless AI rivals that contest every lot; Elite auctions get an extra rival-speed boost beyond the day's normal curve.
- 1–3 named collectors per day with binary tag-matching orders, a personal commission modifier per collector, order budgets with soft-cap/clawback settlement, and no fixed wanted-quantity cap.
- Reworked economy: rarity feeds commission (not just price), reveal-driven step growth for price and decay for the speed multiplier (with a floor), the full commission formula (rarity × speed × fit × collector modifier), and an incorrect-purchase coefficient that ramps from small-positive to negative across the campaign.
- Bankruptcy-only failure condition, no day-retry, continuous day-to-day progression across a fixed 15-day campaign.
- Three venues: Regular (default), Local (always-on safe harbor, no fines, easier content), Elite (paid ticket, unlocks late, bigger/harder/richer content, additional session on top of Regular).
- Meta-progression (permanent buffs) and boosters (one-day effects) as money sinks from the single unified balance.
- Market recurrence with a "familiar lot" marker.
- Curated static dataset of ~30–50 real, licensed public-domain paintings, in Russian.
- Zoom-to-inspect on the lot image.

Exclude (explicitly deferred to Unity phase, or out of scope):
- Sculptures / 3D scans / 3D auction room.
- Voice-over / audio narration (text only in MVP).
- Named/personalized AI rivals.
- Forgery/misattribution risk mechanic.
- Any live museum API integration.
- Any daily rating/score or per-collector persistent reputation/mood beyond the personal commission modifier — explicitly rejected, not just deferred.

**MVP success criterion** (informal, no hard analytics required): a playtest session of 10–15 minutes should feel tense and make the player want to play "just one more day," and a playtester should be able to complete a full 15-day run without going bankrupt by their 2nd or 3rd attempt at the campaign, showing visible improvement in buying speed/accuracy across attempts.
