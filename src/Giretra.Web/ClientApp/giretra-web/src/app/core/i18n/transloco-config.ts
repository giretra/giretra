import { isDevMode } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { TranslocoHttpLoader } from './transloco-loader';

function getDefaultLang(): string {
  const stored = localStorage.getItem('giretra-lang');
  if (stored && ['en', 'fr', 'mg'].includes(stored)) {
    return stored;
  }
  const browserLang = navigator.language?.split('-')[0];
  if (browserLang && ['en', 'fr', 'mg'].includes(browserLang)) {
    return browserLang;
  }
  return 'en';
}

export function provideAppTransloco() {
  return provideTransloco({
    config: {
      availableLangs: ['en', 'fr', 'mg'],
      defaultLang: getDefaultLang(),
      fallbackLang: 'en',
      reRenderOnLangChange: true,
      prodMode: !isDevMode(),
      missingHandler: {
        useFallbackTranslation: true,
      },
    },
    loader: TranslocoHttpLoader,
  });
}
