import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, Subscription, debounceTime, distinctUntilChanged, switchMap, of } from 'rxjs';
import { LucideAngularModule, Search, UserPlus, UserMinus, X, Check } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';
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
  imports: [FormsModule, LucideAngularModule, TranslocoDirective],
  template: `
    <div class="friends" *transloco="let t">
      <!-- Search -->
      <div class="search-section">
        <div class="search-input-wrap">
          <i-lucide [img]="SearchIcon" [size]="14" class="search-icon"></i-lucide>
          <input
            type="text"
            class="search-input"
            [placeholder]="t('settings.friends.searchPlaceholder')"
            [(ngModel)]="searchQuery"
            (ngModelChange)="onSearchChange($event)"
          />
        </div>
        @if (searchResults().length > 0) {
          <div class="search-results">
            @for (user of searchResults(); track user.userId) {
              <div class="user-row">
                <div class="user-avatar-sm">{{ user.displayName.charAt(0).toUpperCase() }}</div>
                <div class="user-info">
                  <span class="user-display">{{ user.displayName }}</span>
                  <span class="user-username">&#64;{{ user.username }}</span>
                </div>
                <button class="btn btn-sm btn-primary" (click)="sendRequest(user.username)">
                  <i-lucide [img]="UserPlusIcon" [size]="12"></i-lucide>
                  {{ t('settings.friends.add') }}
                </button>
              </div>
            }
          </div>
        }
      </div>

      <!-- Pending received -->
      @if (pendingReceived().length > 0) {
        <div class="section">
          <h3 class="section-title">{{ t('settings.friends.pendingRequests') }}</h3>
          @for (req of pendingReceived(); track req.friendshipId) {
            <div class="user-row">
              <div class="user-avatar-sm">{{ req.displayName.charAt(0).toUpperCase() }}</div>
              <div class="user-info">
                <span class="user-display">{{ req.displayName }}</span>
                <span class="user-username">&#64;{{ req.username }}</span>
              </div>
              <div class="row-actions">
                <button class="btn btn-sm btn-primary" (click)="acceptRequest(req.friendshipId)">
                  <i-lucide [img]="CheckIcon" [size]="12"></i-lucide>
                  {{ t('settings.friends.accept') }}
                </button>
                <button class="btn btn-sm btn-destructive" (click)="declineRequest(req.friendshipId)">
                  <i-lucide [img]="XIcon" [size]="12"></i-lucide>
                  {{ t('settings.friends.decline') }}
                </button>
              </div>
            </div>
          }
        </div>
      }

      <!-- Pending sent -->
      @if (pendingSent().length > 0) {
        <div class="section">
          <h3 class="section-title">{{ t('settings.friends.sentRequests') }}</h3>
          @for (req of pendingSent(); track req.friendshipId) {
            <div class="user-row">
              <div class="user-avatar-sm">{{ req.displayName.charAt(0).toUpperCase() }}</div>
              <div class="user-info">
                <span class="user-display">{{ req.displayName }}</span>
                <span class="user-username">&#64;{{ req.username }}</span>
              </div>
              <button class="btn btn-sm btn-destructive" (click)="declineRequest(req.friendshipId)">
                <i-lucide [img]="XIcon" [size]="12"></i-lucide>
                {{ t('settings.friends.cancel') }}
              </button>
            </div>
          }
        </div>
      }

      <!-- Friends list -->
      <div class="section">
        <h3 class="section-title">{{ t('settings.friends.friendsList') }}</h3>
        @if (friends().length === 0) {
          <div class="empty-state">{{ t('settings.friends.noFriends') }}</div>
        } @else {
          @for (friend of friends(); track friend.userId) {
            <div class="user-row">
              <div class="user-avatar-sm">{{ friend.displayName.charAt(0).toUpperCase() }}</div>
              <div class="user-info">
                <span class="user-display">{{ friend.displayName }}</span>
                <span class="user-username">&#64;{{ friend.username }}</span>
              </div>
              <button class="btn btn-sm btn-destructive" (click)="removeFriend(friend.userId)">
                <i-lucide [img]="UserMinusIcon" [size]="12"></i-lucide>
                {{ t('settings.friends.remove') }}
              </button>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    .friends { display:flex; flex-direction:column; gap:1.25rem; }
    .search-section { position:relative; }
    .search-input-wrap { position:relative; }
    .search-icon { position:absolute; left:0.75rem; top:50%; transform:translateY(-50%); color:hsl(var(--muted-foreground)); pointer-events:none; }
    .search-input { width:100%; padding:0.5rem 0.75rem 0.5rem 2.25rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--input)); color:hsl(var(--foreground)); font-size:0.875rem; outline:none; transition:all 0.15s ease; box-sizing:border-box; }
    .search-input:focus { border-color:hsl(var(--ring)); box-shadow:0 0 0 2px hsl(var(--ring)/0.2); }
    .search-results { margin-top:0.5rem; border:1px solid hsl(var(--border)); border-radius:var(--radius); background:hsl(var(--card)); overflow:hidden; }
    .section { display:flex; flex-direction:column; gap:0.5rem; }
    .section-title { font-size:0.6875rem; font-weight:600; color:hsl(var(--muted-foreground)); text-transform:uppercase; letter-spacing:0.06em; margin:0; }
    .user-row { display:flex; align-items:center; gap:0.625rem; padding:0.625rem 0.75rem; border-bottom:1px solid hsl(var(--border)/0.5); transition:all 0.15s ease; }
    .user-row:last-child { border-bottom:none; }
    .user-row:hover { background:hsl(var(--foreground)/0.03); }
    .user-avatar-sm { width:2rem; height:2rem; border-radius:50%; background:hsl(var(--primary)/0.2); border:2px solid hsl(var(--primary)/0.4); display:flex; align-items:center; justify-content:center; font-size:0.75rem; font-weight:700; color:hsl(var(--primary)); flex-shrink:0; }
    .user-info { display:flex; flex-direction:column; gap:0.0625rem; flex:1; min-width:0; }
    .user-display { font-size:0.8125rem; font-weight:600; color:hsl(var(--foreground)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .user-username { font-size:0.6875rem; color:hsl(var(--muted-foreground)); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .row-actions { display:flex; gap:0.375rem; flex-shrink:0; }
    .btn { display:inline-flex; align-items:center; gap:0.375rem; padding:0.375rem 0.75rem; border-radius:var(--radius); border:1px solid hsl(var(--border)); background:hsl(var(--secondary)); color:hsl(var(--foreground)); font-size:0.75rem; font-weight:500; cursor:pointer; transition:all 0.15s ease; white-space:nowrap; }
    .btn:hover { background:hsl(var(--muted)); }
    .btn-sm { padding:0.25rem 0.625rem; font-size:0.6875rem; }
    .btn-primary { background:hsl(var(--primary)); color:hsl(var(--primary-foreground)); border-color:hsl(var(--primary)); }
    .btn-primary:hover { opacity:0.9; }
    .btn-destructive { background:transparent; color:hsl(var(--destructive)); border-color:hsl(var(--destructive)/0.3); }
    .btn-destructive:hover { background:hsl(var(--destructive)/0.1); }
    .empty-state { padding:2rem; text-align:center; color:hsl(var(--muted-foreground)); font-size:0.8125rem; }
  `],
})
export class FriendsSectionComponent implements OnInit, OnDestroy {
  readonly SearchIcon = Search;
  readonly UserPlusIcon = UserPlus;
  readonly UserMinusIcon = UserMinus;
  readonly XIcon = X;
  readonly CheckIcon = Check;

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
