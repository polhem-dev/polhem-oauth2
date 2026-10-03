#!/usr/bin/env bash
# Writes `build=false` to $GITHUB_OUTPUT when a pull request changes only `.md` files, and `build=true` otherwise.
# Every job of build-ci.yml runs it right after checkout and gates its remaining steps on the result.
#
# `build` is a required check of the branch protection on `main`, so the pull_request trigger cannot carry a
# `paths-ignore`: a required check that never starts leaves a documentation-only pull request waiting forever. The
# skip also stays inside each job rather than on a separate job that the others need, because GitHub reports a job
# skipped by `if:` as passing: a failed detection job would skip `build` and let the pull request merge. Here a failed
# detection fails the job.
#
# HEAD is the merge commit GitHub creates for the pull request, so `HEAD^1..HEAD` is exactly what the pull request
# would change on the current `main`, even when the branch is behind it. The checkout therefore needs a depth of at
# least 2. `--no-renames` lists both sides of a rename, so renaming a `.cs` file to `.md` still builds. Anything
# unexpected (another event, no merge commit, an empty list) builds.
set -euo pipefail

build=true
if [ "${GITHUB_EVENT_NAME:-}" = "pull_request" ] && git rev-parse -q --verify 'HEAD^2' > /dev/null; then
  files=$(git diff --name-only --no-renames 'HEAD^1' HEAD)
  echo "Changed files:"
  printf '%s\n' "$files"
  if [ -n "$files" ] && ! printf '%s\n' "$files" | grep -qv '\.md$'; then
    build=false
  fi
fi
echo "build=$build" >> "$GITHUB_OUTPUT"
if [ "$build" = false ]; then
  echo "Only .md files changed: skipping this job's build steps (docs-check.yml checks the documents)"
fi
