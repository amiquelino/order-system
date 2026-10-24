#!/usr/bin/env bash
# Validate prerequisites for the active feature and report available design docs.
#
# Usage: check-prerequisites.sh [--require-tasks] [--include-tasks] [--paths-only]
set -e

REQUIRE_TASKS=false
INCLUDE_TASKS=false
PATHS_ONLY=false

for arg in "$@"; do
    case "$arg" in
        --require-tasks) REQUIRE_TASKS=true ;;
        --include-tasks) INCLUDE_TASKS=true ;;
        --paths-only) PATHS_ONLY=true ;;
        --help|-h)
            echo "Usage: $0 [--require-tasks] [--include-tasks] [--paths-only]"
            exit 0
            ;;
        *) echo "ERROR: Unknown option '$arg'" >&2; exit 1 ;;
    esac
done

SCRIPT_DIR="$(CDPATH="" cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$SCRIPT_DIR/common.sh"

eval "$(get_feature_paths)"

if $PATHS_ONLY; then
    echo "REPO_ROOT: $REPO_ROOT"
    echo "FEATURE_DIR: $FEATURE_DIR"
    echo "FEATURE_SPEC: $FEATURE_SPEC"
    echo "IMPL_PLAN: $IMPL_PLAN"
    echo "TASKS: $TASKS"
    exit 0
fi

if [ ! -d "$FEATURE_DIR" ]; then
    echo "ERROR: Feature directory not found: $FEATURE_DIR. Run /speckit-specify first." >&2
    exit 1
fi
if [ ! -s "$IMPL_PLAN" ]; then
    echo "ERROR: plan.md missing or empty in $FEATURE_DIR. Run /speckit-plan first." >&2
    exit 1
fi
if $REQUIRE_TASKS && [ ! -f "$TASKS" ]; then
    echo "ERROR: tasks.md not found in $FEATURE_DIR. Run /speckit-tasks first." >&2
    exit 1
fi

echo "FEATURE_DIR: $FEATURE_DIR"
echo "AVAILABLE_DOCS:"
check_file "$RESEARCH" "research.md"
check_file "$DATA_MODEL" "data-model.md"
check_dir "$CONTRACTS_DIR" "contracts/"
check_file "$QUICKSTART" "quickstart.md"
$INCLUDE_TASKS && check_file "$TASKS" "tasks.md"
exit 0
