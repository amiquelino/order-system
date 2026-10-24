#!/usr/bin/env bash
# Validate that plan.md/spec.md exist for the active feature and report which
# optional design docs are available, plus the tasks template path.
set -e

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/common.sh"

eval "$(get_feature_paths)"

if [ ! -f "$IMPL_PLAN" ]; then
    echo "ERROR: plan.md not found in $FEATURE_DIR. Run /speckit-plan first." >&2
    exit 1
fi
if [ ! -f "$FEATURE_SPEC" ]; then
    echo "ERROR: spec.md not found in $FEATURE_DIR. Run /speckit-specify first." >&2
    exit 1
fi

TASKS_TEMPLATE="$REPO_ROOT/.specify/templates/tasks-template.md"
if [ ! -f "$TASKS_TEMPLATE" ]; then
    echo "ERROR: tasks template not found at $TASKS_TEMPLATE" >&2
    exit 1
fi

echo "FEATURE_DIR: $FEATURE_DIR"
echo "TASKS_TEMPLATE: $TASKS_TEMPLATE"
echo "AVAILABLE_DOCS:"
check_file "$RESEARCH" "research.md"
check_file "$DATA_MODEL" "data-model.md"
check_dir "$CONTRACTS_DIR" "contracts/"
check_file "$QUICKSTART" "quickstart.md"
