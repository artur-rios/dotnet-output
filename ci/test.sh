#!/usr/bin/env bash
# The `test` check: format, build, test categories. It ports the library's run-tests.yml (the Unit tests and
# Functional tests jobs), but runs no test itself: the `coverage` check (ci/coverage.sh) runs every test, of every
# category, once, with coverage collected, and fails when one fails or skips. yggdrasil runs each check in a container
# of its own, at the same time as the others, and one check cannot read another's result, so running the suite here as
# well only ran it twice. yggdrasil.yml runs it in the pinned .NET SDK image from the repository's root; it runs the
# same on your machine with the SDK installed.
#
# A test with no Category trait (Unit or Functional) fails the check: the categories are how the suite is run and
# reported apart (CONTRIBUTING.md), so a test without one would run in none of those runs. Add other categories to
# the filter below.
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
  echo "ci/test.sh: these tests have no Category trait (Unit or Functional), so no category run would include them:" >&2
  echo "$uncategorized" >&2
  exit 1
fi
