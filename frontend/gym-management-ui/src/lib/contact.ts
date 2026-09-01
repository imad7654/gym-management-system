/**
 * Turns a member's phone number into links reception can tap.
 *
 * The digits come from the server, which strips them with the same rule the rest of the
 * system matches members by — so a number stored as "+961 70 123 456" and one stored as
 * "70123456" produce the same link.
 */
export interface ContactLinks {
  tel: string;
  whatsapp: string;
}

/** Lebanon's country calling code. WhatsApp needs the international form; `tel:` works with it too. */
const LEBANON = '961';

export const contactLinks = (phoneDigits?: string | null): ContactLinks | null => {
  if (!phoneDigits) return null;

  const international = phoneDigits.startsWith(LEBANON)
    ? phoneDigits
    : `${LEBANON}${phoneDigits}`;

  return {
    tel: `tel:+${international}`,
    // wa.me opens WhatsApp on a phone and WhatsApp Web on a desktop, so one link covers
    // both of the places this app is used.
    whatsapp: `https://wa.me/${international}`,
  };
};
