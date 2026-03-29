const UPPERCASE = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
const LOWERCASE = 'abcdefghijklmnopqrstuvwxyz';
const DIGITS    = '0123456789';
const SYMBOLS   = '!@#$%^&*()-_=+[]{}|;:,.<>?';
const ALL       = UPPERCASE + LOWERCASE + DIGITS + SYMBOLS;

/**
 * Returns a uniform random index in [0, max) using rejection sampling
 * to eliminate modulo bias from crypto.getRandomValues.
 */
function secureRandomIndex(max: number): number {
  const limit = Math.floor(0x100000000 / max) * max; // largest multiple of max within Uint32 range
  let value: number;
  do {
    value = crypto.getRandomValues(new Uint32Array(1))[0];
  } while (value >= limit);
  return value % max;
}

/**
 * Generates a random password that satisfies the minimum entropy character count.
 * Always includes at least one character from each category.
 */
export function generatePassword(minLength: number): string {
  const length = Math.max(minLength, 12);
  const pick = (charset: string) => charset[secureRandomIndex(charset.length)];

  const chars = [pick(UPPERCASE), pick(LOWERCASE), pick(DIGITS), pick(SYMBOLS)];

  for (let i = chars.length; i < length; i++) {
    chars.push(pick(ALL));
  }

  // Fisher-Yates shuffle
  for (let i = chars.length - 1; i > 0; i--) {
    const j = secureRandomIndex(i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }

  return chars.join('');
}
