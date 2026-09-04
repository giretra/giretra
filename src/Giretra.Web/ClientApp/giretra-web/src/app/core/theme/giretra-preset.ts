import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * PrimeNG preset aligned with Giretra's own HSL tokens in src/styles.css, so the Freya
 * layout chrome (topbar, sidebar, breadcrumb) lands on the same dark blue-grey surfaces
 * and green primary as the untouched feature pages. Aura maps content.background to
 * surface.900, content.borderColor to surface.700 and text.mutedColor to surface.400.
 */
export const GiretraPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: 'hsl(142 50% 95%)',
      100: 'hsl(142 50% 90%)',
      200: 'hsl(142 50% 80%)',
      300: 'hsl(142 50% 62%)',
      400: 'hsl(142 50% 45%)',
      500: 'hsl(142 50% 35%)', // --primary
      600: 'hsl(142 50% 30%)',
      700: 'hsl(142 50% 25%)',
      800: 'hsl(142 50% 20%)',
      900: 'hsl(142 50% 15%)',
      950: 'hsl(142 50% 10%)',
    },
    colorScheme: {
      dark: {
        surface: {
          0: 'hsl(210 40% 96%)', // --foreground
          50: 'hsl(210 40% 92%)',
          100: 'hsl(212 32% 85%)',
          200: 'hsl(214 26% 76%)',
          300: 'hsl(215 20% 66%)',
          400: 'hsl(215 16% 57%)', // --muted-foreground
          500: 'hsl(218 15% 45%)',
          600: 'hsl(220 15% 25%)', // --muted
          700: 'hsl(220 15% 20%)', // --border
          800: 'hsl(220 18% 17%)', // hover
          900: 'hsl(220 20% 14%)', // --card
          950: 'hsl(220 20% 10%)', // --background
        },
        primary: {
          color: '{primary.500}',
          contrastColor: 'hsl(0 0% 98%)',
          hoverColor: '{primary.400}',
          activeColor: '{primary.300}',
        },
      },
    },
  },
});
