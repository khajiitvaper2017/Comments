import { describe, expect, it } from 'vitest';
import {
  validateEmail,
  validateHomePage,
  validateMarkup,
  validateText,
  validateUserName,
} from './comment-validation';

describe('comment validation', () => {
  it('matches the server username, e-mail, and homepage rules', () => {
    expect(validateUserName('User123')).toBe('');
    expect(validateUserName('User 123')).not.toBe('');
    expect(validateUserName('a'.repeat(101))).not.toBe('');
    expect(validateEmail('user@example.com')).toBe('');
    expect(validateEmail('invalid')).not.toBe('');
    expect(validateHomePage('example.com')).not.toBe('');
    expect(validateHomePage('https://example.com')).toBe('');
    expect(validateHomePage(`https://${'a'.repeat(250)}.com`)).not.toBe('');
    expect(validateHomePage('ftp://example.com')).not.toBe('');
  });

  it('enforces text length and allowed markup', () => {
    expect(validateText('<strong>Valid</strong>')).toBe('');
    expect(validateMarkup('<strong><i>Nested</i></strong>')).toBe('');
    expect(validateMarkup('<strong>Unclosed')).toBe('Invalid XHTML.');
    expect(validateMarkup('<script>alert(1)</script>')).toBe('Invalid XHTML.');
    expect(validateText('x'.repeat(5001))).not.toBe('');
  });
});
