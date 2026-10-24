#!/usr/bin/env bash
# Shared helpers for the OrderSystem Spec-Driven Development scripts.
#
# This is a deliberately trimmed-down port of spec-kit's common.sh: no git
# branches (feature identity is the specs/<NNN-slug>/ directory, tracked via
# .specify/feature.json — this repo doesn't use git), no preset/extension
# template-override stack, no multi-locale slug handling. Just enough to
# drive /speckit-specify -> /speckit-plan -> /speckit-tasks -> /speckit-implement.

# Walk upward from $1 (default: cwd) looking for a .specify/ directory.
find_repo_root() {
    local dir="${1:-$(pwd)}"
    dir="$(cd -- "$dir" 2>/dev/null && pwd)" || return 1
    local prev_dir=""
    while true; do
        if [ -d "$dir/.specify" ]; then
            echo "$dir"
            return 0
        fi
        if [ "$dir" = "/" ] || [ "$dir" = "$prev_dir" ]; then
            break
        fi
        prev_dir="$dir"
        dir="$(dirname "$dir")"
    done
    return 1
}

get_repo_root() {
    find_repo_root || {
        echo "ERROR: Could not find a .specify/ directory above $(pwd)." >&2
        return 1
    }
}

# Read .specify/feature.json's "feature_directory" value, or empty if absent/unreadable.
read_feature_directory() {
    local repo_root="$1"
    local fj="$repo_root/.specify/feature.json"
    [ -f "$fj" ] || { printf ''; return 0; }
    sed -nE 's/.*"feature_directory"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/p' "$fj" | head -n1
}

persist_feature_directory() {
    local repo_root="$1"
    local feature_dir_value="$2"
    if [[ "$feature_dir_value" == "$repo_root/"* ]]; then
        feature_dir_value="${feature_dir_value#"$repo_root/"}"
    fi
    mkdir -p "$repo_root/.specify"
    printf '{"feature_directory":"%s"}\n' "$feature_dir_value" > "$repo_root/.specify/feature.json"
}

# Prints REPO_ROOT, FEATURE_DIR, and the standard per-feature document paths as
# `eval`-able `KEY=value` lines (shell-quoted via printf %q).
get_feature_paths() {
    local repo_root
    repo_root=$(get_repo_root) || return 1

    local feature_dir
    if [ -n "${SPECIFY_FEATURE_DIRECTORY:-}" ]; then
        feature_dir="$SPECIFY_FEATURE_DIRECTORY"
        [[ "$feature_dir" != /* ]] && feature_dir="$repo_root/$feature_dir"
    else
        local fd
        fd=$(read_feature_directory "$repo_root")
        if [ -z "$fd" ]; then
            echo "ERROR: No active feature. Run the specify skill first, or set SPECIFY_FEATURE_DIRECTORY." >&2
            return 1
        fi
        feature_dir="$fd"
        [[ "$feature_dir" != /* ]] && feature_dir="$repo_root/$feature_dir"
    fi

    printf 'REPO_ROOT=%q\n' "$repo_root"
    printf 'FEATURE_DIR=%q\n' "$feature_dir"
    printf 'FEATURE_SPEC=%q\n' "$feature_dir/spec.md"
    printf 'IMPL_PLAN=%q\n' "$feature_dir/plan.md"
    printf 'TASKS=%q\n' "$feature_dir/tasks.md"
    printf 'RESEARCH=%q\n' "$feature_dir/research.md"
    printf 'DATA_MODEL=%q\n' "$feature_dir/data-model.md"
    printf 'QUICKSTART=%q\n' "$feature_dir/quickstart.md"
    printf 'CONTRACTS_DIR=%q\n' "$feature_dir/contracts"
}

check_file() { [ -f "$1" ] && echo "  [x] $2" || echo "  [ ] $2"; }
check_dir() { [ -d "$1" ] && [ -n "$(ls -A "$1" 2>/dev/null)" ] && echo "  [x] $2" || echo "  [ ] $2"; }

# A plain ASCII slug: lowercase, non-alnum -> '-', collapse/trim dashes, first 4 words.
slugify() {
    local input="$1"
    local cleaned
    cleaned=$(printf '%s' "$input" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//')
    printf '%s' "$cleaned" | cut -d'-' -f1-4
}
