import { describe, expect, it } from 'vitest';
import { facebookShareUrl, shareFileName, taggedUrl, whatsappShareUrl } from './clip-share-links';

const PAGE = 'https://play.giretra.com/c/OLzBWUjx7q';

describe('clip share links', () => {
  it('tags the page URL with the channel', () => {
    expect(taggedUrl(PAGE, 'copy')).toBe(`${PAGE}?utm_source=copy&utm_medium=share`);
  });

  it('builds a WhatsApp link with the message and the tagged URL', () => {
    const url = new URL(whatsappShareUrl(PAGE, 'Haga vs Bot — All Trumps'));
    expect(url.origin + url.pathname).toBe('https://wa.me/');
    expect(url.searchParams.get('text')).toBe(
      `Haga vs Bot — All Trumps\n${PAGE}?utm_source=whatsapp&utm_medium=share`,
    );
  });

  it('builds a Facebook sharer link', () => {
    const url = new URL(facebookShareUrl(PAGE));
    expect(url.origin + url.pathname).toBe('https://www.facebook.com/sharer/sharer.php');
    expect(url.searchParams.get('u')).toBe(`${PAGE}?utm_source=facebook&utm_medium=share`);
  });

  it('names the shared file after the clip', () => {
    expect(shareFileName('OLzBWUjx7q')).toBe('giretra-OLzBWUjx7q.mp4');
  });
});
