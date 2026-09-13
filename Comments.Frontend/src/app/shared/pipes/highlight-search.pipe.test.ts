import { HighlightSearchPipe } from './highlight-search.pipe';
import { describe, expect, it } from 'vitest';

describe('HighlightSearchPipe', () => {
  it('highlights the complete phrase instead of separate words', () => {
    const result = new HighlightSearchPipe().transform('red green red-green', 'red green');

    expect(result).toContain('<mark>red green</mark>');
    expect(result).not.toContain('<mark>red</mark>');
  });
});
