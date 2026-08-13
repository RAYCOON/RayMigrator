#!/usr/bin/env bash
#
# Verifies that the version named in the BSL Parameters block of LICENSE.md
# matches RayMigratorVersion in Directory.Build.props.
#
# This is not cosmetic. BSL 1.1 applies separately to each version of the
# Licensed Work, and the "Licensed Work" field is what binds the Additional
# Use Grant to a specific version. If the two drift apart, a release ships a
# licence that grants rights for a version other than the one in the box.
#
# Usage: .github/scripts/check-license-version.sh [repo-root] [expected-version]
#
#   repo-root         defaults to the git top level
#   expected-version  when given (release builds pass the tag version), both
#                     LICENSE.md and Directory.Build.props must equal it. The
#                     release publish overrides the version via -p:Version=,
#                     so the tag is what actually ships.

set -euo pipefail

ROOT="${1:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
EXPECTED="${2:-}"
LICENSE_FILE="$ROOT/LICENSE.md"
PROPS_FILE="$ROOT/Directory.Build.props"

for f in "$LICENSE_FILE" "$PROPS_FILE"; do
    if [ ! -f "$f" ]; then
        echo "ERROR: $f not found"
        exit 1
    fi
done

LICENSE_VERSION=$(sed -n 's/^| Licensed Work.*RayMigrator \([0-9][0-9.]*\).*/\1/p' "$LICENSE_FILE")
PROPS_VERSION=$(sed -n 's/.*<RayMigratorVersion>\(.*\)<\/RayMigratorVersion>.*/\1/p' "$PROPS_FILE")

if [ -z "$LICENSE_VERSION" ]; then
    echo "ERROR: could not parse the Licensed Work version from LICENSE.md."
    echo "       Expected a Parameters row like: | Licensed Work | RayMigrator X.Y.Z — ... |"
    exit 1
fi

if [ -z "$PROPS_VERSION" ]; then
    echo "ERROR: could not parse <RayMigratorVersion> from Directory.Build.props."
    exit 1
fi

if [ "$LICENSE_VERSION" != "$PROPS_VERSION" ]; then
    echo "ERROR: licence version mismatch."
    echo "       LICENSE.md 'Licensed Work': $LICENSE_VERSION"
    echo "       Directory.Build.props:      $PROPS_VERSION"
    echo ""
    echo "Update the Licensed Work row in LICENSE.md to match the build version."
    echo "Also add the new version to Docs/license-change-dates.md once released."
    exit 1
fi

if [ -n "$EXPECTED" ] && [ "$LICENSE_VERSION" != "$EXPECTED" ]; then
    echo "ERROR: licence version does not match the version being released."
    echo "       LICENSE.md / Directory.Build.props: $LICENSE_VERSION"
    echo "       Release version:                    $EXPECTED"
    echo ""
    echo "The published binary would carry a licence naming a different version"
    echo "as the Licensed Work. Update LICENSE.md and Directory.Build.props."
    exit 1
fi

if [ -n "$EXPECTED" ]; then
    echo "OK: licence version matches build and release version ($LICENSE_VERSION)"
else
    echo "OK: licence version matches build version ($LICENSE_VERSION)"
fi
