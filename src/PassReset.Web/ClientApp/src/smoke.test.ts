import { describe, it, expect } from 'vitest';

describe('vitest smoke', () => {
  it('runs a trivial assertion', () => {
    expect(1 + 1).toBe(2);
  });

  it('has jsdom environment', () => {
    expect(typeof document).toBe('object');
    expect(document.createElement('div')).toBeInstanceOf(HTMLElement);
  });
});
