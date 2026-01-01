# Release Process

This repo uses GitHub Actions to build, publish prereleases, publish release candidates, and publish stable releases.

## Prerelease (manual)
1. In GitHub, open the repo and go to Actions.
2. Select "Publish Prerelease" in the left sidebar.
3. Click "Run workflow" on the right and pick the branch/commit.
2. Set:
   - versionPrefix: next planned version (e.g., 1.4.0)
   - versionSuffix: optional (e.g., alpha.1). Leave blank for alpha.<run_number>.
3. The workflow publishes prerelease packages to nuget.org.

## Release Candidate (manual)
1. Create a release branch from main: release/x.y (e.g., release/1.4).
2. In GitHub, open the repo and go to Actions.
3. Select "Publish Release Candidate" in the left sidebar.
4. Click "Run workflow" on the right and pick the release branch.
3. Set:
   - versionPrefix: the release version (e.g., 1.4.0)
   - versionSuffix: optional (e.g., rc.1). Leave blank for rc.<run_number>.
4. The workflow publishes RC packages to nuget.org.

## New Release (stable)
1. Merge or fast-forward the release branch into the desired commit.
2. Create and push a tag in the form vX.Y.Z (e.g., v1.4.0).
   - In GitHub, go to Code > Tags > Releases > Draft a new release.
   - Set the tag to vX.Y.Z and target the correct commit.
   - Publish the release to create/push the tag.
3. The "Publish Release" workflow runs on the tag and publishes stable packages.

## Hotfix (stable)
1. Branch from the release line you need to patch (e.g., release/1.4) or from the release tag.
2. Apply the fix, then create and push a new tag vX.Y.(Z+1).
   - In GitHub, go to Code > Tags > Releases > Draft a new release.
   - Set the tag to vX.Y.(Z+1) and target the hotfix commit.
   - Publish the release to create/push the tag.
3. The "Publish Release" workflow publishes the hotfix release.

## CI Build
- The "CI" workflow runs on pushes and PRs to main and only builds/tests.
- You can view runs under Actions > CI.
