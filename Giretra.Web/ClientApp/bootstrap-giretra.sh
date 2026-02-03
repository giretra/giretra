#!/usr/bin/env bash
set -euo pipefail


# =============================================================================
# Giretra — Angular + Tailwind v4 + Spartan/UI bootstrap
# =============================================================================
# Prerequisites: Node.js 20+ (https://nodejs.org)
#   check with:  node -v && npm -v
# =============================================================================

# ── 1. Angular CLI (global) ─────────────────────────────────────────────────
npm install -g @angular/cli@latest

# ── 2. Scaffold the project ─────────────────────────────────────────────────
ng new giretra-web \
  --style=css \
  --ssr=false \
  --routing=true \
  --skip-tests

cd giretra-web

# ── 3. Tailwind CSS v4 (PostCSS) ────────────────────────────────────────────
npm install tailwindcss @tailwindcss/postcss postcss

# PostCSS config — Angular picks this up automatically
cat > .postcssrc.json << 'EOF'
{
  "plugins": {
    "@tailwindcss/postcss": {}
  }
}
EOF

# Replace default styles.css with Tailwind v4 import + dark game theme
cat > src/styles.css << 'STYLES'
@layer theme, base, components, utilities;

@import "tailwindcss/theme.css" layer(theme);
@import "tailwindcss/preflight.css" layer(base);
@import "tailwindcss/utilities.css";

/* ── Giretra dark card-table theme ─────────────────────────────── */
@layer base {
  :root {
    --background: 220 20% 10%;
    --foreground: 210 40% 96%;
    --card: 220 20% 14%;
    --card-foreground: 210 40% 96%;
    --primary: 142 50% 35%;
    --primary-foreground: 0 0% 98%;
    --secondary: 220 15% 20%;
    --secondary-foreground: 210 40% 96%;
    --accent: 45 90% 55%;
    --accent-foreground: 220 20% 10%;
    --muted: 220 15% 25%;
    --muted-foreground: 215 16% 57%;
    --destructive: 0 72% 51%;
    --destructive-foreground: 0 0% 98%;
    --border: 220 15% 20%;
    --input: 220 15% 20%;
    --ring: 142 50% 35%;
    --radius: 0.5rem;
  }

  body {
    background-color: hsl(var(--background));
    color: hsl(var(--foreground));
    font-family: "Inter", system-ui, -apple-system, sans-serif;
  }
}
STYLES

# ── 4. Angular CDK (required by Spartan brain) ──────────────────────────────
npm install @angular/cdk

# ── 5. Spartan/UI CLI ───────────────────────────────────────────────────────
npm install -D @spartan-ng/cli

# Add components — the CLI will prompt for theme & border-radius on first run.
# After the interactive setup, it installs brain (npm) + copies helm (local).
npx ng g @spartan-ng/cli:ui button
npx ng g @spartan-ng/cli:ui dialog
npx ng g @spartan-ng/cli:ui tooltip
npx ng g @spartan-ng/cli:ui badge
npx ng g @spartan-ng/cli:ui sonner
npx ng g @spartan-ng/cli:ui progress
npx ng g @spartan-ng/cli:ui separator
npx ng g @spartan-ng/cli:ui toggle

# ── 6. SignalR (real-time comms with .NET backend) ───────────────────────────
npm install @microsoft/signalr

# ── 7. Inter font (add to index.html <head>) ────────────────────────────────
sed -i.bak 's|</head>|  <link rel="preconnect" href="https://fonts.googleapis.com">\n  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\n  <link href="https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap" rel="stylesheet">\n</head>|' src/index.html
rm -f src/index.html.bak

# ── 8. Clean up default boilerplate ─────────────────────────────────────────
# Wipe the default Angular welcome page
cat > src/app/app.component.ts << 'TS'
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <main class="min-h-screen flex items-center justify-center">
      <div class="text-center space-y-4">
        <h1 class="text-4xl font-bold text-[hsl(var(--primary))]">
          ♠ Giretra ♥
        </h1>
        <p class="text-[hsl(var(--muted-foreground))]">
          Belote Malagasy — table is being set up…
        </p>
      </div>
    </main>
  `,
  styles: [],
})
export class AppComponent {}
TS

# ── 9. Done ──────────────────────────────────────────────────────────────────
echo ""
echo "✅  giretra-web is ready"
echo ""
echo "  cd giretra-web"
echo "  ng serve -o"
echo ""
