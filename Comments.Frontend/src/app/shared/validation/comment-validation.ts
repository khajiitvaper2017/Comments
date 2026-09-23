const emailPattern = /^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$/;
const allowedTagPattern = /<\/?(a|code|i|strong)(?:\s+[^>]*)?\s*\/?>/gi;
const tagPattern = /<\/?(a|code|i|strong)(?:\s+[^>]*)?\s*\/?>/gi;

export function validateUserName(value: string): string {
  return !value || value.length > 100 || !/^[A-Za-z0-9]+$/.test(value)
    ? 'User Name must contain only Latin letters and digits.'
    : '';
}

export function validateEmail(value: string): string {
  return value.length <= 254 && emailPattern.test(value) ? '' : 'A valid e-mail is required.';
}

export function validateHomePage(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) return '';
  if (trimmed.length > 254) return 'Home page must be at most 254 characters.';
  try {
    const url = new URL(trimmed);
    return ['http:', 'https:'].includes(url.protocol) && !!url.hostname
      ? ''
      : 'Home page must be a valid HTTP(S) URL.';
  } catch {
    return 'Home page must be a valid HTTP(S) URL.';
  }
}

export function validateText(value: string): string {
  if (!value.trim() || value.length > 5000)
    return 'Text is required and must be at most 5000 characters.';
  return validateMarkup(value);
}

export function validateMarkup(value: string): string {
  const remaining = value.replace(allowedTagPattern, '');
  if (/[<>]/.test(remaining)) return 'Invalid XHTML.';

  const stack: string[] = [];
  for (const match of value.matchAll(tagPattern)) {
    const tag = match[1].toLowerCase();
    if (match[0].startsWith('</')) {
      if (stack.pop() !== tag) return 'Invalid XHTML.';
    } else if (!match[0].endsWith('/>')) {
      stack.push(tag);
    }
  }
  return stack.length ? 'Invalid XHTML.' : '';
}
