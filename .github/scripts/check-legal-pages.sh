#!/usr/bin/env bash
#
# Verifies, before the ConfigWizard is deployed, that every legal page the
# wizard links is actually live on raymigrator.com — and that both Terms of
# Use pages state the exact terms version the wizard records in its
# acceptance note.
#
# Why this gates the deploy:
#   - The wizard footer links the imprint pages. Deploying while one of them
#     404s breaks the provider identification (§ 5 DDG), which must be
#     directly reachable at all times.
#   - The pre-export consent dialog links the Terms of Use. If that link is
#     dead, users have no reasonable opportunity to read the terms and the
#     click-wrap incorporation is undermined.
#   - The exported ZIP's TERMS-ACCEPTANCE.txt records TermsVersion as the
#     accepted version. The published pages must state the same version,
#     otherwise users accept a version that is not the one published.
#
# The URLs are parsed from LocalizationService.cs (the same values the
# wizard renders and the unit tests pin) — no second list to maintain.
# The version is parsed from TermsAcceptanceService.cs.
#
# Usage: .github/scripts/check-legal-pages.sh [repo-root]

set -euo pipefail

ROOT="${1:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
LOC_FILE="$ROOT/Raycoon.RayMigrator.ConfigWizard.Web/Services/LocalizationService.cs"
TERMS_FILE="$ROOT/Raycoon.RayMigrator.ConfigWizard.Web/Services/TermsAcceptanceService.cs"

for f in "$LOC_FILE" "$TERMS_FILE"; do
    if [ ! -f "$f" ]; then
        echo "ERROR: $f not found"
        exit 1
    fi
done

TERMS_VERSION=$(sed -n 's/.*TermsVersion = "\([^"]*\)".*/\1/p' "$TERMS_FILE")
if [ -z "$TERMS_VERSION" ]; then
    echo "ERROR: could not parse TermsVersion from $TERMS_FILE"
    exit 1
fi

# All six localized legal URLs (EN dictionary first, then DE — order
# irrelevant). Plain word-split assignment instead of mapfile so the script
# also runs on macOS's bash 3.2; URLs never contain whitespace.
ALL_URLS=($(sed -n \
    -e 's/.*\["Footer\.ImprintUrl"\] = "\([^"]*\)".*/\1/p' \
    -e 's/.*\["Footer\.PrivacyUrl"\] = "\([^"]*\)".*/\1/p' \
    -e 's/.*\["Footer\.TermsUrl"\] = "\([^"]*\)".*/\1/p' \
    "$LOC_FILE"))
# The two Terms of Use URLs — these must additionally state the terms version.
TERMS_URLS=($(sed -n 's/.*\["Footer\.TermsUrl"\] = "\([^"]*\)".*/\1/p' "$LOC_FILE"))

if [ "${#ALL_URLS[@]}" -ne 6 ] || [ "${#TERMS_URLS[@]}" -ne 2 ]; then
    echo "ERROR: expected 6 legal URLs (2 languages x 3 pages) and 2 terms URLs in"
    echo "       $LOC_FILE, found ${#ALL_URLS[@]} / ${#TERMS_URLS[@]}."
    echo "       The Footer.*Url localization keys changed shape — update this script."
    exit 1
fi

fail=0

for url in "${ALL_URLS[@]}"; do
    code=$(curl -sL -o /dev/null -w "%{http_code}" --retry 2 --max-time 30 "$url" || echo "000")
    if [ "$code" != "200" ]; then
        echo "ERROR: $url -> HTTP $code"
        fail=1
    else
        echo "OK:    $url (HTTP 200)"
    fi
done

for url in "${TERMS_URLS[@]}"; do
    body=$(curl -sL --retry 2 --max-time 30 "$url" || true)
    if ! printf '%s' "$body" | grep -qF "$TERMS_VERSION"; then
        echo "ERROR: $url does not state terms version '$TERMS_VERSION'."
        echo "       TERMS-ACCEPTANCE.txt in every exported ZIP records this value as"
        echo "       the accepted version — the published page must state the same."
        fail=1
    else
        echo "OK:    $url states terms version $TERMS_VERSION"
    fi
done

if [ "$fail" != "0" ]; then
    echo ""
    echo "The wizard footer and the pre-export consent dialog link exactly these"
    echo "pages. Publish or update them on raymigrator.com, then re-run the deploy."
    exit 1
fi

echo "OK: all legal pages live, terms version matches ($TERMS_VERSION)"
