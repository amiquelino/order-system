#!/usr/bin/env bash
# Seed plan.md for the active feature from the plan template, if not already present.
set -e

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/common.sh"

eval "$(get_feature_paths)"

mkdir -p "$FEATURE_DIR"

if [ -f "$IMPL_PLAN" ]; then
    echo "Plan already exists at $IMPL_PLAN, skipping template copy"
else
    PLAN_TEMPLATE="$REPO_ROOT/.specify/templates/plan-template.md"
    if [ ! -f "$PLAN_TEMPLATE" ]; then
        echo "ERROR: plan template not found at $PLAN_TEMPLATE" >&2
        exit 1
    fi
    cp "$PLAN_TEMPLATE" "$IMPL_PLAN"
    echo "Copied plan template to $IMPL_PLAN"
fi

echo "FEATURE_SPEC: $FEATURE_SPEC"
echo "IMPL_PLAN: $IMPL_PLAN"
echo "FEATURE_DIR: $FEATURE_DIR"
