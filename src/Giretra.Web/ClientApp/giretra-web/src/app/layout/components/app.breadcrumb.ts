import { Component, inject, signal } from '@angular/core';
import { ActivatedRouteSnapshot, NavigationEnd, Router, RouterModule } from '@angular/router';
import { filter } from 'rxjs/operators';
import { TranslocoPipe } from '@jsverse/transloco';

interface Breadcrumb {
  label: string;
  url: string;
}

@Component({
  selector: '[app-breadcrumb]',
  standalone: true,
  imports: [RouterModule, TranslocoPipe],
  template: `<ol>
    <li>
      <a [routerLink]="['/']" class="layout-breadcrumb-home">
        <i class="pi pi-home"></i>
      </a>
    </li>
    @for (item of breadcrumbs(); track item.url; let last = $last) {
      <li class="layout-breadcrumb-chevron">/</li>
      @if (last) {
        <li>{{ item.label | transloco }}</li>
      } @else {
        <li><a [routerLink]="item.url">{{ item.label | transloco }}</a></li>
      }
    }
  </ol>`,
  host: {
    class: 'layout-breadcrumb',
  },
})
export class AppBreadcrumb {
  private readonly router = inject(Router);

  readonly breadcrumbs = signal<Breadcrumb[]>([]);

  constructor() {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe(() => {
      this.rebuild();
    });
    this.rebuild();
  }

  private rebuild(): void {
    const breadcrumbs: Breadcrumb[] = [];
    this.addBreadcrumb(this.router.routerState.snapshot.root, [], breadcrumbs);
    this.breadcrumbs.set(breadcrumbs);
  }

  private addBreadcrumb(
    route: ActivatedRouteSnapshot,
    parentUrl: string[],
    breadcrumbs: Breadcrumb[],
  ) {
    const routeUrl = parentUrl.concat(route.url.map((url) => url.path));
    const breadcrumb = route.data['breadcrumb'];
    const parentBreadcrumb = route.parent?.data ? route.parent.data['breadcrumb'] : null;

    if (breadcrumb && breadcrumb !== parentBreadcrumb) {
      breadcrumbs.push({
        label: breadcrumb,
        url: '/' + routeUrl.join('/'),
      });
    }

    if (route.firstChild) {
      this.addBreadcrumb(route.firstChild, routeUrl, breadcrumbs);
    }
  }
}
