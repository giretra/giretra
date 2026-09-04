import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, OnDestroy, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { LayoutService } from '../service/layout.service';
import { ErrorBannerService } from '../../core/services/error-banner.service';
import { PendingFriendsService } from '../../core/services/pending-friends.service';
import { AppBreadcrumb } from './app.breadcrumb';
import { AppTopbar } from './app.topbar';
import { AppFooter } from './app.footer';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, AppTopbar, RouterModule, AppBreadcrumb, AppFooter],
  template: `
    <div class="layout-wrapper" [ngClass]="containerClass()">
      <div app-topbar></div>
      <div class="layout-content-wrapper">
        <div class="layout-content">
          @if (errorBanner.message(); as message) {
            <div class="layout-error-banner" role="alert">{{ message }}</div>
          }
          <nav app-breadcrumb class="content-breadcrumb"></nav>
          <router-outlet></router-outlet>
        </div>
      </div>
      <footer app-footer></footer>
      <div class="layout-mask"></div>
    </div>
  `,
})
export class AppLayout implements OnInit, OnDestroy {
  layoutService = inject(LayoutService);
  errorBanner = inject(ErrorBannerService);
  private readonly pendingFriends = inject(PendingFriendsService);

  constructor() {
    effect(() => {
      const state = this.layoutService.layoutState();
      if (state.mobileMenuActive) {
        document.body.classList.add('blocked-scroll');
      } else {
        document.body.classList.remove('blocked-scroll');
      }
    });
  }

  ngOnInit(): void {
    this.pendingFriends.refresh();
  }

  ngOnDestroy(): void {
    // Navigating to the table while the mobile menu is open would otherwise leave the
    // body scroll-locked on a page that has no layout to release it.
    document.body.classList.remove('blocked-scroll');
  }

  containerClass = computed(() => {
    const layoutConfig = this.layoutService.layoutConfig();
    const layoutState = this.layoutService.layoutState();

    return {
      'layout-light': !layoutConfig.darkTheme,
      'layout-dark': layoutConfig.darkTheme,
      'layout-light-menu': layoutConfig.menuTheme === 'light',
      'layout-dark-menu': layoutConfig.menuTheme === 'dark',
      'layout-overlay': layoutConfig.menuMode === 'overlay',
      'layout-static': layoutConfig.menuMode === 'static',
      'layout-slim': layoutConfig.menuMode === 'slim',
      'layout-slim-plus': layoutConfig.menuMode === 'slim-plus',
      'layout-horizontal': layoutConfig.menuMode === 'horizontal',
      'layout-reveal': layoutConfig.menuMode === 'reveal',
      'layout-drawer': layoutConfig.menuMode === 'drawer',
      'layout-overlay-active': layoutState.overlayMenuActive,
      'layout-mobile-active': layoutState.mobileMenuActive,
      'layout-static-inactive':
        layoutState.staticMenuInactive && layoutConfig.menuMode === 'static',
      'layout-sidebar-expanded': layoutState.sidebarExpanded,
      'layout-sidebar-anchored': layoutState.anchored,
    };
  });
}
