#!/usr/bin/env bash
# Generates _preview.html for the Giretra Keycloak login theme.
# Run from any directory — paths are resolved relative to this script.
#
# Usage:
#   bash docker/keycloak/themes/giretra/login/generate-preview.sh
#   # then open _preview.html in your browser

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="$SCRIPT_DIR/_preview.html"
CSS_FILE="$SCRIPT_DIR/resources/css/giretra.css"

if [[ ! -f "$CSS_FILE" ]]; then
  echo "Error: CSS file not found at $CSS_FILE" >&2
  exit 1
fi

# Inline the CSS so the preview is fully self-contained
CSS_CONTENT=$(cat "$CSS_FILE")

# Collect available images for src attributes (fallback to empty if missing)
BANNER_SRC="resources/img/giretra-banner.png"
SCREENSHOT_SRC="resources/img/belotegasy.png"
FAVICON_SRC="resources/img/favicon.ico"

for img in "$BANNER_SRC" "$SCREENSHOT_SRC"; do
  if [[ ! -f "$SCRIPT_DIR/$img" ]]; then
    echo "Warning: $img not found — preview will show a broken image" >&2
  fi
done

cat > "$OUT" << 'HEREDOC_END'
<!DOCTYPE html>
<!--
  Auto-generated preview of the Giretra Keycloak login theme.
  Regenerate with: bash generate-preview.sh
  Do not edit by hand — changes will be overwritten.
-->
<html class="giretra-html" lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Sign in to Giretra (Preview)</title>

    <link rel="icon" href="resources/img/favicon.ico" type="image/x-icon">
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">

    <style>
HEREDOC_END

# Inject the real CSS
cat "$CSS_FILE" >> "$OUT"

cat >> "$OUT" << 'HEREDOC_END'
    </style>
</head>

<body class="giretra-body" data-page-id="login.ftl">
    <div class="giretra-login">
        <div class="giretra-two-col">
            <div class="giretra-auth-col">
                <p class="giretra-tagline">A digital playground for Malagasy-style Belote</p>
                <div class="giretra-form-card">
                    <header class="giretra-form-header">
                        <h2 class="giretra-page-title">Sign in to your account</h2>
                    </header>

                    <div id="kc-content">
                        <div id="kc-content-wrapper">
                            <form class="giretra-form" onsubmit="return false;">
                                <div class="giretra-form-group">
                                    <label for="username" class="giretra-label">Username or email</label>
                                    <input id="username" name="username" type="text"
                                           class="giretra-input" placeholder="player@example.com"
                                           autofocus autocomplete="username">
                                </div>

                                <div class="giretra-form-group">
                                    <div class="giretra-label-row">
                                        <label for="password" class="giretra-label">Password</label>
                                        <a href="#" class="giretra-forgot-link">Forgot password?</a>
                                    </div>
                                    <input id="password" name="password" type="password"
                                           class="giretra-input" autocomplete="current-password">
                                </div>

                                <div class="giretra-options-row">
                                    <div class="giretra-checkbox">
                                        <input id="rememberMe" name="rememberMe" type="checkbox" class="giretra-check-input">
                                        <label for="rememberMe" class="giretra-check-label">Remember me</label>
                                    </div>
                                </div>

                                <div>
                                    <button type="submit"
                                            class="giretra-btn giretra-btn-primary giretra-btn-block giretra-btn-lg">
                                        Sign In
                                    </button>
                                </div>
                            </form>

                            <div class="giretra-social-section">
                                <div class="giretra-divider"><span>or</span></div>
                                <ul class="giretra-social-list">
                                    <li>
                                        <a href="#" class="giretra-social-btn" data-provider="google">
                                            <span class="giretra-social-icon">
                                                <svg width="20" height="20" viewBox="0 0 24 24">
                                                    <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92a5.06 5.06 0 0 1-2.2 3.32v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.1z" fill="#4285F4"/>
                                                    <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
                                                    <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
                                                    <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
                                                </svg>
                                            </span>
                                            <span class="giretra-social-name">Google</span>
                                        </a>
                                    </li>
                                    <li>
                                        <a href="#" class="giretra-social-btn" data-provider="github">
                                            <span class="giretra-social-icon">
                                                <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
                                                    <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
                                                </svg>
                                            </span>
                                            <span class="giretra-social-name">GitHub</span>
                                        </a>
                                    </li>
                                </ul>
                            </div>

                            <div class="giretra-info">
                                <div id="kc-registration">
                                    <span>No account? <a href="#">Register</a></span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <div class="giretra-engage">
                <div class="giretra-engage-content">
                    <div class="giretra-engage-card">
                        <h3 class="giretra-engage-heading">Learn the rules &amp; build a bot</h3>
                        <p class="giretra-engage-text">Discover Belote Gasy, master the strategy, and create your own AI player to compete.</p>
                        <a href="https://giretra.com" target="_blank" rel="noopener noreferrer" class="giretra-engage-btn-primary">Get started</a>
                    </div>

                    <div class="giretra-engage-card">
                        <h3 class="giretra-engage-heading">Contribute</h3>
                        <p class="giretra-engage-text">Giretra is open source. Help improve the game engine, add features, or fix bugs.</p>
                        <a href="https://github.com/haga-rak/giretra" target="_blank" rel="noopener noreferrer" class="giretra-engage-btn-outline">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor" style="vertical-align: middle; margin-right: 0.375rem;">
                                <path d="M12 .297c-6.63 0-12 5.373-12 12 0 5.303 3.438 9.8 8.205 11.385.6.113.82-.258.82-.577 0-.285-.01-1.04-.015-2.04-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.42.36.81 1.096.81 2.22 0 1.606-.015 2.896-.015 3.286 0 .315.21.69.825.57C20.565 22.092 24 17.592 24 12.297c0-6.627-5.373-12-12-12"/>
                            </svg>
                            View on GitHub
                        </a>
                    </div>

                    <div class="giretra-engage-screenshot">
                        <img src="resources/img/belotegasy.png" alt="Belote Gasy gameplay" class="giretra-screenshot-img">
                    </div>
                </div>
            </div>
        </div>

        <footer class="giretra-footer">
            <p>&copy; 2026 Giretra</p>
        </footer>
    </div>
</body>
</html>
HEREDOC_END

echo "Generated $OUT"
