import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { GameHubService } from '../../api/game-hub.service';
import { ChatMessageEvent } from '../../api/generated/signalr-types.generated';

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private readonly hub = inject(GameHubService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _messages = signal<ChatMessageEvent[]>([]);
  private readonly _isChatEnabled = signal(true);
  private readonly _unreadCount = signal(0);
  private readonly _isPopupOpen = signal(false);
  private _historyLoaded = false;

  readonly messages = this._messages.asReadonly();
  readonly isChatEnabled = this._isChatEnabled.asReadonly();
  readonly unreadCount = this._unreadCount.asReadonly();
  readonly isPopupOpen = this._isPopupOpen.asReadonly();

  constructor() {
    this.hub.chatMessageReceived$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      this._messages.update((msgs) => [...msgs, event]);
      if (!this._isPopupOpen()) {
        this._unreadCount.update((c) => c + 1);
      }
    });

    this.hub.chatStatusChanged$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((event) => {
      this._isChatEnabled.set(event.isChatEnabled);
    });
  }

  async openPopup(roomId: string): Promise<void> {
    if (!this._historyLoaded) {
      try {
        const response = await this.hub.getChatHistory(roomId);
        this._messages.set(response.messages);
        this._isChatEnabled.set(response.isChatEnabled);
        this._historyLoaded = true;
      } catch (e) {
        console.error('[Chat] Failed to load history', e);
      }
    }
    this._isPopupOpen.set(true);
    this._unreadCount.set(0);
  }

  closePopup(): void {
    this._isPopupOpen.set(false);
  }

  async sendMessage(roomId: string, clientId: string, content: string): Promise<void> {
    try {
      await this.hub.sendChatMessage(roomId, clientId, content);
    } catch (e) {
      console.error('[Chat] Failed to send message', e);
    }
  }

  reset(): void {
    this._messages.set([]);
    this._isChatEnabled.set(true);
    this._unreadCount.set(0);
    this._isPopupOpen.set(false);
    this._historyLoaded = false;
  }
}
