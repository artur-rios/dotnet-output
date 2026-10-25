#!/bin/sh
# The part of the old Branch Policy workflow that yggdrasil's common branch policy does not know: a pull request from
# release/x.y.z into main must carry that version in the <Version> of every project that ci/pack.sh packs, as the
# workflow's "Validate release version" step required. yggdrasil.yml runs it as `branchPolicy`, after
# scripts/branch-policy.sh, in a small image from the root of the pull request's head checkout, with HEAD_REF and
# BASE_REF set (and HEAD_SHA, YGGDRASIL_DIR). Without them -- on your machine -- pass the two refs as arguments:
#   sh ci/branch-policy.sh release/2.2.0 main
#
# The packed projects are found the way ci/pack.sh finds them: the projects of its SOLUTION that are packable (one
# with <IsPackable>false</IsPackable>, or a Microsoft.NET.Test.Sdk reference that makes a test project unpackable by
# itself, is left out; any other project is packed, with or without a <PackageId>). Their
# <Version> is read from the project, else from the nearest Directory.Build.props up to the repository's root.
# Only a pull request into main from release/<major>.<minor>.<patch> is checked; anything else is already judged by
# scripts/branch-policy.sh. A library whose packages have versions of their own does not use this check.
set -eu
cd "$(dirname "$0")/.."

head_ref=${1:-${HEAD_REF:-}}
base_ref=${2:-${BASE_REF:-}}

case $base_ref:$head_ref in
  main:release/[0-9]*.[0-9]*.[0-9]*) ;;
  *) echo "'$head_ref' -> '$base_ref': nothing more to check."; exit 0 ;;
esac
version=${head_ref#release/}

# The solution (or project) ci/pack.sh packs.
solution=$(sed -n "s/^SOLUTION='\\(.*\\)'\$/\\1/p" ci/pack.sh)
if [ -z "$solution" ] || [ ! -f "$solution" ]; then
  echo "ci/branch-policy.sh: cannot find the solution ci/pack.sh packs (SOLUTION='$solution')" >&2
  exit 2
fi
solution_dir=$(dirname "$solution")
case $solution in
  *.csproj) projects=$solution ;;
  *) projects=$(grep -oE '"[^"]+\.csproj"' "$solution" | tr -d '"' | sed "s|[\]|/|g; s|^|$solution_dir/|") ;;
esac

# The <Version> in the text on its standard input, empty when there is none.
version_of() {
  sed -n 's|.*<Version>[[:space:]]*\([^<[:space:]]*\)[[:space:]]*</Version>.*|\1|p' | head -n 1
}

checked=0
status=0
for project in $projects; do
  [ -f "$project" ] || continue
  text=$(tr -d '\r' < "$project")
  if printf '%s\n' "$text" | grep -qE '<IsPackable>[[:space:]]*false|Microsoft\.NET\.Test\.Sdk'; then
    continue
  fi
  found=$(printf '%s\n' "$text" | version_of)
  directory=$(dirname "$project")
  while [ -z "$found" ]; do
    [ ! -f "$directory/Directory.Build.props" ] || found=$(tr -d '\r' < "$directory/Directory.Build.props" | version_of)
    [ "$directory" != . ] || break
    directory=$(dirname "$directory")
  done
  checked=$((checked + 1))
  if [ "$found" != "$version" ]; then
    echo "$project has <Version>${found:-<none>}</Version>, but this pull request releases $version. Set <Version> to $version on develop (with the changelog) and cut the release branch again." >&2
    status=1
  else
    echo "$project says $version, as the release does."
  fi
done

if [ "$checked" -eq 0 ]; then
  echo "ci/branch-policy.sh: no packable project in $solution (IsPackable, PackageId)" >&2
  exit 2
fi
exit $status
