#!/usr/bin/env bash
# Packs the library's NuGet packages into artifacts/ (`publish.nuget` in yggdrasil.yml: run in the pinned .NET SDK
# image on the release agent, from the checkout of the version tag, with no credential at all; the packages
# `artifacts/*.nupkg` are pushed afterwards by scripts/nuget-push.sh). It ports the packing steps of the
# library's publish-package.yml. Pushing is not done here: the NuGet key never reaches repository code.
#
# RELEASE_VERSION is the tag without a leading v, set by the pipeline for a release: N.N.N, or N.N.N-pre for a
# prerelease; anything else stops the script before anything is built. Every package must then be named exactly
# <PackageId>.<RELEASE_VERSION>.nupkg, as the old workflow's check that the tag matched the project's <Version> did:
# a tag 2.1.0 on a project that says 2.0.9 packs a 2.0.9 package, and nothing is published. The package ids are
# those of the packable projects of the solution, found as ci/branch-policy.sh finds them (every project that sets
# no <IsPackable>false</IsPackable> and does not use Microsoft.NET.Test.Sdk, which makes a test project unpackable by
# itself; a project without a <PackageId> is its file's name). A library whose packages have versions of their own (a multi-package solution) must change that check.
set -euo pipefail

SOLUTION='src/ArturRios.Output.sln'

case $SOLUTION in
  *'<'*) echo "ci/pack.sh: set SOLUTION at the top of the script to the library's solution (it still has the placeholder $SOLUTION)" >&2; exit 2 ;;
esac

# The repository's root, wherever this is run from.
cd "$(dirname "${BASH_SOURCE[0]}")/.."

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# The package ids of the packable projects of $SOLUTION, one per line.
package_ids() {
  local solution_dir project text id
  solution_dir=$(dirname "$SOLUTION")
  case $SOLUTION in
    *.csproj) printf '%s\n' "$SOLUTION" ;;
    *) grep -oE '"[^"]+\.csproj"' "$SOLUTION" | tr -d '"' | sed "s|[\]|/|g; s|^|$solution_dir/|" ;;
  esac | while IFS= read -r project; do
    [ -f "$project" ] || continue
    text=$(tr -d '\r' < "$project")
    if printf '%s\n' "$text" | grep -qE '<IsPackable>[[:space:]]*false|Microsoft\.NET\.Test\.Sdk'; then
      continue
    fi
    id=$(printf '%s\n' "$text" | sed -n 's|.*<PackageId>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*</PackageId>.*|\1|p' | head -n 1)
    printf '%s\n' "${id:-$(basename "$project" .csproj)}"
  done
}

if [ -n "${RELEASE_VERSION:-}" ]; then
  version_pattern='^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$'
  if ! [[ $RELEASE_VERSION =~ $version_pattern ]]; then
    echo "ci/pack.sh: RELEASE_VERSION '$RELEASE_VERSION' is not a version (N.N.N or N.N.N-pre): is the tag one?" >&2
    exit 1
  fi
fi

# Only what this run packs is in artifacts/ (the test check's results are kept elsewhere under it).
rm -f artifacts/*.nupkg artifacts/*.snupkg
dotnet restore "$SOLUTION"
dotnet pack "$SOLUTION" --configuration Release --no-restore --output artifacts

shopt -s nullglob
packages=(artifacts/*.nupkg)
if [ "${#packages[@]}" -eq 0 ]; then
  echo "ci/pack.sh: nothing was packed: no project of $SOLUTION is packable (IsPackable, PackageId)" >&2
  exit 1
fi

if [ -n "${RELEASE_VERSION:-}" ]; then
  ids=()
  while IFS= read -r id; do ids+=("$id"); done < <(package_ids)
  if [ "${#ids[@]}" -eq 0 ]; then
    echo "ci/pack.sh: cannot tell which package ids to expect: no packable project in $SOLUTION (IsPackable, PackageId)" >&2
    exit 1
  fi
  for package in "${packages[@]}"; do
    matched=0
    for id in "${ids[@]}"; do
      if [ "${package##*/}" = "$id.$RELEASE_VERSION.nupkg" ]; then matched=1; fi
    done
    if [ "$matched" -eq 0 ]; then
      echo "ci/pack.sh: the tag is version $RELEASE_VERSION but $package was packed, not <PackageId>.$RELEASE_VERSION.nupkg for one of: ${ids[*]}. The project's <Version> does not match the tag" >&2
      exit 1
    fi
  done
fi
