/**
 * Player position utilities for relative positioning
 */
import { PlayerPosition, Team } from '../../api/generated/signalr-types.generated';

/**
 * All positions in clockwise order starting from Bottom
 */
export const POSITIONS_CLOCKWISE: PlayerPosition[] = [
  PlayerPosition.Bottom,
  PlayerPosition.Left,
  PlayerPosition.Top,
  PlayerPosition.Right,
];

/**
 * Get the team for a position
 */
export function getTeam(position: PlayerPosition): Team {
  return position === PlayerPosition.Bottom || position === PlayerPosition.Top
    ? Team.Team1
    : Team.Team2;
}

/**
 * Get the partner position for a given position
 */
export function getPartner(position: PlayerPosition): PlayerPosition {
  switch (position) {
    case PlayerPosition.Bottom:
      return PlayerPosition.Top;
    case PlayerPosition.Top:
      return PlayerPosition.Bottom;
    case PlayerPosition.Left:
      return PlayerPosition.Right;
    case PlayerPosition.Right:
      return PlayerPosition.Left;
  }
}

/**
 * Get positions relative to the player's position.
 * Returns [self, left, across, right] from the player's perspective.
 */
export function getRelativePositions(myPosition: PlayerPosition): {
  self: PlayerPosition;
  left: PlayerPosition;
  across: PlayerPosition;
  right: PlayerPosition;
} {
  const myIndex = POSITIONS_CLOCKWISE.indexOf(myPosition);

  return {
    self: myPosition,
    left: POSITIONS_CLOCKWISE[(myIndex + 1) % 4],
    across: POSITIONS_CLOCKWISE[(myIndex + 2) % 4],
    right: POSITIONS_CLOCKWISE[(myIndex + 3) % 4],
  };
}

/**
 * Get the position to the left of a given position (clockwise)
 */
export function getLeftPosition(position: PlayerPosition): PlayerPosition {
  const index = POSITIONS_CLOCKWISE.indexOf(position);
  return POSITIONS_CLOCKWISE[(index + 1) % 4];
}

/**
 * Get the position to the right of a given position (counter-clockwise)
 */
export function getRightPosition(position: PlayerPosition): PlayerPosition {
  const index = POSITIONS_CLOCKWISE.indexOf(position);
  return POSITIONS_CLOCKWISE[(index + 3) % 4];
}

/**
 * Get the next position in turn order (counter-clockwise: Bottom -> Right -> Top -> Left -> Bottom)
 */
export function getNextPosition(position: PlayerPosition): PlayerPosition {
  return getRightPosition(position);
}

/**
 * Map a table position to a relative position from the player's perspective.
 * Used for rendering cards in the trick area.
 */
export type RelativePosition = 'bottom' | 'left' | 'top' | 'right';

export function toRelativePosition(
  tablePosition: PlayerPosition,
  myPosition: PlayerPosition
): RelativePosition {
  const relative = getRelativePositions(myPosition);

  if (tablePosition === relative.self) return 'bottom';
  if (tablePosition === relative.left) return 'left';
  if (tablePosition === relative.across) return 'top';
  return 'right';
}

/**
 * Position display names
 */
export function getPositionDisplayName(position: PlayerPosition): string {
  return position; // Already readable: "Bottom", "Left", "Top", "Right"
}
