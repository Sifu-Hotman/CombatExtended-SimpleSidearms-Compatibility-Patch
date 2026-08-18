# Suite release runbook

The suite ships together: **Compatibility Patch + Loadouts Module** in one
Workshop session (Tactics is a placeholder, not part of the first release).
Everything below is staged; the only unscripted inputs are the campaign soak
and the Publish buttons.

## Gate

Real-campaign soak of both mods on the owner's 300-mod save. Automated passes
are already green (patch: cetest1–4; Loadouts: supply1–2).

## Publish order (matters — the Workshop ID dependency)

1. **Final builds + test passes** per each repo's RELEASING.md checklist.
2. **Publish the Patch** (in-game Mods → Upload). Record its new Workshop ID.
3. **Backfill the ID**: in the Loadouts repo, add
   `<steamWorkshopUrl>steam://url/CommunityFilePage/<PATCH_ID></steamWorkshopUrl>`
   to the core-patch entry in `About/About.xml` modDependencies. Commit, rebuild
   (no code change — but keep DLL/source in lockstep), push.
4. **Publish Loadouts.** Record its Workshop ID.
5. **Cross-link descriptions**: edit both Workshop listings so the suite section
   links the other mod's page (drafts in each repo's
   `docs/WORKSHOP_DESCRIPTION.bbcode` — replace the GitHub suite links with
   Workshop links once IDs exist).
6. **Tag** `v1.0.0` in both repos with release notes stating the CE + SS
   versions soaked against (and for Loadouts, the patch version).
7. **Post-publish**: PR a one-line entry to CE's `SupportedThirdPartyMods.md`
   listing the Patch (and Loadouts if their format allows), then start the
   upstream outreach tracked in issue #5.

## Prepared assets checklist (all done)

- [x] About/Preview.png in both repos (512×512, `Media/badge_gen.py`)
- [x] Workshop description drafts in both repos (`docs/WORKSHOP_DESCRIPTION.bbcode`)
- [x] Dependency IDs wired in both About.xml (patch's Workshop ID pending step 3)
- [x] LICENSE (MIT) in both repos
- [x] RELEASING.md checklists + save-compat guarantees in both repos
- [x] Badges in both READMEs; BBCode for listings inside the description drafts
