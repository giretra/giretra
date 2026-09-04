import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { TranslocoDirective } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { AvatarModule } from 'primeng/avatar';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import {
  ApiService,
  FriendsListResponse,
  FriendResponse,
  FriendRequestResponse,
  UserSearchResultResponse,
} from '../../../core/services/api.service';

@Component({
  selector: 'app-friends-section',
  standalone: true,
  imports: [FormsModule, TranslocoDirective, ButtonModule, AvatarModule, TagModule, InputTextModule, IconFieldModule, InputIconModule],
  template: `
    <ng-container *transloco="let t">
      <div class="g-panel-head">
        <div class="g-panel-text">
          <span class="g-panel-title">{{ t('settings.tabs.friends') }}</span>
          <span class="g-form-desc">{{ t('settings.friends.hint') }}</span>
        </div>
        <div class="head-search">
          <p-iconfield>
            <p-inputicon class="pi pi-search" />
            <input
              pInputText
              fluid
              type="text"
              name="search"
              [placeholder]="t('settings.friends.searchPlaceholder')"
              [(ngModel)]="searchQuery"
              (ngModelChange)="onSearchChange($event)"
            />
          </p-iconfield>

        </div>
      </div>
      <div class="g-divider mx"></div>
      <div class="g-panel-body">
      @if (searchResults().length > 0) {
        <ul class="g-user-list results">
          @for (user of searchResults(); track user.userId) {
            <li class="g-user-row">
              <p-avatar [label]="user.displayName.charAt(0).toUpperCase()" shape="circle" />
              <div class="g-user-info">
                <span class="g-user-name">{{ user.displayName }}</span>
                <span class="g-user-sub">&#64;{{ user.username }}</span>
              </div>
              <p-button icon="pi pi-user-plus" [label]="t('settings.friends.add')" size="small" (onClick)="sendRequest(user.username)" />
            </li>
          }
        </ul>
      }

      @if (pendingReceived().length > 0) {
        <div class="g-list-head">
          {{ t('settings.friends.pendingRequests') }}
          <p-tag [value]="pendingReceived().length.toString()" severity="danger" [rounded]="true" />
        </div>
        <ul class="g-user-list">
          @for (req of pendingReceived(); track req.friendshipId) {
            <li class="g-user-row">
              <p-avatar [label]="req.displayName.charAt(0).toUpperCase()" shape="circle" />
              <div class="g-user-info">
                <span class="g-user-name">{{ req.displayName }}</span>
                <span class="g-user-sub">&#64;{{ req.username }}</span>
              </div>
              <div class="g-row-actions">
                <p-button icon="pi pi-check" [label]="t('settings.friends.accept')" severity="success" size="small" (onClick)="acceptRequest(req.friendshipId)" />
                <p-button icon="pi pi-times" severity="danger" [text]="true" size="small" [attr.aria-label]="t('settings.friends.decline')" (onClick)="declineRequest(req.friendshipId)" />
              </div>
            </li>
          }
        </ul>
      }

      @if (pendingSent().length > 0) {
        <div class="g-list-head">
          {{ t('settings.friends.sentRequests') }}
          <p-tag [value]="pendingSent().length.toString()" severity="secondary" [rounded]="true" />
        </div>
        <ul class="g-user-list">
          @for (req of pendingSent(); track req.friendshipId) {
            <li class="g-user-row">
              <p-avatar [label]="req.displayName.charAt(0).toUpperCase()" shape="circle" />
              <div class="g-user-info">
                <span class="g-user-name">{{ req.displayName }}</span>
                <span class="g-user-sub">&#64;{{ req.username }}</span>
              </div>
              <p-button [label]="t('settings.friends.cancel')" severity="secondary" [outlined]="true" size="small" (onClick)="declineRequest(req.friendshipId)" />
            </li>
          }
        </ul>
      }

      <div class="g-list-head">
        {{ t('settings.friends.friendsList') }}
        <p-tag [value]="friends().length.toString()" severity="secondary" [rounded]="true" />
      </div>
      @if (friends().length === 0) {
        <div class="g-empty compact">
          <span class="g-empty-icon"><i class="pi pi-users"></i></span>
          <span class="g-empty-hint">{{ t('settings.friends.noFriends') }}</span>
        </div>
      } @else {
        <ul class="g-user-list">
          @for (friend of friends(); track friend.userId) {
            <li class="g-user-row">
              <p-avatar [label]="friend.displayName.charAt(0).toUpperCase()" shape="circle" />
              <div class="g-user-info">
                <span class="g-user-name">{{ friend.displayName }}</span>
                <span class="g-user-sub">&#64;{{ friend.username }}</span>
              </div>
              <p-button icon="pi pi-user-minus" [label]="t('settings.friends.remove')" severity="danger" [text]="true" size="small" (onClick)="removeFriend(friend.userId)" />
            </li>
          }
        </ul>
      }
      </div>
    </ng-container>
  `,
  styles: [`
    :host { display:flex; flex-direction:column; flex:1; }
    .g-panel-text { display:flex; flex-direction:column; gap:0.125rem; }
    .g-divider.mx { margin:0 1.5rem; }
    @media (min-width:1200px) { .g-divider.mx { margin:0 2rem; } }
    .head-search { flex:1; min-width:14rem; max-width:24rem; }
    .results { padding:0.25rem; border:1px solid var(--surface-border); border-radius:1rem; background:var(--p-surface-800); }
    .g-panel-body { gap:0.5rem; }
    .g-list-head { margin:1rem 0 0.25rem; }
    .g-panel-body > .g-list-head:first-child { margin-top:0; }
    .g-empty.compact { padding:1.5rem 0.5rem; }
  `],
})
export class FriendsSectionComponent implements OnInit, OnDestroy {

  private readonly api = inject(ApiService);

  readonly friends = signal<FriendResponse[]>([]);
  readonly pendingReceived = signal<FriendRequestResponse[]>([]);
  readonly pendingSent = signal<FriendRequestResponse[]>([]);
  readonly searchResults = signal<UserSearchResultResponse[]>([]);

  searchQuery = '';
  private readonly searchSubject = new Subject<string>();
  private searchSub: Subscription | null = null;

  ngOnInit(): void {
    this.loadFriends();
    this.searchSub = this.searchSubject
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) => (q.length >= 2 ? this.api.searchUsers(q) : of({ results: [] }))),
      )
      .subscribe({
        next: (res) => this.searchResults.set(res.results),
      });
  }

  ngOnDestroy(): void {
    this.searchSub?.unsubscribe();
  }

  onSearchChange(query: string): void {
    if (query.length < 2) {
      this.searchResults.set([]);
    }
    this.searchSubject.next(query);
  }

  private loadFriends(): void {
    this.api.getFriends().subscribe({
      next: (res) => {
        this.friends.set(res.friends);
        this.pendingReceived.set(res.pendingReceived);
        this.pendingSent.set(res.pendingSent);
      },
    });
  }

  sendRequest(username: string): void {
    this.api.sendFriendRequest(username).subscribe({
      next: () => {
        this.searchResults.set([]);
        this.searchQuery = '';
        this.loadFriends();
      },
    });
  }

  acceptRequest(friendshipId: string): void {
    this.api.acceptFriendRequest(friendshipId).subscribe({
      next: () => this.loadFriends(),
    });
  }

  declineRequest(friendshipId: string): void {
    this.api.declineFriendRequest(friendshipId).subscribe({
      next: () => this.loadFriends(),
    });
  }

  removeFriend(userId: string): void {
    this.api.removeFriend(userId).subscribe({
      next: () => this.loadFriends(),
    });
  }
}
