import { Injectable, signal } from '@angular/core';

type SoundName =
  | 'general_click'
  | 'card_folding_started'
  | 'card_played'
  | 'card_played_under'
  | 'card_played_master';

const STORAGE_KEY = 'giretra-sound-muted';

@Injectable({
  providedIn: 'root',
})
export class SoundService {
  private audioContext: AudioContext | null = null;
  private readonly buffers = new Map<SoundName, AudioBuffer>();
  private readonly _muted = signal(
    typeof localStorage !== 'undefined' && localStorage.getItem(STORAGE_KEY) === 'true',
  );
  readonly muted = this._muted.asReadonly();

  constructor() {
    this.preload('general_click', 'assets/sounds/general_click.mp3');
    this.preload('card_folding_started', 'assets/sounds/card_folding_started.mp3');
    this.preload('card_played', 'assets/sounds/card_played.mp3');
    this.preload('card_played_under', 'assets/sounds/card_played_under.mp3');
    this.preload('card_played_master', 'assets/sounds/card_played_master.mp3');
  }

  private getContext(): AudioContext {
    if (!this.audioContext) {
      this.audioContext = new AudioContext();
    }
    return this.audioContext;
  }

  private preload(name: SoundName, path: string): void {
    fetch(path)
      .then((res) => res.arrayBuffer())
      .then((data) => this.getContext().decodeAudioData(data))
      .then((buffer) => this.buffers.set(name, buffer))
      .catch(() => {
        // Silent fail — sound just won't play
      });
  }

  play(name: SoundName): void {
    if (this._muted()) return;
    const buffer = this.buffers.get(name);
    if (!buffer) return;

    const ctx = this.getContext();
    // Resume context if suspended (browser autoplay policy)
    if (ctx.state === 'suspended') {
      ctx.resume();
    }
    const source = ctx.createBufferSource();
    source.buffer = buffer;
    source.connect(ctx.destination);
    source.start(0);
  }

  toggleMute(): void {
    const newVal = !this._muted();
    this._muted.set(newVal);
    localStorage.setItem(STORAGE_KEY, String(newVal));
  }
}
