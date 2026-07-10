# BardQuest — Drum Chart Rating Methodology

> Handoff reference for continuing the rating work. Describes **how a Pro Drums chart is turned
> into a monster profile and an overall rank**, why the design evolved the way it did, the exact
> formulas in the shipped code, and the open problems worth another pass.
>
> Everything here lives in `src/mod/BardQuest.Domain/Ratings/` (pure, no Unity, no I/O). The mod
> extracts notes and calls into it; the Domain measures and derives.

---

## 1. The core idea

A chart is **not** reduced to a single difficulty number. It gets a **descriptive six-attribute
"monster profile"** (each attribute 0–10) that says *what the chart demands*, plus a separately
computed **overall Rank** (F … SSS) and a **Threat Level** (its single highest attribute).

Two charts with the same overall rank can be shaped very differently — a well-rounded B and a
one-trick B — and the profile is what carries that. The rank is the summary; the profile is the
character sheet.

The six attributes: **Strength, Endurance, Technique, Agility, Precision, Dexterity.**

---

## 2. Architecture: raw-first, two stages

The single most important structural decision. Rating is split into **MEASURE** and **DERIVE**:

```
   chart notes ──(MEASURE, expensive, cached)──▶  DrumRawMetrics  ──(DERIVE, cheap, on load)──▶  AttributeProfile ──▶ Rank
   [role,time,tick,lane]                          (17 raw numbers)                               (6 scores 0-10)
```

- **MEASURE** (`DrumChartAnalysis.Measure`) reads the note stream once and produces a cached
  `DrumRawMetrics` record of 17 raw, tuning-free numbers (peak NPS, longest kick run, independence
  rates, …). This is the slow part (parsing every chart). It is cached to
  `…/in.yarg.game/bardquest/ratingcache.bin`.
- **DERIVE** (`DrumAttributeDerivation.Derive` → `RankDerivation.Derive`) turns raw numbers into the
  0–10 attributes and the rank. This is pure arithmetic, runs on every load, and is **never cached**.

**Why this matters for you:** every scoring constant (ceilings, weights, band cutoffs) lives in the
DERIVE stage. **Changing them retunes the whole library instantly with no rescan.** You only need a
rescan (~20–30s, user-driven in-game) when you add or change a *raw measurement*. Calibrate on the
DERIVE side whenever you can.

Key files:
| File | Responsibility |
|---|---|
| `Drums/DrumRawMetrics.cs` | The 17-field cached record (source of truth) |
| `Drums/DrumChartAnalysis.cs` | MEASURE — all raw-metric algorithms |
| `Drums/DrumAttributeDerivation.cs` | DERIVE — raw → six 0–10 attributes (**tunable constants live here**) |
| `RankDerivation.cs` | DERIVE — profile → Rank (bands + boss rule) |
| `AttributeProfile.cs` | The six scores; `Sum()`, `Threat()` |
| `RatingCache.cs` | Versioned binary codec (add a raw field ⇒ new field here + a rescan) |

---

## 3. Why we rank on FIVE attributes, not six

This is the crux of the whole journey, so it's worth the detail.

We started ranking on the **sum of all six** attributes (0–60). On the real ~2.2k-drum-chart library
this produced two stubborn problems that no amount of band-tuning could fix:

1. **The library topped out at ~38/60.** Even the hardest charts (Dream Theater, Abnormality,
   Battery) couldn't reach the top ranks. Diagnosis: three attributes were *structurally incapable*
   of reaching 10 because their ceilings were set above what real charts produce (e.g. Endurance's
   `MaxKickBurstNps` is hard-clipped at 8 by its own measurement window, but the ceiling was 14).
   **Fix:** recalibrate every ceiling to the real p97–p99 of the library (see §5). This alone lifted
   the span to ~49/60.

2. **Precision and Dexterity are low-signal for a rock/metal library, and they *dragged*.** The four
   "physical" axes (Strength, Endurance, Technique, Agility) climb cleanly with real difficulty. But:
   - **Precision** measures rhythmic *complexity* (syncopation, odd meter, subdivision mixing). Most
     charts are straight 4/4, so Precision is low for them — *correctly*. But averaging a
     near-always-low axis into the rank just compressed everything.
   - **Dexterity** (kit movement) is genuinely low for metal that navigates on cymbals rather than
     toms, and rarely stressed elsewhere.

   Measured on i6 (hardest) charts: Strength/Endurance/Agility/Technique averaged 6.6–8.3, but
   Precision 4.7 and Dexterity 3.4 — together they left ~12 of 20 possible points on the table,
   capping i6 charts at A instead of S+.

**Decision (first pass): rank on the four physical axes only.** Precision and Dexterity stayed on the
monster sheet as *descriptive flavour* but didn't drag the rank. i6 charts then landed where a human
expects: 84% A+, 56% S+, with a real SSS tail.

**Dexterity's return (second pass) — now FIVE axes.** The original Dexterity was *measuring the wrong
thing*. `TomFraction + CymbalFraction` counts the *share* of hits on toms/cymbals, so a repetitive
single-tom groove (The White Stripes – "The Hardest Button to Button") maxed it at **10** despite never
ranging around the kit — the metric couldn't tell coverage from movement, and `DrumRole` collapses the
three toms and both cymbals into one voice each, so it couldn't even see a sweep. Rebuilt as
**kit-piece entropy × non-repetition** at *lane* granularity (§5), Dexterity now reads that White
Stripes chart at **1.6**, and its easy→hard slope (+2.9 across the intensity range) matches Agility's.
With a trustworthy Dexterity the six-vs-rank test was rerun: adding it costs essentially nothing in
difficulty-tracking (rank-score ↔ intensity `r` 0.804 → 0.792, inside the yardstick's own noise) while
improving recognition of the hardest charts (i6 reaching S+ **19 → 22**). So Dexterity **re-enters the
rank**, and the five lower bands were raised one point to absorb its mid-range lift (§6).

**Precision's reshape (third pass) — still descriptive-only, but now honest.** Like the old Dexterity,
Precision was *measuring the wrong thing*: it averaged three raw terms, one of which — `SubdivisionMixIndex`
(how many of {on-beat, 8th, 16th, triplet} appear *anywhere* in the chart) — sat at ~0.8 for **every**
chart, easy to brutal, because almost any chart contains some of each. That constant term pinned Precision
into a compressed **2–6 band** that never reached 10 and never went properly low, and it averaged away the
one signal that mattered. Rebuilt as **the worse of syncopation load or odd-meter load** (§5) — dropping
the dead term entirely, no rescan — Precision now spans the full **0–10** (four-on-the-floor rock reads ~1,
odd-time/syncopated charts reach 10) and climbs monotonically with difficulty (mean 1.4 → 5.4 across i0→i6).

It **stays out of the rank** — re-tested after the reshape and the answer was decisive. Its correlation
with overall difficulty is still modest (~0.37), *correctly* — timing-exactness is orthogonal to raw
difficulty (a slow, simple odd-time groove is high-Precision but low-intensity). Adding it as a sixth rank
axis *degrades* the rank on every measure: score↔intensity falls 0.792 → 0.778, and in an
identical-distribution reordering test easy charts leak to B+ (0 → 3 of 62) while *fewer* of the hardest
charts reach S+ (i6 16 → 14 of 32), shuffling 91 charts the wrong way. Unlike Dexterity — which *improved*
hard-chart recognition and so earned its place — Precision at the chart level is noise relative to
difficulty: it pulls brutal straight-16ths charts down and easy odd-time charts up. So Precision earns a
better *descriptive* axis, not a rank axis. (A prog/jazz-heavy library could flip this.) `SubdivisionMixIndex`
is now a dead raw metric (still cached, no longer read — joins the prune list).

> These axis choices are a fit to *this* (rock/metal) library, not a law. A prog/jazz-heavy library would
> push far more weight onto Precision and likely pull it into the rank.

---

## 4. The six attributes (what each describes)

| Attribute | Demands… | In the rank? |
|---|---|---|
| **Strength** | Raw speed / power — sustained and peak note density | ✅ |
| **Endurance** | Stamina — sustained double-kick runs, kick density | ✅ |
| **Technique** | Limb independence — fast *coordinated* voice changes | ✅ |
| **Agility** | Quick bursts and fills, tight note spacing | ✅ |
| **Dexterity** | Kit-ranging — breadth of piece coverage, gated by non-repetition | ✅ |
| **Precision** | Timing-exactness — the worse of syncopation load or odd-meter load | ❌ (sheet only, reshaped) |

---

## 5. The five rank attributes — exact formulas

All in `DrumAttributeDerivation.Derive`. Helpers:
`Norm(v,c) = clamp(v/c, 0, 1)`, `InvGap(g,floor) = g≤0 ? 0 : clamp((floor−g)/floor, 0, 1)`,
`Clamp01(v) = clamp(v, 0, 1)`, `Avg = arithmetic mean`. Every attribute is `10 × [term]`.

**Strength** — `10 · Avg( Norm(PeakNps, 16), Norm(AvgNps, 11), Norm(LongestDenseSectionSeconds, 30) )`

**Endurance** — `10 · Avg( Norm(KickDensity, 3.5), Norm(LongestKickRun, 28), Norm(FastestKickSpanNps, 7) )`

**Technique** — `10 · √Norm(events, 2.0)` where
`events = ResidualAltPerSec + NoCarrierAltPerSec + OffCarrierFastPerSec + 0.6·(OffCarrierPerSec − OffCarrierFastPerSec)`

Limb independence as **carrier-stripped event rates** (see §7 and `DrumChartAnalysis.Independence.cs`):
find the sustained cymbal ostinato (the "carrier"), strip it, and count what the other limbs genuinely
do against it. Rates (events/sec) are speed-weighted by construction, so a slow jazzy weave reads low
with no extra gate; the stripping is what tells "fast and independent" (Everlong, blasts, funk) from
"fast but simple" (a driven backbeat) — raw density and the old set-change `CoordinationRate` cannot.
Off-carrier work under a *slow* ostinato (shuffles, ghost figures) is real but easier — reduced weight
(0.6). The √ lifts the mid-range: raw event rates span ~40× between easy and brutal charts, far wider
than the perceived difficulty gap.

**Agility** — `10 · Avg( Norm(PeakBurstNps, 20), Norm(FastFillRate, 16), InvGap(ShortestTransitionGap, 0.20) )`

**Dexterity** — `10 · Norm( KitPieceEntropy · min(PatternVariety / 0.45, 1), 2.25 )`

Kit-*ranging*, not tom-share. `KitPieceEntropy` (§7) is the Shannon entropy (bits) of the hit
distribution across distinct kit *pieces* at **lane** granularity — the three toms and both cymbals
count separately, kick excluded — so it measures *how many pieces* the chart spreads across. But
breadth alone over-credits a repetitive multi-piece groove (White Stripes), so it is **gated by
non-repetition**: `PatternVariety` (distinct bars / total) saturates the gate at `DexVarietySat = 0.45`.
A chart scores high only when it uses many pieces **and** keeps varying. Both inputs are cached, so this
axis retunes with no rescan.

Descriptive-only (still computed, shown on the sheet, **not** in the rank):

**Precision** — `10 · max( Norm(SyncopationFraction, 0.40), Norm(OddMeterFraction, 0.50) )`

Timing-*exactness*, not rhythmic *variety*. The two raw terms both live off the pulse: `SyncopationFraction`
(notes off the strong beats — you can't coast on the pulse) and `OddMeterFraction` (the pulse itself shifts —
you must count). Combined as a **max**, so a purely odd-time groove and a purely syncopated funk groove both
read high without needing both. The old third term, `SubdivisionMixIndex`, was **dropped** — a presence-count
that sat at ~0.8 for every chart and only compressed the axis (§3, §9). Both inputs are cached, so the reshape
took no rescan.

Constants (all in `DrumAttributeDerivation`): `PeakNpsCeil 16, AvgNpsCeil 11, DenseCeil 30,
KickDensityCeil 3.5, KickRunCeil 28, FastestKickSpanCeil 7, IndependenceCeil 2.0,
OffCarrierSlowWeight 0.6, BurstCeil 20, FastFillCeil 16, FastGapFloor 0.20, KitBreadthCeil 2.25,
DexVarietySat 0.45, SyncopationCeil 0.40, OddMeterCeil 0.50`. These were fit to the p92–p99 of the real
library; treat them as calibration targets, not constants of nature.

---

## 6. Rank scoring

`RankDerivation.Derive(profile, patternVariety)`:

1. **Score** = the **sum of the five rank axes** (0–50): `Str+End+Tec+Agi+Dex`, **plus a gentle variety
   bonus** `3.33 × Stretch(PatternVariety, 0.35, 0.80)`. An all-tens chart scores 50 — that is the semantic
   anchor for SSS. The bonus is zero at/below the library-median variety (a hard chart looping one brutal
   bar — Everlong — keeps its rank; variety can only ever add) and is capped well under one band width, so
   it nudges varied charts over band edges rather than reshuffling the ladder. *(The score was previously a
   0–60 mean × 6, a fossil of the six-axis intent; with exactly five rank axes the raw sum is the honest
   scale. The move is a behaviour-preserving rescale by 5/6 — bands and the variety cap scaled together —
   leaving the library distribution all but identical.)*
2. **Band** (prestige-weighted, tightens toward the top so S/SS/SSS stay rare). These are the prior 0–60
   edges rescaled by 5/6 and rounded to integers, so SSS still means a five-axis sum of ~47 (mean ~9.4):

   | Rank | Score | | Rank | Score |
   |---|---|---|---|---|
   | F | 0–10 | | A | 34–37 |
   | E | 11–17 | | S | 38–42 |
   | D | 18–23 | | SS | 43–46 |
   | C | 24–28 | | SSS | 47–50 |
   | B | 29–33 | | | |

3. **Boss promotion.** If a chart maxes a single rank axis (**≥ 9**) *and* already ranks **B or
   higher**, promote it one rank (up to SSS). This rescues one-skill specialists (a pure-speed
   monster) whose overall breadth understates them, without inflating easy charts (the ≥B gate).

**Threat Level** = `AttributeProfile.Threat()` = the single highest attribute (0–10), across all six.
It's the monster-sheet number that distinguishes a lopsided specialist from a well-rounded chart of
the same rank. Not used in the rank itself.

---

## 7. Raw metrics reference

All measured in `DrumChartAnalysis` from a list of `(Time, DrumRole, Tick, Lane)` notes + a `SyncInfo`
(resolution + time signatures). Roles: `Kick, Snare, Tom, Cymbal (ride/crash), HiHat`. `Lane` is the
raw pad ordinal — the distinct piece, finer than `Role`, which collapses the three toms and both
cymbals; only `KitPieceEntropy` uses it.

All 17 fields are read by a live derivation — superseded/abandoned metrics were pruned (see §9).

| Raw field | Meaning | Used by |
|---|---|---|
| `AvgNps` | notes / duration | Strength |
| `PeakNps` | 90th-pct notes in a 1s sliding window | Strength |
| `LongestDenseSectionSeconds` | longest span holding ≥ 8 nps | Strength |
| `KickDensity` | kicks / duration | Endurance |
| `LongestKickRun` | longest run of kicks ≤ 0.30s apart | Endurance |
| `FastestKickSpanNps` | fastest 8-kick span, `7/(t[i+7]−t[i])` — window-free, continuous | Endurance |
| `PeakBurstNps` | peak notes in a 0.5s window | Agility |
| `FastFillRate` | peak snare+tom notes in a 0.5s window | Agility |
| `ShortestTransitionGap` | 5th-pct inter-onset gap (fastest sustained spacing) | Agility |
| `ResidualAltPerSec` | fast (≤0.16s) **carrier-stripped** voice alternations under the ostinato, /sec — {HH+K}→{HH+S} is a real K→S interleave; {HH}→{HH+K} strips to {}→{K} and does not count | Technique |
| `OffCarrierPerSec` | figure onsets **between** ostinato hits (a limb subdividing alone), /sec | Technique |
| `OffCarrierFastPerSec` | same, only under a fast (≥5 hits/s) ostinato — the Everlong signature | Technique |
| `NoCarrierAltPerSec` | fast disjoint alternations between **cymbal-free** groups (fills/solos), /sec | Technique |
| `SyncopationFraction` | fraction of notes off strong beats | Precision |
| `OddMeterFraction` | fraction of time not in 4/4 | Precision |
| `KitPieceEntropy` | Shannon entropy (bits) of hits across distinct **lanes** (kick excluded) — breadth of kit coverage | Dexterity |
| `PatternVariety` | distinct bar-signatures / total bars (loop→low, varied→high) | Rank (variety bonus) + Dexterity (non-repetition gate) |

The Technique rates share a carrier model (`DrumChartAnalysis.Independence.cs`): a **carrier** is a
sustained quasi-continuous run of one cymbal-family voice (≥8 hits, inter-onset ≤0.45s — the
timekeeping ostinato); "fast" carriers have median IOI ≤0.20s. This dodges the continuous-ostinato
trap: under a steady hi-hat every onset group contains hi-hat, so naive set-*change* checks over-fire
on plain backbeats while naive disjoint-set checks read zero on exactly the hard charts. Stripping
the carrier first, and crediting between-carrier-hit placements, measures what the figure limbs
genuinely do against the timekeeping hand.

The cache holds **only** the fields a live derivation reads. Earlier the cache kept abandoned metrics
too (the idea being raw-first lets you fold one back into a formula with no rescan) — but a dozen of them
accumulated from superseded experiments, bloating the codec and the analyzer for no benefit. They were
pruned (§9). The retune-without-rescan property is unchanged for real tuning: you reweight the *kept*
raws freely on the DERIVE side. Re-introducing a pruned metric (e.g. a fresh Precision signal for a
prog/jazz library) is a one-rescan change — the same cost as any new measurement.

Pruned in this pass (were cached, read by nothing): `SubdivisionMixIndex`, `TomFraction`, `CymbalFraction`,
`ZoneTransitionRate`, `VoiceVariety`, `TimekeepingNps`, `CoordinationRate`, `CarrierCoverage`, `CarrierNps`,
`MaxKickBurstNps`, `OffGridKickFraction`, `SimultaneousLimbRate`.

---

## 8. How to calibrate (the loop)

1. Deploy the mod (`bash scripts/deploy-sandbox.sh`) and open the BardQuest screen in-game once to
   build the cache (rescan). The human-readable `bardquest/ratings-report.tsv`
   (rank/attrs/intensity/name per Expert chart) is generated by the analysis harness from the cache
   — there is deliberately no report code in the shipped mod.
2. Analyse against the cache directly with the scratchpad tool (see below) — never ship analysis
   code in the mod.
3. Adjust DERIVE constants → rebuild + redeploy (**no rescan**) → reopen → re-check.

**The analysis harness** (kept out of the repo, in the working scratchpad): a tiny console app that
references `BardQuest.Domain`, deserializes the real `ratingcache.bin`, runs
`DrumAttributeDerivation.Derive` + `RankDerivation.Derive`, and prints distributions. Rebuild it
against Domain and it always reflects the current derivation. Re-create it as needed — it's ~40 lines.

**Prototyping RAW metrics without in-game rescans.** The harness can also replicate the mod's whole
MEASURE pipeline offline: reference the **sandbox's own** `YARG.Core.Package.dll` under
`extern alias yargpkg` (exactly like the mod — the *vendored* YARG.Core is version-skewed: different
hashes, and it doesn't elevate 4-lane CON charts to ProDrums), copy the game's `songcache.bin` to the
scratchpad, quick-load it via `CacheHandler.RunScan(tryQuickScan: true, …)`, then `entry.LoadChart()`
and mirror `DrumChartExtractor`. That reproduces the in-game cache bit-for-bit (verified 244/246
exact), so candidate raw metrics can be designed and calibrated against the full real library and the
one in-game rescan is spent only on the finished design. The sandbox's library lives in the `release`
channel dir (`…/in.yarg.game/release/settings.json` → SongFolders); copy — never touch — its files.

**The yardstick — use YARG intensity as a *diagnostic only*, never an input.** Each chart carries
YARG's own 0–6 drum-star intensity. It is *not* fed into any formula (that would defeat having our own
ratings). It is used only to sanity-check: if most i6 (hardest) charts don't land S+, or most i0–1
charts reach B+, something is wrong. A handful of disagreements is fine and expected — a busy drum
chart in a low-intensity song *should* out-rank its star (that's the point of rating drums
specifically; several i1 charts like RHCP – "Give It Away" are genuine YARG under-ratings).

---

## 9. Known limitations & open items (good targets for the next run)

- **The variety bonus trusts `PatternVariety` blindly.** Sparse-but-noodly charts (Bob Dylan –
  Tangled Up in Blue, pv 0.89) collect the full bonus even though their variation is easy to play;
  the ≥-median floor and the small cap contain the damage (one band at most). If it bothers, scale
  the bonus by the base score.
- **The carrier model only recognises cymbal-family ostinatos.** A snare-driven train beat, or
  independence woven against a *tom* ostinato, has no carrier; it can still score through
  `NoCarrierAltPerSec` but only when cymbal-free. Rare in this library; revisit for other genres.
- **Cymbal-washed chaos reads low on Technique.** Keith Moon-style fill storms (I Can See for Miles)
  carry crash hits inside the figure, so neither the carrier-stripped nor the cymbal-free branch
  credits them; they rate through Strength/Agility instead. Arguably right — it's not limb
  independence — but know the shape.
- **`SubdivisionMixIndex` was degenerate and is now pruned** (≈ 0.8 for every chart, easy to brutal — like
  the abandoned `VoiceVariety`). It was the diluter that compressed Precision; dropped from the derivation
  (§3, §5) and then removed from the cache (§7). If Precision ever needs a third signal, add a
  *discriminating* rhythmic metric (e.g. rhythmic-placement entropy), not a presence-count — a one-rescan change.
- **Precision is descriptive-only** by choice for a rock/metal library (§3); Dexterity now feeds the
  rank. The reshape was re-tested for rank inclusion and *rejected* — it degrades difficulty tracking and
  hard-chart recognition (§3). Worth re-evaluating only for a prog/jazz-heavy library, where timing-exactness
  correlates far more with real difficulty.
- **Dexterity's gate reuses `PatternVariety`**, which also drives the rank's variety bonus. They share
  signal, but play different roles (an axis vs an additive nudge), so there's no double-counting.
- **`KitPieceEntropy` is a whole-song measure.** It rewards breadth of coverage *across the chart*, not
  local ranging density — a chart that sweeps the kit hard in one section is read by its overall spread.
  A peak-window or transition-rate variant is the obvious next refinement if it matters.
- **`TomFraction`/`CymbalFraction` are pruned** (Dexterity moved to `KitPieceEntropy`) — removed from the
  cache in the dead-metric prune (§7), along with ten other superseded raws.
- **Boss rule triggers on any of the 5 rank axes at ≥9.** Technique's √ compression pushes strong charts
  toward the ≥9 zone; the event rates themselves are honest, but watch the promotion volume if you
  lower `IndependenceCeil` further.

Resolved in v2 (kept for the record): *`MaxKickBurstNps` "clipped at 8"* — the real cause was window
quantisation (values snapped to {2,4,6,8}), not unresolvable double bass; the library's genuinely
fastest feet sit at ~7.7 kicks/s, and `FastestKickSpanNps` measures that continuously.
*`CoordinationRate` false positives* — replaced by the carrier-stripped independence rates; rank ↔
YARG-intensity Spearman went 0.74 → 0.80, i0/i1 charts at B+ went 5 → 1, with all SSS references
(Battery, Visions, Panic Attack, Caught in a Mosh, Painkiller) intact.

---

## 10. Validation snapshot (246-chart sandbox, 5-axis rank, current constants)

*(Predates the dead-metric prune, but the prune is behaviour-neutral — it removed only raws no derivation
read — so a rescan reproduces these exact ratings.)*

Rank distribution: `F:2 E:21 D:60 C:57 B:31 A:34 S:22 SS:8 SSS:11` (on the 0–50 sum scale; the prior
0–60 mean scale gave `F:2 E:19 D:63 C:57 B:31 A:34 S:21 SS:7 SSS:12` — the rescale is behaviour-preserving,
shifting only ~3 charts across the D/E edge, none by more than one rank); rank-score ↔ YARG-intensity
Pearson ~0.79. Against YARG intensity as the diagnostic: i6 (hardest) → 22/32 S+ (up from 19 on the
four-axis rank); i≤1 (easiest) → 0/62 at B+. Reference charts land where expected — Everlong S
(pv 0.33 → no variety bonus), Painkiller/Battery/Abnormality/Panic Attack SSS. The White Stripes'
"The Hardest Button to Button" — the chart that motivated the Dexterity rebuild — now reads
Dexterity **1.6** (was 10) and stays a low E, no longer a broad-kit false positive. Deliberate reads:
James Brown "I Got You" (i3) rates high on Technique (funk interleaving, user-confirmed in-game);
Keith Moon's "I Can See for Miles" reads Technique-low but Strength/Endurance-high (fill chaos, not
independence).
