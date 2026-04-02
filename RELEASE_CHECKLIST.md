# Release Checklist

Use this checklist when preparing a ChromaLink release package.

## Before Packaging

1. Update [VERSION](C:/Users/mrkoo/OneDrive/Documents/RIFT/Interface/AddOns/ChromaLink/VERSION) if the release version should change.
2. Update [CHANGELOG.md](C:/Users/mrkoo/OneDrive/Documents/RIFT/Interface/AddOns/ChromaLink/CHANGELOG.md) with the release summary.
3. Confirm the repo is on the intended commit and branch.

## Validation

1. Run `dotnet test .\DesktopDotNet\ChromaLink.sln`
2. Build the default package:
   `.\scripts\Package-ChromaLinkDesktop.ps1`
3. Build the self-contained package after the default package completes:
   `.\scripts\Package-ChromaLinkDesktop-SelfContained.cmd`
4. Smoke the product launcher from the desired package:
   `.\artifacts\package\Open-ChromaLink-Product.cmd`
   or
   `.\artifacts\package-selfcontained\Open-ChromaLink-Product.cmd`
5. Confirm `Status-ChromaLinkStack.cmd` reports a ready/healthy stack, then stop it.

## Release Artifacts

1. Use `artifacts\package` for framework-dependent handoff.
2. Use `artifacts\package-selfcontained` for the safer cross-machine handoff.
3. Check `package-manifest.json` for:
   - `PackageVersion`
   - `SourceCommit`
   - `SelfContained`

## Finalize

1. Push the release commit(s).
2. Create the release/tag using the version from [VERSION](C:/Users/mrkoo/OneDrive/Documents/RIFT/Interface/AddOns/ChromaLink/VERSION).
