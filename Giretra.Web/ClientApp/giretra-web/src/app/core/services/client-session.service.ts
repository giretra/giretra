import { Injectable, signal, computed, effect } from '@angular/core';
import { PlayerPosition } from '../../api/generated/signalr-types.generated';
import { ClientSession, DEFAULT_SESSION } from '../models';

const STORAGE_KEY = 'giretra_session';

@Injectable({
  providedIn: 'root',
})
export class ClientSessionService {
  // Core session signals
  private readonly _clientId = signal<string | null>(null);
  private readonly _playerName = signal<string | null>(null);
  private readonly _roomId = signal<string | null>(null);
  private readonly _position = signal<PlayerPosition | null>(null);
  private readonly _isWatcher = signal<boolean>(false);

  // Public readonly signals
  readonly clientId = this._clientId.asReadonly();
  readonly playerName = this._playerName.asReadonly();
  readonly roomId = this._roomId.asReadonly();
  readonly position = this._position.asReadonly();
  readonly isWatcher = this._isWatcher.asReadonly();

  // Computed signals
  readonly hasName = computed(() => !!this._playerName());
  readonly isInRoom = computed(() => !!this._roomId());
  readonly isPlayer = computed(() => !!this._position() && !this._isWatcher());

  constructor() {
    // Load from localStorage on init
    this.loadFromStorage();

    // Persist to localStorage on changes
    effect(() => {
      const session: ClientSession = {
        clientId: this._clientId(),
        playerName: this._playerName(),
        roomId: this._roomId(),
        position: this._position(),
        isWatcher: this._isWatcher(),
      };
      this.saveToStorage(session);
    });
  }

  /**
   * Set the player's display name
   */
  setPlayerName(name: string): void {
    this._playerName.set(name.trim() || null);
  }

  /**
   * Join a room as a player
   */
  joinRoom(roomId: string, clientId: string, position: PlayerPosition): void {
    this._roomId.set(roomId);
    this._clientId.set(clientId);
    this._position.set(position);
    this._isWatcher.set(false);
  }

  /**
   * Join a room as a watcher
   */
  watchRoom(roomId: string, clientId: string): void {
    this._roomId.set(roomId);
    this._clientId.set(clientId);
    this._position.set(null);
    this._isWatcher.set(true);
  }

  /**
   * Leave the current room
   */
  leaveRoom(): void {
    this._roomId.set(null);
    this._clientId.set(null);
    this._position.set(null);
    this._isWatcher.set(false);
  }

  /**
   * Update position (for rejoining after disconnect)
   */
  updatePosition(position: PlayerPosition): void {
    this._position.set(position);
  }

  /**
   * Clear all session data
   */
  clear(): void {
    this._clientId.set(null);
    this._playerName.set(null);
    this._roomId.set(null);
    this._position.set(null);
    this._isWatcher.set(false);
  }

  /**
   * Get current session snapshot
   */
  getSession(): ClientSession {
    return {
      clientId: this._clientId(),
      playerName: this._playerName(),
      roomId: this._roomId(),
      position: this._position(),
      isWatcher: this._isWatcher(),
    };
  }

  private loadFromStorage(): void {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const session: ClientSession = JSON.parse(stored);
        this._clientId.set(session.clientId ?? null);
        this._playerName.set(session.playerName ?? null);
        this._roomId.set(session.roomId ?? null);
        this._position.set(session.position ?? null);
        this._isWatcher.set(session.isWatcher ?? false);
      }
    } catch {
      // Invalid stored data, use defaults
      console.warn('Failed to load session from localStorage');
    }
  }

  private saveToStorage(session: ClientSession): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
      console.warn('Failed to save session to localStorage');
    }
  }
}
