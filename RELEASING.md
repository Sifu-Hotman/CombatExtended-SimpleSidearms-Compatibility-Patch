# Releasing

## Why there is no CI

This repo **cannot build in CI**. The compile references are the Combat Extended
and Simple Sidearms DLLs resolved from the local Steam Workshop folders
(`~/.local/share/Steam/steamapps/workshop/content/294100/`):

- **CE** (`2890901044`) is licensed CC BY-NC-SA — the NC clause rules out
  vendoring its assembly into this repo or a build image.
- **Simple Sidearms** (`927155256`) has **no published license** — no
  redistribution right at all.

So every release is a manual local build, and the built
`Assemblies/CESimpleSidearmsCompat.dll` is **committed to the repo** so that
cloning the repo (or downloading a release) yields a working mod without a
toolchain.

## Release checklist

1. **Sync upstreams.** Let Steam update CE and Simple Sidearms, then rebuild —
   a CE/SS update can silently change patched members. Fix any compile breaks
   before anything else; Harmony patch targets that moved will only surface at
   runtime, which is what the test pass below is for.

   ```bash
   dotnet build Source/CESimpleSidearmsCompat/CESimpleSidearmsCompat.csproj -c Release
   ```

2. **Automated test pass** (fresh saves against the current upstream versions,
   then all four scenarios — each writes `test/SaveData/test-results-*.json`):

   ```bash
   ./test/run-test.sh stage        # regenerate CETEST saves; quit after the letter
   ./test/run-assert.sh cetest1 CETEST-1-pickup
   ./test/run-assert.sh cetest2 CETEST-2-selection
   ./test/run-assert.sh cetest3 CETEST-3-combat
   ./test/run-assert.sh cetest4 CETEST-4-generation
   ```

   All four must report `"passed": true`. Check the game log for new errors.

3. **Manual smoke** (what the runner can't see): load a real campaign save,
   play a fight, confirm no red dev-log errors and the checks in
   `test/TESTPLAN.md` marked manual (gizmo rendering, caravan dialog, Gear tab,
   save/reload).

4. **Commit the DLL.** `Assemblies/CESimpleSidearmsCompat.dll` ships in-repo
   (see above). Commit it together with the source changes it was built from —
   never let source and committed DLL drift apart.

5. **Record upstream versions.** Note the CE and SS versions tested against in
   the release notes (CE's About.xml `<description>` carries its version
   string; SS via its Workshop changelog). Compatibility statements are only
   meaningful against pinned upstream versions.

6. **Tag and publish.**

   ```bash
   git tag vX.Y.Z && git push --tags
   gh release create vX.Y.Z --title "vX.Y.Z" --notes "<axes changed, upstream versions tested>"
   ```

   Version semantics: see the versioning & save-compat policy (issue #4).

7. **Workshop upload** — per the publishing checklist (issue #3) once the mod
   is on the Workshop; the badge BBCode for the listing is recorded there.
