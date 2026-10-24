#!/usr/bin/env bash
# Create specs/<NNN-slug>/ for a new feature, seed spec.md from the template,
# and record it as the active feature in .specify/feature.json.
#
# Usage: create-new-feature.sh <feature description>
set -e

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/common.sh"

FEATURE_DESCRIPTION="$*"
if [ -z "$(echo "$FEATURE_DESCRIPTION" | tr -d '[:space:]')" ]; then
    echo "Usage: $0 <feature description>" >&2
    exit 1
fi

REPO_ROOT=$(get_repo_root) || exit 1
SPECS_DIR="$REPO_ROOT/specs"
mkdir -p "$SPECS_DIR"

SLUG=$(slugify "$FEATURE_DESCRIPTION")
if [ -z "$SLUG" ]; then
    echo "Error: could not derive a slug from the feature description." >&2
    exit 1
fi

HIGHEST=0
if [ -d "$SPECS_DIR" ]; then
    for dir in "$SPECS_DIR"/*/; do
        [ -d "$dir" ] || continue
        name=$(basename "$dir")
        if [[ "$name" =~ ^([0-9]{3})- ]]; then
            n=$((10#${BASH_REMATCH[1]}))
            [ "$n" -gt "$HIGHEST" ] && HIGHEST=$n
        fi
    done
fi
FEATURE_NUM=$(printf "%03d" "$((HIGHEST + 1))")
FEATURE_DIR_NAME="${FEATURE_NUM}-${SLUG}"
FEATURE_DIR="$SPECS_DIR/$FEATURE_DIR_NAME"

if [ -d "$FEATURE_DIR" ]; then
    echo "Error: $FEATURE_DIR already exists." >&2
    exit 1
fi

mkdir -p "$FEATURE_DIR"

SPEC_FILE="$FEATURE_DIR/spec.md"
SPEC_TEMPLATE="$REPO_ROOT/.specify/templates/spec-template.md"
if [ -f "$SPEC_TEMPLATE" ]; then
    cp "$SPEC_TEMPLATE" "$SPEC_FILE"
else
    echo "Warning: spec template not found at $SPEC_TEMPLATE; created an empty spec.md" >&2
    touch "$SPEC_FILE"
fi

persist_feature_directory "$REPO_ROOT" "$FEATURE_DIR"

echo "REPO_ROOT: $REPO_ROOT"
echo "FEATURE_DIR: $FEATURE_DIR"
echo "SPEC_FILE: $SPEC_FILE"
echo "FEATURE_NUM: $FEATURE_NUM"
