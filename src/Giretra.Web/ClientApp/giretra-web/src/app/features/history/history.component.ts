import { Component } from '@angular/core';
import { MatchHistorySectionComponent } from '../settings/components/match-history-section.component';

/** Standalone page for the player's match history, reachable from the burger menu. */
@Component({
  selector: 'app-history',
  standalone: true,
  imports: [MatchHistorySectionComponent],
  template: `
    <div class="g-card history">
      <app-match-history-section />
    </div>
  `,
  styles: [`
    .history { display:flex; flex-direction:column; padding:0; overflow:hidden; min-height:32rem; }
  `],
})
export class HistoryComponent {}
