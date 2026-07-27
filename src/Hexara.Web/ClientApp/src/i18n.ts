import fa from '@locales/fa.json';
import en from '@locales/en.json';

type Nested = { [key: string]: string | number | boolean | Nested };

const catalogs: Record<string, Nested> = { fa: fa as Nested, en: en as Nested };

export const DEFAULT_CULTURE = 'fa';

/** زبان جاری از خودِ سند خوانده می‌شود؛ سرور آن را روی <html lang> گذاشته است. */
export function currentCulture(): string {
  const lang = document.documentElement.lang?.split('-')[0]?.toLowerCase();
  return lang && lang in catalogs ? lang : DEFAULT_CULTURE;
}

export function isRtl(culture = currentCulture()): boolean {
  return culture === 'fa';
}

function lookup(catalog: Nested | undefined, key: string): string | undefined {
  if (!catalog) return undefined;

  let node: Nested | string | number | boolean | undefined = catalog;
  for (const part of key.split('.')) {
    if (typeof node !== 'object' || node === null) return undefined;
    node = (node as Nested)[part];
  }

  return typeof node === 'string' ? node : undefined;
}

/**
 * ترجمه با همان کلیدهای سمت سرور. اگر کلید نبود، به فارسی و در نهایت به
 * خودِ کلید برمی‌گردد تا کمبود ترجمه در UI دیده شود.
 */
export function t(key: string, ...args: unknown[]): string {
  const culture = currentCulture();
  const value = lookup(catalogs[culture], key) ?? lookup(catalogs[DEFAULT_CULTURE], key) ?? key;

  return args.length === 0
    ? value
    : value.replace(/\{(\d+)\}/g, (match, index: string) => String(args[Number(index)] ?? match));
}
