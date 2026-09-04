import { LayoutService } from '../service/layout.service';
import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { TooltipModule } from 'primeng/tooltip';

@Component({
    selector: '[app-menuitem]',
    imports: [CommonModule, RouterModule, TooltipModule, TranslocoPipe],
    template: `
        @if (root() && isVisible()) {
            <div class="layout-menuitem-root-text">{{ item().label | transloco }}</div>
        }
        @if ((!hasRouterLink() || hasChildren()) && isVisible()) {
            <a
                [attr.href]="item().url"
                (click)="itemClick($event)"
                (mouseenter)="onMouseEnter()"
                [ngClass]="item().class"
                [attr.target]="item().target"
                tabindex="0"
                [pTooltip]="item().label | transloco"
                [tooltipDisabled]="!(isSlim() && root() && !isActive())"
            >
                <i [ngClass]="item().icon" class="layout-menuitem-icon"></i>
                <span class="layout-menuitem-text">{{ item().label | transloco }}</span>
                @if (hasChildren()) {
                    <i class="pi pi-fw pi-angle-down layout-submenu-toggler"></i>
                }
            </a>
        }
        @if (hasRouterLink() && !hasChildren() && isVisible()) {
            <a
                (click)="itemClick($event)"
                (mouseenter)="onMouseEnter()"
                [ngClass]="item().class"
                [routerLink]="item().routerLink"
                routerLinkActive="active-route"
                [routerLinkActiveOptions]="item().routerLinkActiveOptions || { paths: 'exact', queryParams: 'ignored', matrixParams: 'ignored', fragment: 'ignored' }"
                [fragment]="item().fragment"
                [queryParamsHandling]="item().queryParamsHandling"
                [preserveFragment]="item().preserveFragment"
                [skipLocationChange]="item().skipLocationChange"
                [replaceUrl]="item().replaceUrl"
                [state]="item().state"
                [queryParams]="item().queryParams"
                [attr.target]="item().target"
                tabindex="0"
                [pTooltip]="item().label | transloco"
                [tooltipDisabled]="!(isSlim() && root())"
            >
                <i [ngClass]="item().icon" class="layout-menuitem-icon"></i>
                <span class="layout-menuitem-text">{{ item().label | transloco }}</span>
                @if (hasChildren()) {
                    <i class="pi pi-fw pi-angle-down layout-submenu-toggler"></i>
                }
            </a>
        }
        @if (hasChildren() && isVisible()) {
            <ul [animate.enter]="initialized() ? 'p-submenu-enter' : null" [animate.leave]="'p-submenu-leave'" [class.layout-root-submenulist]="root()">
                @for (child of item().items; track child?.label) {
                    <li app-menuitem [item]="child" [root]="false" [parentPath]="fullPath()" [class]="child['badgeClass']"></li>
                }
            </ul>
        }
    `,
    host: {
        '[class.active-menuitem]': 'isActive()',
        '[class.layout-root-menuitem]': 'root()'
    },
    styles: [
        `
            .p-submenu-enter {
                animation: p-animate-submenu-expand 450ms cubic-bezier(0.86, 0, 0.07, 1) forwards;
                overflow: hidden;
            }

            .p-submenu-leave {
                animation: p-animate-submenu-collapse 450ms cubic-bezier(0.86, 0, 0.07, 1) forwards;
                overflow: hidden;
            }

            @keyframes p-animate-submenu-expand {
                from {
                    max-height: 0;
                }
                to {
                    max-height: 1000px;
                }
            }

            @keyframes p-animate-submenu-collapse {
                from {
                    max-height: 1000px;
                }
                to {
                    max-height: 0;
                }
            }
        `
    ]
})
export class AppMenuitem {
    layoutService = inject(LayoutService);

    router = inject(Router);

    item = input<any>(null);

    root = input<boolean>(true);

    parentPath = input<string | null>(null);

    isDisabled = computed(() => this.item()?.disabled ?? false);

    isSlim = computed(() => this.layoutService.layoutConfig().menuMode === 'slim');

    isHorizontal = computed(() => this.layoutService.layoutConfig().menuMode === 'horizontal');

    isSlimPlus = computed(() => this.layoutService.layoutConfig().menuMode === 'slim-plus');

    isVisible = computed(() => this.item()?.visible !== false);

    hasChildren = computed(() => this.item()?.items && this.item()?.items.length > 0);

    hasCommand = computed(() => typeof this.item()?.command === 'function');

    hasRouterLink = computed(() => !!this.item()?.routerLink);

    fullPath = computed(() => {
        const itemPath = this.item()?.path;
        if (!itemPath) return this.parentPath();
        const parent = this.parentPath();
        if (parent && !itemPath.startsWith(parent)) {
            return parent + itemPath;
        }
        return itemPath;
    });

    menuHoverActive = computed(() => this.layoutService.layoutState().menuHoverActive);

    isActive = computed(() => {
        const activePath = this.layoutService.layoutState().activePath;
        if (this.item()?.path) {
            return activePath?.startsWith(this.fullPath() ?? '') ?? false;
        }
        return false;
    });

    initialized = signal<boolean>(false);

    constructor() {
        effect(() => {
            this.updateActivePath();
        });
    }

    updateActivePath() {
        if (this.layoutService.hasOverlaySubmenu() && this.layoutService.isDesktop()) {
            return;
        }

        const item = this.item();
        const parentPath = this.parentPath();

        if (item?.routerLink && !item?.items) {
            const isRouteActive = this.router.isActive(item.routerLink[0], {
                paths: 'exact',
                queryParams: 'ignored',
                matrixParams: 'ignored',
                fragment: 'ignored'
            });

            if (isRouteActive && parentPath) {
                this.layoutService.layoutState.update((val) => ({
                    ...val,
                    activePath: parentPath
                }));
            }
        }
    }

    ngAfterViewInit() {
        setTimeout(() => {
            this.initialized.set(true);
        });
    }

    itemClick(event: Event) {
        if (this.isDisabled()) {
            event.preventDefault();
            return;
        }

        if (this.hasCommand()) {
            this.item().command({ originalEvent: event, item: this.item() });
        }

        if (this.hasChildren()) {
            if (this.isActive()) {
                const deactivateHover = this.root() && this.layoutService.hasOverlaySubmenu() && this.layoutService.isDesktop();
                this.layoutService.layoutState.update((val) => ({
                    ...val,
                    activePath: this.parentPath(),
                    menuHoverActive: deactivateHover ? false : val.menuHoverActive
                }));
            } else {
                this.layoutService.layoutState.update((val) => ({
                    ...val,
                    activePath: this.fullPath(),
                    menuHoverActive: true
                }));
            }
        } else {
            this.layoutService.layoutState.update((val) => ({
                ...val,
                overlayMenuActive: false,
                mobileMenuActive: false,
                menuHoverActive: false
            }));

            if (this.layoutService.hasOverlaySubmenu() && this.layoutService.isDesktop()) {
                this.layoutService.layoutState.update((val) => ({
                    ...val,
                    activePath: null
                }));
            }
        }
    }

    onMouseEnter() {
        if (this.layoutService.isDesktop() && this.root() && this.hasChildren() && this.menuHoverActive() && !this.isActive()) {
            this.layoutService.layoutState.update((val) => ({
                ...val,
                activePath: this.fullPath(),
                menuHoverActive: true
            }));
        }
    }
}
