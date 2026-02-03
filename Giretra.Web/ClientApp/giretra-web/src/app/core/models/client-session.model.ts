/**
 * Client session types for localStorage persistence
 */
import { PlayerPosition } from '../../api/generated/signalr-types.generated';

export interface ClientSession {
  clientId: string | null;
  playerName: string | null;
  roomId: string | null;
  position: PlayerPosition | null;
  isWatcher: boolean;
}

export const DEFAULT_SESSION: ClientSession = {
  clientId: null,
  playerName: null,
  roomId: null,
  position: null,
  isWatcher: false,
};
