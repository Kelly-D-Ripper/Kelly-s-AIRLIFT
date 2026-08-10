# GitHub publication runbook

KellysAIRLIFT currently lives inside a workspace whose parent Git remote belongs
to a different project. Do not push the parent repository. Publish from the
generated `artifacts/KellysAIRLIFT-0.14.0-SOURCE` directory or extract the
matching source ZIP into a clean directory.

## Before creating the repository

1. Choose a source/distribution licence and add it as `LICENSE`. No licence is
   assumed by this project.
2. Decide whether the new `KellysAIRLIFT` repository is public or private.
3. Run every package script, then `verify-release.ps1`.
4. Complete the private-server acceptance tests in `INSTALL-CHECKLIST.md`.

## Repository publication

From the clean source directory:

```powershell
git init
git switch -c main
git add .
git commit -m "Release Kelly's AIRLIFT 0.14.0: RAPID"
git remote add origin https://github.com/<owner>/KellysAIRLIFT.git
git push -u origin main
git tag -a v0.14.0 -m "Kelly's AIRLIFT 0.14.0: RAPID"
git push origin v0.14.0
```

Create a GitHub release for `v0.14.0`, use `docs/RELEASE-0.14.0.md` as the
release notes and attach the four runtime ZIPs plus the source ZIP from
`artifacts`. Copy each SHA-256 from the packaging output into the release.

The GitHub workflow intentionally runs only game-independent policy tests.
Compiling the BepInEx plugins requires a legal local Nuclear Option installation
and therefore must not upload game assemblies to GitHub Actions.
