# Riftline opening and survival upgrade

Scope: respond to the player's UI, invisible damage and dull opening feedback. Desktop Editor evidence only. Use the existing Unity AI Generator art, animation and sound library. Keep VR camera tracking independent of visual feedback.

The selected approach combines an immediately playable opening with a readable survival HUD. A cosmetic-only pass would leave the missing damage rules intact; adding an unrelated pursuit system would expand scope before fixing the current loop.

- Home: large generated hero artwork, strong title and a single primary “Run now” action selecting the next unfinished unlocked contract. Secondary contract/library/settings actions remain reachable.
- HUD: three clearly separated life segments, numeric life count, distance/score, chip streak and phase charge in compact cards. Show one relevant upcoming action with its current binding rather than six permanent instructions. Damage updates the health display immediately.
- Collision: three life points. One point per obstacle contact, 1.25 seconds of invulnerability measured in simulation time, one damage event per occupied obstacle. Damage resets the multiplier/streak; the third hit ends the run. Gaps, collapse and missed turns remain terminal. Tutorial collisions use the same visible rule; remove unlimited silent recovery.
- Opening: retain the existing learn-through-play sequence and its two-second hazard warnings. Add short crystal trails before/after lesson hazards, early phase charge and a phase challenge. Chip streaks at 5/10/20 charge phase and raise the score multiplier; missed chips/damage break the chain. Shorten only the first onboarding contract to a roughly 60-second real run, maintaining all six required actions and an archive fork.
- Feedback: damage callout with lost life, red edge accents and briefly highlighted health loss; collected crystals confirm the rising counter/streak, near misses show a short payoff. Use Generator sound assets for damage/streak; no HMD shake or visual pose manipulation.

## Execution and acceptance

1. Preserve an ordinary idle tutorial witness and a failing collision fixture before edits.
2. Implement survival and streak rules in Core. Add meaningful collision, occupied interval, pause, reset, terminal hazard and partition-invariance checks.
3. Update homepage, compact HUD and contextual prompts through existing uGUI construction. Add localized copy and bind display to Core events.
4. Add audiovisual confirmation through the existing generated sound bank and world presentation.
5. Run affected Core regressions, actual keyboard/mouse menu paths and normal-time opening/collision gameplay; capture readable life loss, success chain, failure/retry and menus.
6. Save the scene and restore temporary inputs, isolated-save hooks and Editor settings. Deliver a short video, comparison and concise report with evidence limits.
