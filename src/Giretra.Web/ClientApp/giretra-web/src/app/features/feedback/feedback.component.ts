import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import {
  LucideAngularModule,
  ArrowLeft,
  Bug,
  ChevronRight,
  CircleCheck,
  ExternalLink,
  Github,
  Lightbulb,
  Mail,
  MessageCircleQuestion,
  MessageSquare,
  ShieldCheck,
} from 'lucide-angular';
import {
  ApiService,
  FeedbackCategory,
  FeedbackConfigResponse,
} from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

type Step = 'choose' | 'form' | 'sent';

interface CategoryOption {
  key: FeedbackCategory;
  icon: typeof Bug;
  labelKey: string;
}

const SUBJECT_MIN = 3;
const SUBJECT_MAX = 120;
const MESSAGE_MIN = 10;
const MESSAGE_MAX = 4000;
const DEFAULT_ISSUES_URL = 'https://github.com/giretra/giretra/issues/new/choose';

/**
 * "Idea or bug?" page. Step 1 lets the player pick between the in-app contact form
 * (mailed to the moderators, no extra account needed) and a GitHub issue; step 2 is the
 * form itself; step 3 confirms delivery.
 */
@Component({
  selector: 'app-feedback',
  standalone: true,
  imports: [FormsModule, TranslocoDirective, ButtonModule, InputTextModule, TextareaModule, ToggleSwitchModule, LucideAngularModule],
  template: `
    <div class="feedback" *transloco="let t">
      <div class="g-card card">
        @switch (step()) {
          <!-- ── Step 1: choose a channel ─────────────────────────────── -->
          @case ('choose') {
            <header class="head">
              <span class="head-icon"><i-lucide [img]="LightbulbIcon" [size]="24" [strokeWidth]="2"></i-lucide></span>
              <h1 class="head-title">{{ t('feedback.title') }}</h1>
              <p class="head-sub">{{ t('feedback.subtitle') }}</p>
            </header>

            <div class="choices">
              <button
                type="button"
                class="choice"
                [class.is-disabled]="!contactEnabled()"
                [disabled]="!contactEnabled()"
                (click)="startForm()"
              >
                <span class="choice-icon primary"><i-lucide [img]="MailIcon" [size]="22" [strokeWidth]="2"></i-lucide></span>
                <span class="choice-body">
                  <span class="choice-title">
                    {{ t('feedback.choose.contact.title') }}
                    @if (contactEnabled()) {
                      <span class="choice-badge">{{ t('feedback.choose.contact.badge') }}</span>
                    }
                  </span>
                  <span class="choice-desc">
                    {{ contactEnabled() ? t('feedback.choose.contact.desc') : t('feedback.choose.contact.unavailable') }}
                  </span>
                </span>
                <i-lucide [img]="ChevronRightIcon" [size]="18" class="choice-arrow"></i-lucide>
              </button>

              <a class="choice" [href]="gitHubUrl()" target="_blank" rel="noopener noreferrer">
                <span class="choice-icon"><i-lucide [img]="GithubIcon" [size]="22" [strokeWidth]="2"></i-lucide></span>
                <span class="choice-body">
                  <span class="choice-title">{{ t('feedback.choose.github.title') }}</span>
                  <span class="choice-desc">{{ t('feedback.choose.github.desc') }}</span>
                </span>
                <i-lucide [img]="ExternalLinkIcon" [size]="16" class="choice-arrow"></i-lucide>
              </a>
            </div>

            <p class="fine-print">
              <i-lucide [img]="ShieldCheckIcon" [size]="14"></i-lucide>
              <span>{{ t('feedback.choose.privacy') }}</span>
            </p>
          }

          <!-- ── Step 2: the form ─────────────────────────────────────── -->
          @case ('form') {
            <header class="head compact">
              <button type="button" class="back" (click)="step.set('choose')">
                <i-lucide [img]="ArrowLeftIcon" [size]="15"></i-lucide>
                <span>{{ t('feedback.form.back') }}</span>
              </button>
              <h1 class="head-title">{{ t('feedback.form.title') }}</h1>
              <p class="head-sub">{{ t('feedback.form.subtitle') }}</p>
            </header>

            <form class="form" (ngSubmit)="submit()" novalidate>
              <fieldset class="field">
                <legend class="label">{{ t('feedback.form.categoryLabel') }}</legend>
                <div class="cats" role="radiogroup" [attr.aria-label]="t('feedback.form.categoryLabel')">
                  @for (c of categories; track c.key) {
                    <button
                      type="button"
                      class="cat"
                      role="radio"
                      [attr.aria-checked]="category() === c.key"
                      [class.active]="category() === c.key"
                      (click)="category.set(c.key)"
                    >
                      <i-lucide [img]="c.icon" [size]="16" [strokeWidth]="2"></i-lucide>
                      <span>{{ t(c.labelKey) }}</span>
                    </button>
                  }
                </div>
              </fieldset>

              <div class="field">
                <label class="label" for="fb-subject">{{ t('feedback.form.subject') }}</label>
                <input
                  pInputText
                  fluid
                  id="fb-subject"
                  name="subject"
                  type="text"
                  autocomplete="off"
                  [ngModel]="subject()"
                  (ngModelChange)="subject.set($event)"
                  (blur)="subjectTouched.set(true)"
                  [maxlength]="SUBJECT_MAX"
                  [invalid]="subjectTouched() && !subjectValid()"
                  [placeholder]="t('feedback.form.subjectPlaceholder.' + categoryKey())"
                />
                <div class="meta">
                  <span class="hint" [class.error]="subjectTouched() && !subjectValid()">
                    @if (subjectTouched() && !subjectValid()) {
                      {{ t('feedback.form.subjectTooShort', { min: SUBJECT_MIN }) }}
                    }
                  </span>
                  <span class="count">{{ subject().trim().length }}/{{ SUBJECT_MAX }}</span>
                </div>
              </div>

              <div class="field">
                <label class="label" for="fb-message">{{ t('feedback.form.message') }}</label>
                <textarea
                  pTextarea
                  fluid
                  id="fb-message"
                  name="message"
                  rows="7"
                  [ngModel]="message()"
                  (ngModelChange)="message.set($event)"
                  (blur)="messageTouched.set(true)"
                  [maxlength]="MESSAGE_MAX"
                  [invalid]="messageTouched() && !messageValid()"
                  [placeholder]="t('feedback.form.messagePlaceholder.' + categoryKey())"
                ></textarea>
                <div class="meta">
                  <span class="hint" [class.error]="messageTouched() && !messageValid()">
                    @if (messageTouched() && !messageValid()) {
                      {{ t('feedback.form.messageTooShort', { min: MESSAGE_MIN }) }}
                    }
                  </span>
                  <span class="count">{{ message().trim().length }}/{{ MESSAGE_MAX }}</span>
                </div>
              </div>

              @if (fromUrl(); as from) {
                <label class="attach">
                  <p-toggleswitch name="includePage" [ngModel]="includePage()" (ngModelChange)="includePage.set($event)" />
                  <span class="attach-text">
                    <span class="attach-label">{{ t('feedback.form.includePage') }}</span>
                    <code class="attach-url">{{ from }}</code>
                  </span>
                </label>
              }

              <p class="sender-note">
                <i-lucide [img]="ShieldCheckIcon" [size]="15"></i-lucide>
                <span>
                  @if (senderEmail(); as email) {
                    {{ t('feedback.form.attachNote', { name: senderName(), email }) }}
                  } @else {
                    {{ t('feedback.form.attachNoteNoEmail', { name: senderName() }) }}
                  }
                </span>
              </p>

              @if (error(); as err) {
                <div class="form-error" role="alert">
                  <span>{{ err }}</span>
                  @if (errorSuggestsGithub()) {
                    <a [href]="gitHubUrl()" target="_blank" rel="noopener noreferrer">
                      {{ t('feedback.choose.github.cta') }} <i-lucide [img]="ExternalLinkIcon" [size]="12"></i-lucide>
                    </a>
                  }
                </div>
              }

              <div class="actions">
                <p-button type="button" [label]="t('common.cancel')" severity="secondary" [text]="true" (onClick)="step.set('choose')" />
                <p-button type="submit" [label]="t('feedback.form.send')" icon="pi pi-send" [loading]="sending()" [disabled]="!canSubmit()" />
              </div>
            </form>
          }

          <!-- ── Step 3: confirmation ─────────────────────────────────── -->
          @case ('sent') {
            <div class="sent">
              <span class="sent-icon"><i-lucide [img]="CircleCheckIcon" [size]="34" [strokeWidth]="2"></i-lucide></span>
              <h1 class="head-title">{{ t('feedback.sent.title') }}</h1>
              <p class="head-sub">
                @if (senderEmail(); as email) {
                  {{ t('feedback.sent.body', { email }) }}
                } @else {
                  {{ t('feedback.sent.bodyNoEmail') }}
                }
              </p>
              <div class="actions center">
                <p-button [label]="backUrl() === '/' ? t('feedback.sent.home') : t('feedback.sent.backTo')" icon="pi pi-arrow-left" (onClick)="goBack()" />
                <p-button [label]="t('feedback.sent.another')" severity="secondary" [outlined]="true" (onClick)="reset()" />
              </div>
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display:block; }
    .feedback { max-width:46rem; margin:0 auto; }
    .card { padding:2rem 1.5rem; }
    @media (min-width: 768px) { .card { padding:2.5rem; } }

    /* Header */
    .head { display:flex; flex-direction:column; align-items:center; text-align:center; gap:0.375rem; margin-bottom:1.75rem; }
    .head.compact { align-items:flex-start; text-align:left; margin-bottom:1.5rem; }
    .head-icon { display:inline-flex; align-items:center; justify-content:center; width:3.25rem; height:3.25rem; border-radius:1rem; background:color-mix(in srgb, var(--p-primary-color) 14%, transparent); color:var(--p-primary-color); margin-bottom:0.5rem; }
    .head-title { margin:0; font-size:1.375rem; font-weight:700; color:var(--text-color); }
    .head-sub { margin:0; max-width:34rem; font-size:0.9375rem; line-height:1.5; color:var(--text-color-secondary); }
    .back { display:inline-flex; align-items:center; gap:0.375rem; margin-bottom:0.75rem; padding:0; border:0; background:transparent; color:var(--text-color-secondary); font-size:0.8125rem; font-weight:500; cursor:pointer; }
    .back:hover { color:var(--text-color); }

    /* Choices */
    .choices { display:flex; flex-direction:column; gap:0.75rem; }
    .choice { display:flex; align-items:center; gap:1rem; width:100%; padding:1.125rem 1.25rem; border:1px solid var(--surface-border); border-radius:1rem; background:var(--p-surface-800); color:inherit; text-align:left; text-decoration:none; cursor:pointer; transition:border-color var(--transition-duration), background-color var(--transition-duration), transform var(--transition-duration); }
    .choice:hover:not(.is-disabled) { border-color:color-mix(in srgb, var(--p-primary-color) 55%, transparent); background:var(--surface-hover); }
    .choice:focus-visible { outline:2px solid var(--p-primary-color); outline-offset:2px; }
    .choice.is-disabled { cursor:not-allowed; opacity:0.6; }
    .choice-icon { display:inline-flex; align-items:center; justify-content:center; width:2.75rem; height:2.75rem; border-radius:0.875rem; background:var(--p-surface-700); color:var(--text-color); flex-shrink:0; }
    .choice-icon.primary { background:color-mix(in srgb, var(--p-primary-color) 16%, transparent); color:var(--p-primary-color); }
    .choice-body { display:flex; flex-direction:column; gap:0.25rem; min-width:0; flex:1; }
    .choice-title { display:flex; align-items:center; flex-wrap:wrap; gap:0.5rem; font-size:0.9375rem; font-weight:600; color:var(--text-color); }
    .choice-badge { font-size:0.625rem; font-weight:700; letter-spacing:0.06em; text-transform:uppercase; padding:0.125rem 0.5rem; border-radius:9999px; background:color-mix(in srgb, var(--p-primary-color) 18%, transparent); color:var(--p-primary-color); }
    .choice-desc { font-size:0.8125rem; line-height:1.45; color:var(--text-color-secondary); }
    .choice-arrow { color:var(--text-color-secondary); flex-shrink:0; }
    .fine-print { display:flex; align-items:flex-start; gap:0.5rem; margin:1.25rem 0 0; font-size:0.75rem; line-height:1.5; color:var(--text-color-secondary); }
    .fine-print i-lucide { flex-shrink:0; margin-top:0.125rem; }

    /* Form */
    .form { display:flex; flex-direction:column; gap:1.25rem; }
    .field { display:flex; flex-direction:column; gap:0.5rem; margin:0; padding:0; border:0; min-width:0; }
    .label { font-size:0.8125rem; font-weight:600; color:var(--text-color); }
    .cats { display:flex; flex-wrap:wrap; gap:0.5rem; }
    .cat { display:inline-flex; align-items:center; gap:0.375rem; padding:0.5rem 0.875rem; border:1px solid var(--surface-border); border-radius:9999px; background:transparent; color:var(--text-color-secondary); font-size:0.8125rem; font-weight:500; cursor:pointer; transition:background-color var(--transition-duration), border-color var(--transition-duration), color var(--transition-duration); }
    .cat:hover { background:var(--surface-hover); color:var(--text-color); }
    .cat.active { border-color:color-mix(in srgb, var(--p-primary-color) 60%, transparent); background:color-mix(in srgb, var(--p-primary-color) 14%, transparent); color:var(--text-color); }
    .meta { display:flex; justify-content:space-between; gap:0.75rem; font-size:0.75rem; color:var(--text-color-secondary); min-height:1rem; }
    .hint.error { color:var(--p-red-400); }
    .count { flex-shrink:0; font-variant-numeric:tabular-nums; }
    textarea { resize:vertical; min-height:8rem; }
    .attach { display:flex; align-items:center; gap:0.875rem; padding:0.875rem 1rem; border-radius:0.875rem; background:var(--p-surface-800); cursor:pointer; }
    .attach-text { display:flex; flex-direction:column; gap:0.125rem; min-width:0; }
    .attach-label { font-size:0.8125rem; font-weight:500; color:var(--text-color); }
    .attach-url { font-size:0.75rem; color:var(--text-color-secondary); overflow:hidden; text-overflow:ellipsis; white-space:nowrap; }
    .sender-note { display:flex; align-items:flex-start; gap:0.5rem; margin:0; font-size:0.75rem; line-height:1.5; color:var(--text-color-secondary); }
    .sender-note i-lucide { flex-shrink:0; margin-top:0.125rem; }
    .form-error { display:flex; flex-wrap:wrap; align-items:center; gap:0.5rem 0.75rem; padding:0.75rem 1rem; border-radius:0.75rem; border:1px solid color-mix(in srgb, var(--p-red-400) 45%, transparent); background:color-mix(in srgb, var(--p-red-400) 10%, transparent); color:var(--text-color); font-size:0.8125rem; }
    .form-error a { display:inline-flex; align-items:center; gap:0.25rem; color:var(--p-primary-400); font-weight:500; }
    .actions { display:flex; justify-content:flex-end; flex-wrap:wrap; gap:0.5rem; margin-top:0.25rem; }
    .actions.center { justify-content:center; margin-top:1.5rem; }

    /* Sent */
    .sent { display:flex; flex-direction:column; align-items:center; text-align:center; gap:0.375rem; padding:1rem 0; }
    .sent-icon { display:inline-flex; align-items:center; justify-content:center; width:4rem; height:4rem; border-radius:50%; background:color-mix(in srgb, var(--p-green-400) 16%, transparent); color:var(--p-green-400); margin-bottom:0.75rem; }
  `],
})
export class FeedbackComponent {
  readonly LightbulbIcon = Lightbulb;
  readonly MailIcon = Mail;
  readonly GithubIcon = Github;
  readonly ChevronRightIcon = ChevronRight;
  readonly ExternalLinkIcon = ExternalLink;
  readonly ShieldCheckIcon = ShieldCheck;
  readonly ArrowLeftIcon = ArrowLeft;
  readonly CircleCheckIcon = CircleCheck;

  readonly SUBJECT_MIN = SUBJECT_MIN;
  readonly SUBJECT_MAX = SUBJECT_MAX;
  readonly MESSAGE_MIN = MESSAGE_MIN;
  readonly MESSAGE_MAX = MESSAGE_MAX;

  readonly categories: CategoryOption[] = [
    { key: 'Bug', icon: Bug, labelKey: 'feedback.form.category.bug' },
    { key: 'Idea', icon: Lightbulb, labelKey: 'feedback.form.category.idea' },
    { key: 'Question', icon: MessageCircleQuestion, labelKey: 'feedback.form.category.question' },
    { key: 'Other', icon: MessageSquare, labelKey: 'feedback.form.category.other' },
  ];

  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly transloco = inject(TranslocoService);

  readonly step = signal<Step>('choose');
  readonly config = signal<FeedbackConfigResponse | null>(null);

  readonly category = signal<FeedbackCategory>('Bug');
  readonly subject = signal('');
  readonly message = signal('');
  readonly includePage = signal(true);
  readonly subjectTouched = signal(false);
  readonly messageTouched = signal(false);
  readonly sending = signal(false);
  readonly error = signal<string | null>(null);
  readonly errorSuggestsGithub = signal(false);

  /** In-app path the player came from (footer/menu link), only kept when it is a safe relative path. */
  readonly fromUrl = signal<string | null>(FeedbackComponent.sanitizeFrom(this.route.snapshot.queryParamMap.get('from')));

  // While the config is loading, assume the form works: a wrong guess just surfaces as a clear error on submit.
  readonly contactEnabled = computed(() => this.config()?.contactEnabled ?? true);
  readonly gitHubUrl = computed(() => this.config()?.gitHubIssuesUrl ?? DEFAULT_ISSUES_URL);
  readonly categoryKey = computed(() => this.category().toLowerCase());
  readonly subjectValid = computed(() => this.subject().trim().length >= SUBJECT_MIN);
  readonly messageValid = computed(() => this.message().trim().length >= MESSAGE_MIN);
  readonly canSubmit = computed(() => this.subjectValid() && this.messageValid() && !this.sending());
  readonly senderName = computed(() => this.auth.user()?.displayName ?? '');
  readonly senderEmail = computed(() => {
    const email = this.auth.user()?.email;
    // The offline auth stub fabricates "<name>@offline" addresses; nobody can be reached there.
    return email && !email.endsWith('@offline') ? email : null;
  });
  // A table URL is not a place to "go back" to: leaving the table already ended that seat.
  readonly backUrl = computed(() => {
    const from = this.fromUrl();
    return from && !from.startsWith('/table') ? from : '/';
  });

  constructor() {
    this.api.getFeedbackConfig().subscribe((cfg) => this.config.set(cfg));
  }

  startForm(): void {
    if (!this.contactEnabled()) return;
    this.error.set(null);
    this.step.set('form');
  }

  submit(): void {
    this.subjectTouched.set(true);
    this.messageTouched.set(true);
    if (!this.canSubmit()) return;

    this.sending.set(true);
    this.error.set(null);
    this.errorSuggestsGithub.set(false);

    this.api
      .sendFeedback({
        category: this.category(),
        subject: this.subject().trim(),
        message: this.message().trim(),
        pageUrl: this.includePage() ? this.fromUrl() : null,
        language: this.transloco.getActiveLang(),
      })
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.step.set('sent');
        },
        error: (err: HttpErrorResponse) => {
          this.sending.set(false);
          this.error.set(this.describeError(err));
        },
      });
  }

  reset(): void {
    this.subject.set('');
    this.message.set('');
    this.subjectTouched.set(false);
    this.messageTouched.set(false);
    this.error.set(null);
    this.step.set('form');
  }

  goBack(): void {
    this.router.navigateByUrl(this.backUrl());
  }

  private describeError(err: HttpErrorResponse): string {
    if (err.status === 429) return this.transloco.translate('feedback.errors.rateLimited');
    if (err.status === 503) {
      this.errorSuggestsGithub.set(true);
      this.config.update((c) => (c ? { ...c, contactEnabled: false } : c));
      return this.transloco.translate('feedback.errors.unavailable');
    }
    if (err.status === 400 && typeof err.error?.error === 'string') return err.error.error;
    if (err.status === 0) return this.transloco.translate('errors.connectionIssue');
    this.errorSuggestsGithub.set(true);
    return this.transloco.translate('feedback.errors.failed');
  }

  private static sanitizeFrom(value: string | null): string | null {
    if (!value) return null;
    if (!value.startsWith('/') || value.startsWith('//')) return null;
    if (value.startsWith('/feedback')) return null;
    return value.length > 500 ? value.slice(0, 500) : value;
  }
}
