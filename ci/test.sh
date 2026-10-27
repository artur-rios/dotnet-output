#!/usr/bin/env bash
# The `test` check: format, build, test categories, tests. It ports the library's run-tests.yml (the Unit tests
# and Functional tests jobs). yggdrasil.yml runs it in the pinned .NET SDK image from the repository's root; it
# runs the same on your machine with the SDK installed.
#
# 1. A test with no Category trait (Unit or Functional) fails the check: CI runs the categories one by one, so a
#    test without one would run in none of them. Add other categories to the two places that name them.
# 2. Functional tests that use Testcontainers need `docker: true` on this check in yggdrasil.yml.
set -euo pipefail

SOLUTION='src/ArturRios.Output.sln'

case $SOLUTION in
  *'<'*) echo "ci/test.sh: set SOLUTION at the top of the script to the library's solution (it still has the placeholder $SOLUTION)" >&2; exit 2 ;;
esac

# The repository's root, wherever this is run from.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

dotnet restore "$SOLUTION"
dotnet format "$SOLUTION" --verify-no-changes --no-restore
dotnet build "$SOLUTION" --configuration Release --no-restore

# The tests outside every category, listed by `dotnet test --list-tests` indented by four spaces.
uncategorized=$(dotnet test "$SOLUTION" --configuration Release --no-build --list-tests \
  --filter "Category!=Unit&Category!=Functional" | sed -n 's/^    //p')
if [ -n "$uncategorized" ]; then
  echo "ci/test.sh: these tests have no Category trait (Unit or Functional), so no CI run would run them:" >&2
  echo "$uncategorized" >&2
  exit 1
fi

# One run per category, with its results (.trx) under artifacts/test-results/<category>, which the check archives.
for category in Unit Functional; do
  directory=$(printf '%s' "$category" | tr '[:upper:]' '[:lower:]')
  dotnet test "$SOLUTION" --configuration Release --no-build \
    --filter "Category=$category" \
    --logger "console;verbosity=normal" \
    --logger "trx;LogFileName=$directory-tests.trx" \
    --results-directory "artifacts/test-results/$directory"
done
