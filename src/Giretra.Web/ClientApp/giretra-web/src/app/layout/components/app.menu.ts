import { Component, computed, ElementRef, inject, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { AppMenuitem } from './app.menuitem';

@Component({
  selector: '[app-menu]',
  standalone: true,
  imports: [CommonModule, AppMenuitem, RouterModule],
  template: `<ul class="layout-menu" #menuContainer>
    @for (item of model(); track $index) {
      @if (!item.separator) {
        <li app-menuitem [item]="item" [root]="true"></li>
      } @else {
        <li class="menu-separator"></li>
      }
    }
  </ul>`,
  host: {
    class: 'layout-menu-container',
  },
})
export class AppMenu {
  el: ElementRef = inject(ElementRef);

  private readonly auth = inject(AuthService);

  @ViewChild('menuContainer') menuContainer!: ElementRef;

  // Labels are translation keys, resolved by app.menuitem.
  model = computed<any[]>(() => [
    {
      label: 'menu.sections.play',
      items: [{ label: 'menu.home', icon: 'pi pi-fw pi-home', routerLink: ['/'] }],
    },
    {
      label: 'menu.sections.stats',
      items: [
        { label: 'menu.leaderboard', icon: 'pi pi-fw pi-trophy', routerLink: ['/leaderboard'] },
        { label: 'menu.highlights', icon: 'pi pi-fw pi-chart-line', routerLink: ['/highlights'] },
        { label: 'menu.achievements', icon: 'pi pi-fw pi-star', routerLink: ['/achievements'] },
      ],
    },
    {
      label: 'menu.sections.account',
      items: [{ label: 'menu.settings', icon: 'pi pi-fw pi-cog', routerLink: ['/settings'] }],
    },
    {
      label: 'menu.sections.help',
      items: [{ label: 'menu.feedback', icon: 'pi pi-fw pi-lightbulb', routerLink: ['/feedback'] }],
    },
    {
      label: 'menu.sections.admin',
      visible: this.auth.isModerator(),
      items: [
        { label: 'menu.admin', icon: 'pi pi-fw pi-shield', routerLink: ['/admin'] },
        { label: 'menu.adminUsers', icon: 'pi pi-fw pi-users', routerLink: ['/admin/users'] },
        { label: 'menu.adminGames', icon: 'pi pi-fw pi-list', routerLink: ['/admin/games'] },
      ],
    },
  ]);
}
