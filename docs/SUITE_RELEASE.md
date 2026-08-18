# Suite release runbook

The suite ships together: **Compatibility Patch + Loadouts Module + Tactics
Module** in one Workshop session (Tactics joined the train 2026-08-18 —
feature-complete, 15/15 automated phases green). Everything below is staged;
the unscripted inputs are the campaign soak, the demo-GIF recordings, and the
Publish buttons.

## Gate

Real-campaign soak of both mods on the owner's 300-mod save. Automated passes
are already green (patch: cetest1–4; Loadouts: supply1–2).

## Publish order (matters — the Workshop ID dependency)

1. **Final builds + test passes** per each repo's RELEASING.md checklist.
2. **Record the demo GIFs** (owner, one per mod, using the staged test saves —
   scene prep documented in each repo's RELEASING.md "Demo scene" section):
   - Patch: CETEST-3-combat (CQC melee draw / warmup swap).
   - Loadouts: SUPPLY-1-loadout-sidearms (loadout → sidearms + ammo fetch).
   - Tactics: TACT-1-reload-abort (mid-reload swap under threat).
   Commit each clip to that repo's `Media/` (raw GitHub links animate in Steam
   descriptions); swap it into the description draft's DEMO GIF slot and embed
   in the README.
3. **Publish the Patch** (in-game Mods → Upload). Record its new Workshop ID.
4. **Backfill the ID**: in the Loadouts AND Tactics repos, add
   `<steamWorkshopUrl>steam://url/CommunityFilePage/<PATCH_ID></steamWorkshopUrl>`
   to the core-patch entry in `About/About.xml` modDependencies. Commit, rebuild
   (no code change — but keep DLL/source in lockstep), push.
5. **Publish Loadouts, then Tactics.** Record their Workshop IDs.
6. **Cross-link descriptions**: edit both Workshop listings so the suite section
   links the other mod's page (drafts in each repo's
   `docs/WORKSHOP_DESCRIPTION.bbcode` — replace the GitHub suite links with
   Workshop links once IDs exist).
7. **Tag** `v1.0.0` in all three repos with release notes stating the CE + SS
   versions soaked against (and for Loadouts, the patch version).
8. **Post-publish**: PR a one-line entry to CE's `SupportedThirdPartyMods.md`
   listing the Patch (and Loadouts if their format allows), then start the
   upstream outreach tracked in issue #5.

## Prepared assets checklist (all done)

- [x] About/Preview.png in both repos (512×512, `Media/badge_gen.py`)
- [x] Workshop description drafts in both repos (`docs/WORKSHOP_DESCRIPTION.bbcode`)
- [x] Dependency IDs wired in both About.xml (patch's Workshop ID pending step 3)
- [x] LICENSE (MIT) in both repos
- [x] RELEASING.md checklists + save-compat guarantees in both repos
- [x] Badges in both READMEs; BBCode for listings inside the description drafts
