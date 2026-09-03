/**
 * Everything that makes this install *this gym* rather than any other.
 *
 * **This is the file to edit when cloning the system for a new customer.** Resale is by
 * cloning the repository per gym and changing it by hand, so the goal is one obvious place
 * to change rather than a theming engine — that was considered and rejected, because under
 * roughly five customers it never pays for itself.
 *
 * What belongs here is *identity*: the things fixed when a gym is set up and then left
 * alone. What does not belong here is *content* — opening hours, address, phone, the copy
 * on the homepage. Those live in Gym settings, in the database, because the owner changes
 * them without a developer and should not need one.
 *
 * The one overlap is the name. It sits here as well so the app has something to show before
 * the first API call returns, and so a gym that never opens Settings still does not read
 * "Fit Bear". Wherever the saved name exists it wins; this is the fallback.
 *
 * The backend has its own copy of the name under `Gym:Name` in `appsettings.json`, used for
 * the emails it sends. Two files rather than one, because a TypeScript constant cannot be
 * read from C# — they are listed together in the README so neither gets missed.
 */
export const GYM = {
  /** Full name, as it appears on the public homepage and in the browser tab. */
  name: 'The Fit Bear Gym',

  /** Short form for the admin sidebar, where the full name wraps awkwardly. */
  shortName: 'Fit Bear Gym',

  /**
   * Shown before the name everywhere the name appears. An emoji keeps a clone one
   * character away from being rebranded; set it to an empty string for no mark at all,
   * or replace the `Logo` component below with an image.
   */
  mark: '🐻',

  /** The one line under the name on the homepage, until Gym settings overrides it. */
  tagline: 'Where Strength Meets Nature',

  /**
   * The brand colour, and the two shades built from it.
   *
   * `main` is what everything is derived from — buttons, links, the sidebar. `dark` is the
   * admin chrome, `light` is hover states. Changing all three together is what keeps a
   * rebrand from looking half-finished.
   */
  colour: {
    main: '#2e7d32',
    light: '#4caf50',
    dark: '#1b5e20',

    /** Only for the deep gradients on the public homepage. */
    deepest: '#0d4416',
  },

  /**
   * The secondary colour, used for the few actions that sit beside a primary one. Keep it
   * related to `colour.main` — an unrelated hue here is what makes a rebrand look accidental.
   */
  accent: {
    main: '#66bb6a',
    light: '#81c784',
    dark: '#388e3c',
  },

  /**
   * Whether to draw the bear illustration on the homepage.
   *
   * It is drawn in code rather than loaded as an image, and it is a *bear* — which is right
   * for this gym and wrong for most others. A clone that has not drawn its own should turn
   * this off rather than ship somebody else's mascot.
   */
  showMascot: true,
} as const;

/** The name with its mark, which is how it is written nearly everywhere. */
export const gymLabel = (name: string = GYM.name): string =>
  GYM.mark ? `${GYM.mark} ${name}` : name;

/**
 * The brand colour at partial opacity, for shadows and hover tints.
 *
 * These used to be written as literal `rgba(46, 125, 50, 0.2)` — the same green a third
 * time, in a form that no search for the hex would ever find. A rebrand changed the buttons
 * and left every shadow behind it the old colour.
 */
export const gymTint = (alpha: number, hex: string = GYM.colour.main): string => {
  const value = hex.replace('#', '');
  const r = parseInt(value.slice(0, 2), 16);
  const g = parseInt(value.slice(2, 4), 16);
  const b = parseInt(value.slice(4, 6), 16);
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
};
