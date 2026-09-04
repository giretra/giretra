import { LayoutService } from '../service/layout.service';
import { CommonModule } from '@angular/common';
import { Component, computed, effect, ElementRef, inject, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter, Subject, takeUntil } from 'rxjs';
import { AppMenu } from './app.menu';

const BREAKPOINT = 992;

@Component({
    selector: '[app-sidebar]',
    standalone: true,
    imports: [CommonModule, AppMenu, RouterModule],
    template: ` <div class="sidebar-header">
            <a [routerLink]="['/']" class="app-logo cursor-pointer">
                <img src="icon-192x192.png" alt="Giretra" class="app-logo-normal brand-icon" width="32" height="32" />
                <span class="logo">giretra</span>
            </a>
        </div>
        <div #menuContainer app-menu (scroll)="onMenuScroll()"></div>`,
    host: {
        class: 'layout-sidebar',
        '(mouseenter)': 'onMouseEnter()',
        '(mouseleave)': 'onMouseLeave()'
    }
})
export class AppSidebar implements OnInit, OnDestroy {
    layoutService = inject(LayoutService);

    router = inject(Router);

    el = inject(ElementRef);

    @ViewChild('menuContainer') menuContainer!: AppMenu;

    private timeout: any = null;

    private observer: IntersectionObserver | null = null;

    private outsideClickListener: ((event: MouseEvent) => void) | null = null;

    private scrollListener: (() => void) | null = null;

    private mediaQuery = window.matchMedia(`(min-width: ${BREAKPOINT}px)`);

    private destroy$ = new Subject<void>();

    isHorizontal = computed(() => this.layoutService.isHorizontal());

    isDrawer = computed(() => this.layoutService.layoutConfig().menuMode === 'drawer');

    isReveal = computed(() => this.layoutService.layoutConfig().menuMode === 'reveal');

    isAnchored = computed(() => this.layoutService.layoutState().anchored);

    constructor() {
        effect(() => {
            const hasOpenOverlay = this.layoutService.hasOpenOverlay();
            const mobileMenuActive = this.layoutService.layoutState().mobileMenuActive;

            if (this.layoutService.isDesktop()) {
                if (hasOpenOverlay) {
                    this.bindOutsideClickListener();
                } else {
                    this.unbindOutsideClickListener();
                }
            } else {
                if (mobileMenuActive) {
                    this.bindOutsideClickListener();
                } else {
                    this.unbindOutsideClickListener();
                }
            }
        });

        effect(() => {
            const hasOpenOverlaySubmenu = this.layoutService.hasOpenOverlaySubmenu();
            if (this.layoutService.isDesktop()) {
                if (hasOpenOverlaySubmenu) {
                    setTimeout(() => this.setupIntersectionObserver());
                } else {
                    this.unbindObserver();
                }
            }
        });

        effect(() => {
            const hasOpenOverlay = this.layoutService.hasOpenOverlay();
            if (this.layoutService.isHorizontal() && this.layoutService.isDesktop()) {
                if (hasOpenOverlay) {
                    this.bindScrollListener();
                } else {
                    this.unbindScrollListener();
                }
            } else {
                this.unbindScrollListener();
            }
        });
    }

    ngOnInit() {
        this.router.events
            .pipe(
                filter((event) => event instanceof NavigationEnd),
                takeUntil(this.destroy$)
            )
            .subscribe((event) => {
                const navEvent = event as NavigationEnd;
                this.onRouteChange(navEvent.urlAfterRedirects);
            });

        this.onRouteChange(this.router.url);

        this.mediaQuery.addEventListener('change', this.screenChangeListener);
    }

    ngOnDestroy() {
        this.destroy$.next();
        this.destroy$.complete();
        this.unbindOutsideClickListener();
        this.unbindScrollListener();
        this.unbindObserver();
        this.mediaQuery.removeEventListener('change', this.screenChangeListener);
    }

    private onRouteChange(path: string) {
        let newActivePath: string | null;

        if (this.layoutService.hasOverlaySubmenu() && this.layoutService.isDesktop()) {
            newActivePath = null;
        } else {
            newActivePath = path;
        }

        this.layoutService.layoutState.update((val) => ({
            ...val,
            activePath: newActivePath,
            overlayMenuActive: false,
            mobileMenuActive: false,
            menuHoverActive: false
        }));
    }

    private screenChangeListener = () => {
        if (this.layoutService.hasOverlaySubmenu()) {
            this.layoutService.layoutState.update((val) => ({
                ...val,
                activePath: this.layoutService.isDesktop() ? null : this.router.url,
                menuHoverActive: false
            }));
            this.unbindOutsideClickListener();
            this.unbindObserver();
        }
    };

    private bindOutsideClickListener() {
        if (!this.outsideClickListener) {
            this.outsideClickListener = (event: MouseEvent) => {
                if (this.isOutsideClicked(event)) {
                    if (this.layoutService.isDesktop()) {
                        this.layoutService.layoutState.update((val) => ({
                            ...val,
                            overlayMenuActive: false
                        }));

                        if (this.layoutService.hasOverlaySubmenu()) {
                            this.layoutService.layoutState.update((val) => ({
                                ...val,
                                activePath: null,
                                menuHoverActive: false
                            }));
                        }
                    } else {
                        this.layoutService.layoutState.update((val) => ({
                            ...val,
                            mobileMenuActive: false
                        }));
                    }
                }
            };

            document.addEventListener('click', this.outsideClickListener);
        }
    }

    private unbindOutsideClickListener() {
        if (this.outsideClickListener) {
            document.removeEventListener('click', this.outsideClickListener);
            this.outsideClickListener = null;
        }
    }

    private bindScrollListener() {
        if (!this.scrollListener) {
            this.scrollListener = () => {
                if (this.layoutService.isHorizontal() && this.layoutService.isDesktop() && this.layoutService.hasOpenOverlay()) {
                    this.layoutService.layoutState.update((val) => ({
                        ...val,
                        overlayMenuActive: false,
                        activePath: null,
                        menuHoverActive: false
                    }));
                }
            };

            document.addEventListener('scroll', this.scrollListener, true);
        }
    }

    private unbindScrollListener() {
        if (this.scrollListener) {
            document.removeEventListener('scroll', this.scrollListener, true);
            this.scrollListener = null;
        }
    }

    private isOutsideClicked(event: MouseEvent): boolean {
        const topbarButtonEl = document.querySelector('.topbar-menubutton');
        const sidebarEl = this.el.nativeElement;

        return !(sidebarEl?.isSameNode(event.target as Node) || sidebarEl?.contains(event.target as Node) || topbarButtonEl?.isSameNode(event.target as Node) || topbarButtonEl?.contains(event.target as Node));
    }

    onMouseEnter() {
        if (!this.isAnchored() && (this.isDrawer() || this.isReveal())) {
            if (this.timeout) {
                clearTimeout(this.timeout);
                this.timeout = null;
            }
            this.layoutService.layoutState.update((state) => ({
                ...state,
                sidebarExpanded: true
            }));
        }
    }

    onMouseLeave() {
        if (!this.layoutService.layoutState().anchored && !this.timeout) {
            this.timeout = setTimeout(() => {
                this.layoutService.layoutState.update((state) => ({
                    ...state,
                    sidebarExpanded: false
                }));
            }, 300);
        }
    }


    onMenuScroll() {
        if (this.menuContainer?.el.nativeElement && this.menuContainer?.el.nativeElement) {
            if (this.layoutService.isHorizontal()) {
                const scrollLeft = this.menuContainer?.el.nativeElement.scrollLeft;
                this.menuContainer.el.nativeElement.style.setProperty('--menu-scroll-x', `-${scrollLeft}px`);
            } else {
                const scrollTop = this.menuContainer?.el.nativeElement.scrollTop;
                this.menuContainer.el.nativeElement.style.setProperty('--menu-scroll-y', `-${scrollTop}px`);
            }
        }

        if (this.layoutService.hasOverlaySubmenu() && this.layoutService.isDesktop()) {
            this.layoutService.layoutState.update((val) => ({
                ...val,
                activePath: null,
                menuHoverActive: false
            }));
        }
    }

    private setupIntersectionObserver() {
        if (!this.menuContainer?.el.nativeElement) return;

        if (this.observer) {
            this.observer.disconnect();
        }

        const activeMenuItem = this.menuContainer.el.nativeElement.querySelector('.layout-root-menuitem.active-menuitem');
        if (!activeMenuItem) return;

        this.observer = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    if (this.layoutService.isDesktop() && !entry.isIntersecting && this.layoutService.hasOverlaySubmenu() && this.layoutService.layoutState().activePath) {
                        this.layoutService.layoutState.update((val) => ({
                            ...val,
                            activePath: null
                        }));
                    }
                });
            },
            {
                root: this.menuContainer.el.nativeElement,
                threshold: 0
            }
        );

        this.observer.observe(activeMenuItem);
    }

    private unbindObserver() {
        if (this.observer) {
            this.observer.disconnect();
            this.observer = null;
        }
    }
}
