import { computed, effect, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';

export interface LayoutConfig {
  preset: string;
  primary: string;
  surface: string | undefined | null;
  darkTheme: boolean;
  menuMode: string;
  menuTheme: string;
}

interface LayoutState {
  staticMenuInactive: boolean;
  overlayMenuActive: boolean;
  mobileMenuActive: boolean;
  topbarMenuActive: boolean;
  sidebarExpanded: boolean;
  menuHoverActive: boolean;
  activePath: string | null;
  anchored: boolean;
}

/**
 * Freya layout state, trimmed for Giretra: the app is dark-only (.app-dark is set on <html>
 * in index.html) and the menu mode is fixed to overlay, so the dark-mode transition, the
 * right menu and the theme configurator were removed.
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  layoutConfig = signal<LayoutConfig>({
    preset: 'Aura',
    primary: 'green',
    surface: null,
    darkTheme: true,
    menuMode: 'overlay',
    menuTheme: 'dark',
  });

  layoutState = signal<LayoutState>({
    staticMenuInactive: false,
    overlayMenuActive: false,
    mobileMenuActive: false,
    topbarMenuActive: false,
    sidebarExpanded: false,
    menuHoverActive: false,
    activePath: null,
    anchored: false,
  });

  router = inject(Router);

  isDarkTheme = computed(() => this.layoutConfig().darkTheme);

  isSlim = computed(() => this.layoutConfig().menuMode === 'slim');

  isSlimPlus = computed(() => this.layoutConfig().menuMode === 'slim-plus');

  isHorizontal = computed(() => this.layoutConfig().menuMode === 'horizontal');

  isOverlay = computed(() => this.layoutConfig().menuMode === 'overlay');

  isStatic = computed(() => this.layoutConfig().menuMode === 'static');

  hasOverlaySubmenu = computed(() => this.isSlim() || this.isSlimPlus() || this.isHorizontal());

  hasOpenOverlay = computed(
    () => this.layoutState().overlayMenuActive || this.hasOpenOverlaySubmenu(),
  );

  hasOpenOverlaySubmenu = computed(() => {
    return this.hasOverlaySubmenu() && !!this.layoutState().activePath;
  });

  isSidebarStateChanged = computed(() => {
    const layoutConfig = this.layoutConfig();
    return (
      layoutConfig.menuMode === 'horizontal' ||
      layoutConfig.menuMode === 'slim' ||
      layoutConfig.menuMode === 'slim-plus'
    );
  });

  private previousMenuMode: string | undefined = undefined;

  constructor() {
    effect(() => {
      this.updateMenuState();
    });
  }

  private updateMenuState() {
    const menuMode = this.layoutConfig().menuMode;
    if (this.previousMenuMode === undefined) {
      this.previousMenuMode = menuMode;
      return;
    }

    if (this.previousMenuMode === menuMode) {
      return;
    }

    this.previousMenuMode = menuMode;

    const isOverlaySubmenu =
      menuMode === 'slim' || menuMode === 'slim-plus' || menuMode === 'horizontal';

    this.layoutState.update((prev) => ({
      ...prev,
      staticMenuInactive: false,
      overlayMenuActive: false,
      mobileMenuActive: false,
      sidebarExpanded: false,
      menuHoverActive: false,
      anchored: false,
      activePath: this.isDesktop() ? (isOverlaySubmenu ? null : this.router.url) : prev.activePath,
    }));
  }

  toggleMenu() {
    if (this.isDesktop()) {
      if (this.layoutConfig().menuMode === 'static') {
        this.layoutState.update((prev) => ({
          ...prev,
          staticMenuInactive: !prev.staticMenuInactive,
        }));
      }

      if (this.layoutConfig().menuMode === 'overlay') {
        this.layoutState.update((prev) => ({
          ...prev,
          overlayMenuActive: !prev.overlayMenuActive,
        }));
      }
    } else {
      this.layoutState.update((prev) => ({ ...prev, mobileMenuActive: !prev.mobileMenuActive }));
    }
  }

  isDesktop() {
    return window.innerWidth > 991;
  }
}
