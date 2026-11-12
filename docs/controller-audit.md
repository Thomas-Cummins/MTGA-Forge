# Human controller audit

Source audit performed on 2026-11-12. Compared the checked-out Forge `mtga` base
(`153f627f3e7`) with `origin/fix/private-cube-and-card-rules` (`4674cbfc413`),
and the companion bridge branch (`a8f89de`). Cube examples below come from that
branch's `PrewarIconic.dck`. These are source-confirmed gaps in routing player
decisions, not results of running the Arena client or SelfPlay.

Implementation started on the same fixes branches: protection choices now route
to the existing color picker (with explicit prompts for non-color options), and
reflected mana sources use Forge's dynamic color calculation throughout manual
actions, color selection, and auto-tap previews. Six SelfPlay recipes are added
in `tools/verify.ps1`; see `docs/rigs.md`. Java 17 syntax parsing passed. Full
compilation, runtime assertions, and real-client verification remain pending
because the local schemas and client data are absent. The findings below retain
the original audit descriptions; these two gaps have draft fixes, not verified
completion. Delve now also has a draft fix and five regression recipes: prediction
stays noninteractive, actual payment asks which cards to exile, and selected cards
are staged until the remaining mana is paid. Cancellation and an unaffordable
choice have rollback checks. Runtime verification remains pending. The other
findings remain unimplemented.

## What was checked

Compared `PlayerController`, `PlayerControllerAi`, and `ForgeBackend.MtgaController`,
then followed engine call sites and representative Forge card scripts. A missing
override alone is insufficient: an inherited method can call another method that
MtgaController already overrides. The existing Deluge and Spyglass changes do not
alter the decision paths listed below.

Source paths in this note are relative to the separate `forge/` repository:

- Arena integration: `forge-gui-mtga/src/main/java/forge/mtga/forge/ForgeBackend.java`.
- AI controller: `forge-ai/src/main/java/forge/ai/PlayerControllerAi.java`.
- Controller interface: `forge-game/src/main/java/forge/game/player/PlayerController.java`.
- Engine effects: `forge-game/src/main/java/forge/game/ability/effects/`.
- Card scripts: `forge-gui/res/cardsfolder/`.

## First regression targets

### 1. Protection choices

Cube examples: **Mother of Runes**, **Shelter**, **Apostle's Blessing**.

`ProtectEffect.resolve` calls `chooseProtectionType`. MtgaController does not
override it, so PlayerControllerAi chooses using stack and board heuristics.
The existing `chooseColor` override does not intercept this separate hook.
Apostle's Blessing also needs the non-color choice, artifacts.

Proposed rig: activate Mother of Runes with several colors available, deliberately
choose a color other than the AI's preference, and assert both that a request was
sent and that the resulting protection matches the reply. Add Apostle's Blessing
to exercise the artifact option. This fixes a family of protection choices.

### 2. Delve payment

Cube examples: **Dig Through Time**, **Treasure Cruise**.

`CostAdjustment.adjust` calls `chooseCardsToDelve`; the inherited implementation
delegates to `AiController.chooseCardsToDelve`. It selects cards automatically,
preferring noncreatures, up to the generic cost or graveyard size. The human's
`MtgaCostDecision.visit(CostExile)` does not cover this separate payment path.

Proposed rig: provide more graveyard cards than necessary, including a valuable
flashback spell, plus enough mana to choose a smaller delve payment. Assert that
only the explicitly selected cards are exiled and the remaining cost is paid
with mana. Also exercise choosing zero cards.

Implementation caution: the same cost-adjustment path runs with `test=true` for
affordability checks. Do not open interactive prompts while enumerating actions
or calculating an auto-tap preview; separate prediction from actual payment.

### 3. New targets for copied spells

Cube examples: **Dualcaster Mage**, **Expansion // Explosion**, **Thousand-Year Storm**.

MtgaController's `orderAndPlaySimultaneousSa` explicitly delegates copied
abilities to the superclass. PlayerControllerAi then calls its own
`chooseNewTargetsForCopy`, which uses `brains.doTrigger` and
`CopyTargets.awayFromTheOthers`. The human does not choose the copy's new targets.
Choosing the original spell to copy and choosing targets for the resulting copy
are separate decisions; the first can work while the second remains automated.

Proposed rig: copy a Lightning Bolt with Dualcaster Mage, keep one copy's original
target and change another to a deliberately different legal target. Assert the
requests, actual stack targets, and resulting damage. Include a case with only
the original target legal, and cancellation meaning retain the original targets.

Related gap: `chooseNewTargetsFor` itself is inherited and returns null. It is
used by general retargeting in `ChangeTargetsEffect` and by
`SpellAbility.setupNewTargets`. Test a card such as Redirect separately; fixing
that hook alone will not repair the AI copy path above. Spellskite uses a
different, magnet-based path and should not be assumed broken by this finding.

### 4. Choosing a pile

Cube example: **Fact or Fiction**.

`TwoPilesEffect.resolve` uses two different hooks. Separating the revealed cards
uses `chooseCardsForEffect`, which the integration implements. Choosing which
pile to keep uses `chooseCardsPile`, which it does not. The inherited AI method
evaluates the piles and picks one without asking the human.

Proposed rig: reveal a fixed five-card library, have the opponent form known
piles, choose the pile the AI would rank lower, and assert the exact cards going
to hand and graveyard. Use two human seats to check that each request reaches
the player responsible for that decision. Do not group Gifts Ungiven into this
test merely because its text also describes an opponent's choice; trace its
own effect path first.

### 5. Choosing a player

Cube example: **True-Name Nemesis**.

`ChoosePlayerEffect` calls `chooseSingleEntityForEffect`. The integration overrides
that method, but falls back to the superclass whenever the choices include
anything other than cards. Player objects therefore go to the AI. A forced,
nonoptional choice of a single entity is intentionally handled without a prompt;
the gap concerns multiple choices or an optional choice.

Proposed rig: put True-Name Nemesis onto the battlefield, choose self rather than
the usual opponent, and assert the recorded player and resulting protection.
This verifies user control even when the AI's usual choice would be sensible.

### 6. Combat damage allocation

Cube examples: any multiple-blocker combat; **Blightsteel Colossus** also exercises
trample and infect.

`Combat` calls `assignCombatDamage`, which is inherited and delegates to
`ComputerUtilCombat.distributeAIDamage`. The human chooses attackers and blockers
but does not control this allocation through that hook. Blocker/attacker ordering
hooks are also inherited; whether they matter depends on Forge's combat rules
configuration. Do not assume legacy damage-order rules are active.

Proposed rig: attack into two blockers with more than one legal damage allocation,
choose a legal allocation different from the AI's, and assert each recipient's
damage. Extend to trample and deathtouch using Forge's active rules and legality
checks. Verify the client's AssignDamage workflow from locally decompiled code
before implementing the wire messages.

### 7. Ordering simultaneous triggers

Cube examples: multiple enters-the-battlefield triggers after **Eerie Interlude**.

MtgaController overrides `orderAndPlaySimultaneousSa`, but loops through the
incoming list directly without asking the player for its order. Its targeting
and modal-trigger handling does not provide an ordering choice. Calls delegated
to the superclass pass singleton lists, so its `orderSimultaneousSa` hook cannot
restore a choice among the original batch.

Proposed rig: return two permanents with distinct triggers together, explicitly
choose their order, and assert both stack order and reverse resolution order.
Repeat with targets to verify ordering does not replace or skip target selection.

### 8. Choosing among replacement effects

`ReplacementHandler.run` calls `chooseSingleReplacementEffect`. The integration
implements `confirmReplacementEffect`, which asks whether to apply an optional
effect, but does not implement choosing which applicable replacement comes
first. That selection remains in the AI controller.

Proposed rig: use Rest in Peace and Blightsteel Colossus as a small fixture, then
sacrifice Colossus. Exercise choosing its shuffle replacement versus the exile
replacement and assert the destination. Rest in Peace is a test fixture outside
this cube. First verify both replacements are eligible in the same replacement
layer; never offer a choice that Forge's layer rules do not allow.

## Further audit candidates

### Reflected mana sources: Fellwar Stone

Play report: Fellwar Stone was unavailable for manual tapping but could be tapped
automatically to pay for a spell. Source tracing identifies a matching mismatch:
the manual action list rejects abilities for which `producesMana` returns false.
That helper tests `AbilityManaPart.canProduce` against individual mana codes.
Fellwar Stone uses `ManaReflected` without a fixed `Produced` field; its colors
come from `CardUtil.getReflectableManaColors`, which Forge's payment logic handles
explicitly. The integration's helper and `addManaOptions` do not handle that
dynamic color set. This affects a family of reflected mana sources rather than
only Fellwar Stone. Runtime reproduction is still pending.

Proposed rig: an untapped Fellwar Stone with opposing Plains and Island must be
offered manually, produce either selected legal color, and also work for automatic
payment. With no opposing lands producing colored mana it must not be offered;
an opposing colorless-only land must not give Fellwar Stone colorless mana.
Change the opposing lands during play and verify the available colors update.
Check other reflected sources separately because their scripts can allow mana
types, including colorless, rather than only colors. Include auto-tap previews
and Full Control payment when testing a shared dynamic-color helper.

### Other decision paths

These paths also retain AI behavior, but need individual rigs before deciding
their exact implementation scope:

- **Combat taxes:** `CombatUtil.checkPropagandaEffects` calls inherited
  `payCombatCost`, which uses `ComputerUtil.playNoStack`. Cube examples include
  Ghostly Prison and Archangel of Tithes. Check payment confirmation, cancellation,
  source selection, and whether unaffordable attackers are removed correctly.
- **Resolution payments:** the integration's own `payCostToPreventEffect` asks
  whether to pay but then constructs `AiCostDecision` directly. Non-mana payment
  components can bypass the human cost-selection overrides even after consent.
- **Overloads:** only the plain min/max `chooseNumber` overload is implemented.
  The overload with a parameter map and the explicit-list overload remain AI
  controlled. `CountersRemoveEffect`, `CountersMoveEffect`, and `LifeSetEffect`
  contain relevant call sites. The parameter-map `chooseBinary` overload is also
  inherited; the implemented Boolean overload only prompts for odd/even.
- **Ability choices:** `chooseSingleSpellForEffect` only provides an Arena choice
  for Venture. Other ability selections, including those reached through inherited
  `chooseSpellAbilitiesForEffect`, still go to AI heuristics.
- **Repeated modes:** `chooseModeForAbility` explicitly falls back to the AI for
  `allowRepeat`. Add a repeatable-mode card fixture, such as a Confluence.
- **Other hooks:** counter-type selection, convoke/improvise payments, exert,
  enlist, voting, and shield distribution. `divideShield` currently inherits an
  empty-map implementation. Prioritize these by actual cards and reachable play
  modes rather than treating every inherited utility as a user-facing bug.

## Paths that should not be reported as wholly missing

- Inherited `chooseEntitiesForEffect` calls the overridden
  `chooseSingleEntityForEffect`; card selections can already reach Arena, although
  player selections hit the fallback described above.
- The integration already handles normal card selection, discarding, sacrifices,
  zone-change selections, scry/surveil, ordinary color selection, card naming,
  many optional actions, ordinary nonrepeating modes, and targeted triggers.
  This is not proof that every edge case in those implementations works.
- Auto-tapping is an intentional setting. An AI utility involved in calculating
  an auto-tap solution is not itself evidence of a missing player choice.

## Validation and next steps

### Prompt presentation: Riot

Play report: Riot appeared as a generic yes/no question instead of an explicit
counter/haste choice. Source confirms why: `CardFactoryUtil` implements Riot as
granting haste unless the player pays an `AddCounter<1/P1P1>` cost. That reaches
`MtgaController.payCostToPreventEffect`, which presents a generic optional-payment
question. Accepting adds the +1/+1 counter; declining grants haste. This is a
presentation gap even though the human makes the underlying decision.

Inspect Arena's native Riot handler and its required request/ability metadata in
the locally decompiled client before choosing a protocol mapping. Add a Riot
rig using Gruul Spellbreaker and exercise both outcomes. Do not change Forge's
counter-versus-haste semantics to improve the labels. The client reference is
absent in this checkout, so the native UI mapping is not yet established.

### Regression priorities

Named-card presentation also needs a client-reference check. The play report describes
Arena announcing a chosen card name in an emote-style bubble. Forge stores named
cards on the source permanent, but `StateSync` currently only publishes chosen
color/parity links, not named-card links. `ForgeMatch.relayUi` forwards ordinary
client emotes; the server does not explicitly announce a completed naming choice.
Verify whether Arena generates a naming bubble from selection/state metadata or
needs a separate message, and confirm the native persistent display on Spyglass.
Do not assume an ordinary chat message implements the native naming announcement.
Test both seats and distinguish the public chosen name from Spyglass's privately
revealed opponent hand. This is separate from the already implemented activation
restriction fix.

Start with protection choices: it has a direct engine call, several cards already
in the cube, and a small deterministic test surface. Add a SelfPlay assertion
that a choice request occurred as well as an assertion on the resulting game
state. Use a deliberate answer that differs from the AI preference so silent
automation cannot accidentally pass the test. Then verify the prompt in Arena
and add a row to `docs/rigs.md`.

For every family, test both human seats, forced choices, optional declines, and
appropriate cancellation. Keep hidden information private. Check prompts against
the locally decompiled client before implementing new UI workflows.

This checkout has no generated protocol schemas, decompiled client, patched
client, or `tools/cp.txt`, so no runtime or client verification was performed.
The source audit establishes the routes above; the proposed rigs remain work to
do, not passing regression coverage.
