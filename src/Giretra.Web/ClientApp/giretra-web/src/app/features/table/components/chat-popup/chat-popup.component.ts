import {
  Component,
  input,
  output,
  signal,
  ElementRef,
  viewChild,
  effect,
  HostListener,
} from '@angular/core';
import { ChatMessageEvent } from '../../../../api/generated/signalr-types.generated';
import { LucideAngularModule, X, Send, TriangleAlert } from 'lucide-angular';
import { TranslocoDirective } from '@jsverse/transloco';

@Component({
  selector: 'app-chat-popup',
  standalone: true,
  imports: [LucideAngularModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <!-- Backdrop -->
      <div class="backdrop" (click)="closed.emit()"></div>

      <!-- Container -->
      <div class="container">
        <div class="panel" (click)="$event.stopPropagation()">
          <!-- Header -->
          <div class="header">
            <span class="header-title">{{ t('chat.title') }}</span>
            <button class="close-btn" (click)="closed.emit()">
              <i-lucide [img]="XIcon" [size]="16" [strokeWidth]="1.5"></i-lucide>
            </button>
          </div>

          <!-- Disclaimer -->
          <div class="disclaimer">
            <i-lucide
              [img]="TriangleAlertIcon"
              [size]="13"
              [strokeWidth]="1.5"
            ></i-lucide>
            <span>{{ t('chat.disclaimer') }}</span>
          </div>

          <!-- Messages -->
          <div class="messages" #messageList>
            @if (messages().length === 0) {
              <div class="empty">{{ t('chat.noMessages') }}</div>
            } @else {
              @for (msg of messages(); track msg.sequenceNumber) {
                @if (msg.isSystem) {
                  <div class="msg-system">{{ msg.content }}</div>
                } @else {
                  <div class="msg">
                    <span class="msg-sender" [class.msg-player]="msg.isPlayer">
                      {{ msg.isPlayer ? msg.senderName : msg.senderName + ' (' + t('chat.spectator') + ')' }}
                    </span>
                    <span class="msg-content">{{ msg.content }}</span>
                  </div>
                }
              }
            }
          </div>

          <!-- Input area -->
          @if (isChatEnabled()) {
            <div class="input-area">
              <input
                #chatInput
                type="text"
                class="chat-input"
                [placeholder]="t('chat.placeholder')"
                [maxLength]="300"
                (keydown.enter)="onSend()"
                [(value)]="inputValue"
                (input)="inputValue.set(chatInput.value)"
              />
              <button
                class="send-btn"
                (click)="onSend()"
                [disabled]="!inputValue().trim()"
              >
                <i-lucide [img]="SendIcon" [size]="16" [strokeWidth]="1.5"></i-lucide>
              </button>
            </div>
          } @else {
            <div class="disabled-hint">{{ t('chat.disabledDuringPlay') }}</div>
          }
        </div>
      </div>
    </ng-container>
  `,
  styles: [
    `
      :host {
        display: contents;
      }

      .backdrop {
        position: fixed;
        inset: 0;
        z-index: 100;
        background: hsl(0 0% 0% / 0.4);
        animation: fadeIn 0.15s ease;
      }

      @keyframes fadeIn {
        from {
          opacity: 0;
        }
        to {
          opacity: 1;
        }
      }

      .container {
        position: fixed;
        inset: 0;
        z-index: 110;
        display: flex;
        align-items: flex-end;
        justify-content: center;
        padding: 1rem;
        pointer-events: none;
      }

      .panel {
        pointer-events: auto;
        width: 100%;
        max-width: 400px;
        max-height: 70vh;
        background: hsl(var(--card));
        border: 0.5px solid hsl(var(--foreground) / 0.1);
        border-radius: 0.75rem;
        display: flex;
        flex-direction: column;
        overflow: hidden;
        box-shadow:
          0 0 0 0.5px hsl(0 0% 0% / 0.12),
          0 8px 40px hsl(0 0% 0% / 0.45);
        animation: panelIn 0.2s cubic-bezier(0.2, 0, 0, 1);
      }

      @keyframes panelIn {
        from {
          opacity: 0;
          transform: translateY(16px) scale(0.97);
        }
        to {
          opacity: 1;
          transform: translateY(0) scale(1);
        }
      }

      .header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.75rem 1rem;
        border-bottom: 0.5px solid hsl(var(--foreground) / 0.08);
        flex-shrink: 0;
      }

      .header-title {
        font-size: 0.875rem;
        font-weight: 600;
        color: hsl(var(--foreground));
      }

      .close-btn {
        display: grid;
        place-items: center;
        width: 1.75rem;
        height: 1.75rem;
        border: none;
        border-radius: 0.375rem;
        background: transparent;
        color: hsl(var(--foreground) / 0.5);
        cursor: pointer;
        transition:
          background 0.15s ease,
          color 0.15s ease;
      }

      .close-btn:hover {
        background: hsl(var(--foreground) / 0.06);
        color: hsl(var(--foreground));
      }

      .disclaimer {
        display: flex;
        align-items: flex-start;
        gap: 0.375rem;
        padding: 0.5rem 1rem;
        background: hsl(45 90% 55% / 0.08);
        border-bottom: 0.5px solid hsl(45 90% 55% / 0.15);
        font-size: 0.6875rem;
        color: hsl(45 80% 65%);
        line-height: 1.4;
        flex-shrink: 0;
      }

      .disclaimer i-lucide {
        flex-shrink: 0;
        margin-top: 1px;
      }

      .messages {
        flex: 1;
        overflow-y: auto;
        padding: 0.75rem 1rem;
        display: flex;
        flex-direction: column;
        gap: 0.375rem;
        min-height: 120px;
      }

      .empty {
        display: flex;
        align-items: center;
        justify-content: center;
        flex: 1;
        font-size: 0.8125rem;
        color: hsl(var(--foreground) / 0.3);
      }

      .msg {
        display: flex;
        gap: 0.375rem;
        align-items: baseline;
        font-size: 0.8125rem;
        line-height: 1.4;
        word-break: break-word;
      }

      .msg-sender {
        font-weight: 600;
        flex-shrink: 0;
        color: hsl(var(--foreground) / 0.45);
        font-size: 0.75rem;
      }

      .msg-sender.msg-player {
        color: hsl(var(--primary));
      }

      .msg-content {
        color: hsl(var(--foreground) / 0.85);
      }

      .msg-system {
        font-size: 0.6875rem;
        color: hsl(var(--foreground) / 0.35);
        text-align: center;
        padding: 0.25rem 0;
        font-style: italic;
      }

      .input-area {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding: 0.625rem 0.75rem;
        border-top: 0.5px solid hsl(var(--foreground) / 0.08);
        flex-shrink: 0;
      }

      .chat-input {
        flex: 1;
        background: hsl(var(--foreground) / 0.05);
        border: 0.5px solid hsl(var(--foreground) / 0.1);
        border-radius: 0.5rem;
        padding: 0.5rem 0.75rem;
        font-size: 0.8125rem;
        color: hsl(var(--foreground));
        outline: none;
        transition: border-color 0.15s ease;
      }

      .chat-input::placeholder {
        color: hsl(var(--foreground) / 0.3);
      }

      .chat-input:focus {
        border-color: hsl(var(--primary) / 0.5);
      }

      .send-btn {
        display: grid;
        place-items: center;
        width: 2rem;
        height: 2rem;
        border: none;
        border-radius: 0.5rem;
        background: hsl(var(--primary));
        color: hsl(var(--primary-foreground));
        cursor: pointer;
        transition: opacity 0.15s ease;
        flex-shrink: 0;
      }

      .send-btn:hover:not(:disabled) {
        opacity: 0.85;
      }

      .send-btn:disabled {
        opacity: 0.4;
        cursor: default;
      }

      .disabled-hint {
        padding: 0.75rem 1rem;
        text-align: center;
        font-size: 0.75rem;
        color: hsl(var(--foreground) / 0.35);
        border-top: 0.5px solid hsl(var(--foreground) / 0.08);
        flex-shrink: 0;
      }
    `,
  ],
})
export class ChatPopupComponent {
  readonly XIcon = X;
  readonly SendIcon = Send;
  readonly TriangleAlertIcon = TriangleAlert;

  readonly messages = input<ChatMessageEvent[]>([]);
  readonly isChatEnabled = input(true);

  readonly messageSent = output<string>();
  readonly closed = output<void>();

  readonly inputValue = signal('');
  private readonly messageListRef = viewChild<ElementRef>('messageList');

  constructor() {
    // Auto-scroll to bottom when new messages arrive
    effect(() => {
      const msgs = this.messages();
      const el = this.messageListRef()?.nativeElement;
      if (msgs.length > 0 && el) {
        requestAnimationFrame(() => {
          el.scrollTop = el.scrollHeight;
        });
      }
    });
  }

  onSend(): void {
    const value = this.inputValue().trim();
    if (!value) return;
    this.messageSent.emit(value);
    this.inputValue.set('');
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
