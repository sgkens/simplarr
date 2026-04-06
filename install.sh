#!/usr/bin/env bash
# install.sh
# Simplerr Linux Installer
# Usage: curl -fsSL https://gitlab.com/phellams/simplerr/-/raw/main/install.sh | bash

set -euo pipefail

# ── Constants ────────────────────────────────────────────────────────────────

REPO_PATH="phellams/simplerr"
BINARY_NAME="simplerr"
INSTALL_DIR="${HOME}/.local/bin"
CONFIG_DIR="${HOME}/.phellams/simplerr"
GITLAB_API="https://gitlab.com/api/v4/projects/phellams%2Fsimplerr"

# ── Colours ──────────────────────────────────────────────────────────────────

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
RESET='\033[0m'

# ── Helpers ──────────────────────────────────────────────────────────────────

header() {
    echo -e "${CYAN}"
    echo "  ____  _                 _             "
    echo " / ___|(_)_ __ ___  _ __ | | ___ _ __  "
    echo " \___ \| | '_ ' _ \| '_ \| |/ _ \ '__| "
    echo "  ___) | | | | | | | |_) | |  __/ |    "
    echo " |____/|_|_| |_| |_| .__/|_|\___|_|    "
    echo "                   |_|                  "
    echo -e "${RESET}"
    echo "  Media Suite Manager — Linux Installer"
    echo ""
}

step()    { echo -e "  ${CYAN}-->${RESET} $*"; }
success() { echo -e "  ${GREEN}[OK]${RESET} $*"; }
warn()    { echo -e "  ${YELLOW}[!!]${RESET} $*"; }
fail()    { echo -e "  ${RED}[XX]${RESET} $*"; exit 1; }

command_exists() { command -v "$1" &>/dev/null; }

# ── Preflight ────────────────────────────────────────────────────────────────

preflight_checks() {
    step "Running preflight checks..."
    [[ "$(uname -s)" != "Linux" ]] && fail "This installer is for Linux only."
    
    for tool in curl grep sed cut tar; do
        command_exists "$tool" || fail "$tool is required but not installed."
    done

    curl -fsSL --max-time 5 https://gitlab.com > /dev/null 2>&1 || fail "Cannot reach GitLab."
}

# ── Architecture ──────────────────────────────────────────────────────────────

detect_arch() {
    local arch
    arch="$(uname -m)"
    case "$arch" in
        x86_64)  echo "linux-x64"   ;;
        *)       fail "Unsupported architecture: $arch" ;;
    esac
}

# ── Fetch Latest Release ──────────────────────────────────────────────────────

fetch_latest_release() {
    local arch="$1"
    step "Fetching latest release from GitLab..."

    VERSION=$(curl -fsSL "${GITLAB_API}/repository/tags" | grep -oP '"name":"\K[^"]+' | head -n1)
    [[ -z "$VERSION" ]] && fail "Could not determine latest version."

    success "Latest version: $VERSION"

    FILENAME="simplerr-${VERSION}-${arch}.tar.gz"
    DOWNLOAD_URL="${GITLAB_API}/packages/generic/simplarr/${VERSION}/${FILENAME}"
}

# ── Install ───────────────────────────────────────────────────────────────────

install_simplerr() {
    TEMP_DIR=$(mktemp -d)
    step "Downloading ${FILENAME}..."
    curl -fsSL -o "${TEMP_DIR}/${FILENAME}" "$DOWNLOAD_URL"

    step "Installing to ${INSTALL_DIR}..."
    mkdir -p "$INSTALL_DIR"
    tar -xzf "${TEMP_DIR}/${FILENAME}" -C "$TEMP_DIR"
    cp "${TEMP_DIR}/${BINARY_NAME}" "${INSTALL_DIR}/${BINARY_NAME}"
    chmod +x "${INSTALL_DIR}/${BINARY_NAME}"
    
    rm -rf "$TEMP_DIR"
    success "Binary installed at ${INSTALL_DIR}/${BINARY_NAME}"
}

# ── PATH ──────────────────────────────────────────────────────────────────────

add_to_path() {
    if [[ ":$PATH:" != *":${INSTALL_DIR}:"* ]]; then
        step "Adding ${INSTALL_DIR} to PATH..."
        local shell_rc="${HOME}/.bashrc"
        [[ -f "${HOME}/.zshrc" ]] && shell_rc="${HOME}/.zshrc"
        
        echo -e "\n# Simplerr\nexport PATH=\"\$PATH:${INSTALL_DIR}\"" >> "$shell_rc"
        success "PATH updated in ${shell_rc}"
    fi
}

# ── Main ──────────────────────────────────────────────────────────────────────

main() {
    header
    preflight_checks
    local arch=$(detect_arch)
    fetch_latest_release "$arch"
    install_simplerr
    add_to_path
    
    echo -e "\n${CYAN}─────────────────────────────────────────${RESET}"
    success "Simplerr ${VERSION} installed!"
    echo -e "Run 'simplerr' in a new terminal."
}

main "$@"
