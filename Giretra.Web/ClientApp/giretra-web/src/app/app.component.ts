import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: `
    <main class="min-h-screen flex items-center justify-center">
      <div class="text-center space-y-4">
        <h1 class="text-4xl font-bold text-[hsl(var(--primary))]">
          ♠ Giretra ♥
        </h1>
        <p class="text-[hsl(var(--muted-foreground))]">
          Belote Malagasy — table is being set up…
        </p>
      </div>
    </main>
  `,
  styles: [],
})
export class AppComponent {}
