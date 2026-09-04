import { inject, Injectable, signal } from '@angular/core';
import { ApiService } from './api.service';
import { GameHubService } from '../../api/game-hub.service';

/**
 * Pending friend request count shown as a badge on the topbar avatar. Lives in a root
 * service so every page under the layout sees it, not just the home lobby.
 */
@Injectable({ providedIn: 'root' })
export class PendingFriendsService {
  private readonly api = inject(ApiService);
  private readonly hub = inject(GameHubService);

  private readonly _count = signal(0);
  readonly count = this._count.asReadonly();

  constructor() {
    this.hub.pendingFriendCountChanged$.subscribe((event) => this._count.set(event.count));
    this.hub.reconnected$.subscribe(() => this.refresh());
  }

  refresh(): void {
    this.api.getPendingFriendCount().subscribe({
      next: (res) => this._count.set(res.count),
      error: () => {},
    });
  }
}
