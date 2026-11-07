/**
 * Share targets for a clip's public page. Every link carries UTM tags so the landing-page
 * analytics can tell channels apart; the landing page's canonical/og:url stays the clean
 * URL, so Facebook and WhatsApp treat all variants as the same object.
 */
export type ShareChannel = 'whatsapp' | 'facebook' | 'native' | 'copy';

export function taggedUrl(pageUrl: string, channel: ShareChannel): string {
  const url = new URL(pageUrl);
  url.searchParams.set('utm_source', channel);
  url.searchParams.set('utm_medium', 'share');
  return url.toString();
}

/** Opens the WhatsApp app (or web) with the message pre-filled. */
export function whatsappShareUrl(pageUrl: string, message: string): string {
  const text = `${message}\n${taggedUrl(pageUrl, 'whatsapp')}`;
  return `https://wa.me/?text=${encodeURIComponent(text)}`;
}

/** Facebook's sharer ignores custom text; the caption comes from the Open Graph tags. */
export function facebookShareUrl(pageUrl: string): string {
  return `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(taggedUrl(pageUrl, 'facebook'))}`;
}

/** File name of the MP4 when it goes through the system share sheet. */
export function shareFileName(shortId: string): string {
  return `giretra-${shortId}.mp4`;
}
