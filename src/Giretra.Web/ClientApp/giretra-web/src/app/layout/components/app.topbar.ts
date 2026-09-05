import { Component, ElementRef, inject, ViewChild } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { TranslocoPipe } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { StyleClassModule } from 'primeng/styleclass';
import { LayoutService } from '../service/layout.service';
import { AuthService } from '../../core/services/auth.service';
import { PendingFriendsService } from '../../core/services/pending-friends.service';
import { LanguageSwitcherComponent } from '../../shared/components/language-switcher/language-switcher.component';
import { AppBreadcrumb } from './app.breadcrumb';
import { AppSidebar } from './app.sidebar';

@Component({
  selector: '[app-topbar]',
  standalone: true,
  imports: [
    RouterModule,
    CommonModule,
    AppSidebar,
    AppBreadcrumb,
    ButtonModule,
    StyleClassModule,
    TranslocoPipe,
    LanguageSwitcherComponent,
  ],
  template: `
    <div class="topbar-start">
      <button
        pButton
        #menubutton
        type="button"
        class="topbar-menubutton p-trigger duration-300"
        [attr.aria-label]="'layout.menu' | transloco"
        (click)="onMenuButtonClick()"
      >
        <i class="pi pi-bars"></i>
      </button>
      <a [routerLink]="['/']" class="topbar-brand">
        <img src="icon-192x192.png" alt="Giretra" class="brand-icon" width="24" height="24" />
        <span class="logo">giretra</span>
      </a>
      <nav app-breadcrumb class="topbar-breadcrumb"></nav>
    </div>
    <div class="layout-topbar-menu-section">
      <div app-sidebar></div>
    </div>
    <div class="topbar-end">
      <!-- Horizontal space is scarce on mobile: keep a single item in this row and put
           every action inside the user dropdown below. Verify at ~375px when changing. -->
      <ul class="topbar-menu">
        <li class="topbar-item">
          @if (auth.user(); as user) {
            <button
              type="button"
              class="topbar-avatar"
              [attr.aria-label]="'layout.userMenu' | transloco"
              pStyleClass="@next"
              enterFromClass="!hidden"
              enterActiveClass="animate-scalein"
              leaveToClass="!hidden"
              leaveActiveClass="animate-fadeout"
              [hideOnOutsideClick]="true"
            >
              {{ user.displayName.charAt(0) }}
              @if (pendingFriends.count() > 0) {
                <span class="badge-dot"></span>
              }
            </button>
            <ul class="!hidden topbar-menu active-topbar-menu topbar-dropdown">
              <li class="topbar-menu-header">
                <span class="topbar-menu-name">{{ user.displayName }}</span>
                @if (auth.isModerator()) {
                  <span class="topbar-menu-role">{{ 'admin.moderatorBadge' | transloco }}</span>
                }
              </li>
              <li class="topbar-menu-row">
                <span class="topbar-menu-label">{{ 'layout.language' | transloco }}</span>
                <app-language-switcher />
              </li>
              <li role="menuitem">
                <a class="topbar-menu-item" routerLink="/highlights" (click)="closeMenu()">
                  <i class="pi pi-chart-line"></i>
                  <span>{{ 'menu.highlights' | transloco }}</span>
                </a>
              </li>
              <li role="menuitem">
                <a class="topbar-menu-item" routerLink="/settings" (click)="closeMenu()">
                  <i class="pi pi-cog"></i>
                  <span>{{ 'layout.settings' | transloco }}</span>
                  @if (pendingFriends.count() > 0) {
                    <span class="topbar-menu-pill">{{ pendingFriends.count() }}</span>
                  }
                </a>
              </li>
              @if (auth.isModerator()) {
                <li role="menuitem">
                  <a class="topbar-menu-item" routerLink="/admin" (click)="closeMenu()">
                    <i class="pi pi-shield"></i>
                    <span>{{ 'layout.admin' | transloco }}</span>
                  </a>
                </li>
              }
              <li role="menuitem">
                <a class="topbar-menu-item" (click)="logout()">
                  <i class="pi pi-sign-out"></i>
                  <span>{{ 'layout.logout' | transloco }}</span>
                </a>
              </li>
            </ul>
          }
        </li>
      </ul>
    </div>
  `,
  host: {
    class: 'layout-topbar',
  },
})
export class AppTopbar {
  layoutService = inject(LayoutService);

  auth = inject(AuthService);

  pendingFriends = inject(PendingFriendsService);

  el = inject(ElementRef);

  @ViewChild('menubutton') menuButton!: ElementRef;

  @ViewChild(AppSidebar) appSidebar!: AppSidebar;

  onMenuButtonClick() {
    this.layoutService.toggleMenu();
  }

  // pStyleClass closes the dropdown on any click outside the trigger; a synthetic body
  // click after navigating keeps the panel from lingering over the next page.
  closeMenu() {
    setTimeout(() => document.body.click());
  }

  logout() {
    this.closeMenu();
    this.auth.logout();
  }
}
