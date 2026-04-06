#!/usr/bin/env bash
# install.sh
# Simplerr Linux Installer
# Usage: curl -fsSL https://raw.githubusercontent.com/phellams/simplerr/main/install.sh | bash

set -euo pipefail

# ── Constants ────────────────────────────────────────────────────────────────

REPO="phellams/simplerr"
BINARY_NAME="simplerr"
INSTALL_DIR="${HOME}/.local/bin"
CONFIG_DIR="${HOME}/.phellams/simplerr"
GITHUB_API="https://api.github.com/repos/${REPO}/releases/latest"

# ── Colours ──────────────────────────────────────────────────────────────────

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GREY='\033[0;90m'
RESET='\033[0m'

# ── Helpers ──────────────────────────────────────────────────────────────────

header() {
    echo ""
    echo -e "${CYAN}  ____  _                 _             ${RESET}"
    echo -e "${CYAN} / ___|(_)_ __ ___  _ __ | | ___ _ __  ${RESET}"
    echo -e "${CYAN} \\___ \\| | '_ ' _ \\| '_ \\| |/ _ \\ '__| ${RESET}"
    echo -e "${CYAN}  ___) | | | | | | | |_) | |  __/ |    ${RESET}"
    echo -e "${CYAN} |____/|_|_| |_| |_| .__/|_|\\___|_|    ${RESET}"
    echo -e "${CYAN}                   |_|                  ${RESET}"
    echo ""
    echo -e "  Media Suite Manager — Linux Installer"
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

    # OS
    if [[ "$(uname -s)" != "Linux" ]]; then
        fail "This installer is for Linux only."
    fi
    success "Linux detected"

    # Required tools
    for tool in curl grep sed cut tar; do
        if command_exists "$tool"; then
            success "$tool found"
        else
            fail "$tool is required but not installed."
        fi
    done

    # Internet
    if curl -fsSL --max-time 5 https://github.com > /dev/null 2>&1; then
        success "Internet connectivity OK"
    else
        fail "Cannot reach GitHub. Check your internet connection."
    fi

    # Docker
    if command_exists "docker"; then
        success "Docker found"

        # Check if user can run docker without sudo
        if docker ps &>/dev/null; then
            success "Docker accessible without sudo"
        else
            warn "Docker requires sudo. Add yourself to the docker group:"
            warn "  sudo usermod -aG docker \$USER && newgrp docker"
        fi
    else
        warn "Docker not found. Install Docker for container management."
        warn "  https://docs.docker.com/engine/install/"
    fi

    # ~/.local/bin on PATH
    if [[ ":$PATH:" != *":${INSTALL_DIR}:"* ]]; then
        warn "${INSTALL_DIR} is not on your PATH — will be added to shell rc."
    fi
}

# ── Architecture ──────────────────────────────────────────────────────────────

detect_arch() {
    local arch
    arch="$(uname -m)"

    case "$arch" in
        x86_64)  echo "linux-x64"   ;;
        aarch64) echo "linux-arm64" ;;
        armv7l)  echo "linux-arm"   ;;
        *)       fail "Unsupported architecture: $arch" ;;
    esac
}

# ── Fetch Latest Release ──────────────────────────────────────────────────────

fetch_latest_release() {
    local arch="$1"

    step "Fetching latest release from GitHub..."

    local release_json
    release_json="$(
        curl -fsSL \
            -H "Accept: application/vnd.github.v3+json" \
            -H "User-Agent: simplerr-installer" \
            "$GITHUB_API"
    )"

    VERSION="$(echo "$release_json" | grep '"tag_name"' | \
        cut -d'"' -f4)"

    if [[ -z "$VERSION" ]]; then
        fail "Could not determine latest version. Check GitHub API rate limits."
    fi

    success "Latest version: $VERSION"

    # Find matching asset
    DOWNLOAD_URL="$(echo "$release_json" | \
        grep "browser_download_url" | \
        grep "$arch" | \
        cut -d'"' -f4 | \
        head -n1)"

    if [[ -z "$DOWNLOAD_URL" ]]; then
        fail "No binary found for $arch in release $VERSION."
    fi

    FILENAME="$(basename "$DOWNLOAD_URL")"
}

# ── Download ──────────────────────────────────────────────────────────────────

download_binary() {
    TEMP_DIR="$(mktemp -d)"
    local temp_file="${TEMP_DIR}/${FILENAME}"

    step "Downloading ${FILENAME}..."

    curl -fsSL \
        -H "User-Agent: simplerr-installer" \
        --progress-bar \
        -o "$temp_file" \
        "$DOWNLOAD_URL"

    success "Download complete"

    DOWNLOADED_FILE="$temp_file"
}

# ── Install ───────────────────────────────────────────────────────────────────

install_binary() {
    step "Installing to ${INSTALL_DIR}..."

    mkdir -p "$INSTALL_DIR"

    local dest="${INSTALL_DIR}/${BINARY_NAME}"

    # Extract if tar/zip
    if [[ "$DOWNLOADED_FILE" == *.tar.gz ]]; then
        step "Extracting archive..."
        tar -xzf "$DOWNLOADED_FILE" -C "$TEMP_DIR"

        local extracted
        extracted="$(find "$TEMP_DIR" -type f -name "$BINARY_NAME" | head -n1)"

        if [[ -z "$extracted" ]]; then
            fail "Binary not found in archive."
        fi

        cp "$extracted" "$dest"

    elif [[ "$DOWNLOADED_FILE" == *.zip ]]; then
        step "Extracting archive..."

        if ! command_exists "unzip"; then
            fail "unzip is required to extract this archive. Install it and retry."
        fi

        unzip -q "$DOWNLOADED_FILE" -d "$TEMP_DIR"

        local extracted
        extracted="$(find "$TEMP_DIR" -type f -name "$BINARY_NAME" | head -n1)"

        if [[ -z "$extracted" ]]; then
            fail "Binary not found in archive."
        fi

        cp "$extracted" "$dest"
    else
        cp "$DOWNLOADED_FILE" "$dest"
    fi

    chmod +x "$dest"
    success "Binary installed at $dest"
}

# ── PATH ──────────────────────────────────────────────────────────────────────

add_to_path() {
    if [[ ":$PATH:" == *":${INSTALL_DIR}:"* ]]; then
        success "PATH already contains ${INSTALL_DIR}"
        return
    fi

    step "Adding ${INSTALL_DIR} to PATH..."

    local shell_rc=""

    # Detect shell config file
    if [[ -n "${BASH_VERSION:-}" ]]; then
        shell_rc="${HOME}/.bashrc"
    elif [[ -n "${ZSH_VERSION:-}" ]]; then
        shell_rc="${HOME}/.zshrc"
    elif [[ -f "${HOME}/.bashrc" ]]; then
        shell_rc="${HOME}/.bashrc"
    elif [[ -f "${HOME}/.zshrc" ]]; then
        shell_rc="${HOME}/.zshrc"
    else
        warn "Could not detect shell config. Add this manually:"
        warn "  export PATH=\"\$PATH:${INSTALL_DIR}\""
        return
    fi

    # Avoid duplicate entries
    if ! grep -q "${INSTALL_DIR}" "$shell_rc" 2>/dev/null; then
        echo "" >> "$shell_rc"
        echo "# Simplerr" >> "$shell_rc"
        echo "export PATH=\"\$PATH:${INSTALL_DIR}\"" >> "$shell_rc"
        success "PATH updated in ${shell_rc}"
    else
        success "PATH entry already exists in ${shell_rc}"
    fi
}

# ── Default Config ────────────────────────────────────────────────────────────

create_default_config() {
    mkdir -p "$CONFIG_DIR"

    local config_file="${CONFIG_DIR}/config.json"

    if [[ -f "$config_file" ]]; then
        success "Existing config found — leaving untouched"
        return
    fi

    step "Creating default config at ${config_file}..."

    cat > "$config_file" << 'EOF'
{
  "wsl": {
    "enabled": false,
    "distroName": ""
  },
  "docker": {
    "composePath": "",
    "pollIntervalSeconds": 5,
    "statsPollIntervalSeconds": 3
  },
  "services": [
    { "name": "plex",        "icon": "📺", "friendlyName": "Plex Media Server" },
    { "name": "sonarr",      "icon": "📡", "friendlyName": "Sonarr (TV)"       },
    { "name": "radarr",      "icon": "🎬", "friendlyName": "Radarr (Movies)"   },
    { "name": "qbittorrent", "icon": "🔽", "friendlyName": "qBittorrent"       },
    { "name": "prowlarr",    "icon": "🔍", "friendlyName": "Prowlarr"          }
  ]
}
EOF

    success "Default config created"
}

# ── Desktop Entry (optional) ──────────────────────────────────────────────────

create_desktop_entry() {
    local applications_dir="${HOME}/.local/share/applications"

    # Only bother if desktop environment is present
    if [[ -z "${XDG_CURRENT_DESKTOP:-}" ]] && [[ -z "${DESKTOP_SESSION:-}" ]]; then
        return
    fi

    step "Creating desktop entry..."

    mkdir -p "$applications_dir"

    cat > "${applications_dir}/simplerr.desktop" << EOF
[Desktop Entry]
Name=Simplerr
Comment=Media Suite Manager
Exec=bash -c 'simplerr'
Terminal=true
Type=Application
Categories=Utility;System;
EOF

    success "Desktop entry created"
}

# ── Cleanup ───────────────────────────────────────────────────────────────────

cleanup() {
    if [[ -n "${TEMP_DIR:-}" ]] && [[ -d "$TEMP_DIR" ]]; then
        rm -rf "$TEMP_DIR"
    fi
}

# ── Uninstall ─────────────────────────────────────────────────────────────────

uninstall() {
    header
    echo -e "  ${YELLOW}Uninstalling Simplerr...${RESET}"
    echo ""

    local dest="${INSTALL_DIR}/${BINARY_NAME}"

    if [[ -f "$dest" ]]; then
        rm -f "$dest"
        success "Removed $dest"
    else
        warn "Binary not found at $dest — already removed?"
    fi

    # Remove PATH line from shell rc files
    for rc in "${HOME}/.bashrc" "${HOME}/.zshrc" "${HOME}/.profile"; do
        if [[ -f "$rc" ]] && grep -q "${INSTALL_DIR}" "$rc"; then
            # Remove the comment and export line
            sed -i '/# Simplerr/d' "$rc"
            sed -i "\|${INSTALL_DIR}|d" "$rc"
            success "Removed PATH entry from $rc"
        fi
    done

    # Remove desktop entry
    local desktop_entry="${HOME}/.local/share/applications/simplerr.desktop"
    if [[ -f "$desktop_entry" ]]; then
        rm -f "$desktop_entry"
        success "Removed desktop entry"
    fi

    echo ""
    echo -e "  ${GREY}Config kept at: ${CONFIG_DIR}${RESET}"
    echo -e "  ${GREY}Delete manually if you want a clean removal:${RESET}"
    echo -e "  ${GREY}  rm -rf ${CONFIG_DIR}${RESET}"
    echo ""
    success "Simplerr uninstalled."
}

# ── Main ──────────────────────────────────────────────────────────────────────

main() {
    trap cleanup EXIT

    header

    if [[ "${1:-}" == "--uninstall" ]]; then
        uninstall
        return
    fi

    local arch
    arch="$(detect_arch)"

    preflight_checks
    echo ""

    fetch_latest_release "$arch"
    download_binary
    install_binary
    add_to_path
    create_default_config
    create_desktop_entry

    echo ""
    echo -e "  ${CYAN}─────────────────────────────────────────${RESET}"
    success "Simplerr ${VERSION} installed!"
    echo ""
    echo -e "  Run it:"
    echo -e "  ${CYAN}  simplerr${RESET}"
    echo ""
    echo -e "  Config:"
    echo -e "  ${CYAN}  ${CONFIG_DIR}/config.json${RESET}"
    echo -e "  ${CYAN}─────────────────────────────────────────${RESET}"
    echo ""
    warn "Open a new terminal for PATH changes to take effect."
    echo ""
}

main "$@"