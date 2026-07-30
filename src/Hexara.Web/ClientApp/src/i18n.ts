import fa from '@locales/fa.json';
import en from '@locales/en.json';

type Nested = { [key: string]: string | number | boolean | Nested };

/**
 * کاتالوگ را صاف می‌کند: ‎{ game: { phase: { Roll: 'x' } } }‎ می‌شود
 * ‎'game.phase.Roll'‎.
 *
 * دقیقاً همان کاری است که ‎UiTranslator‎ سمت سرور می‌کند، و عمداً همان‌طور نوشته
 * شده. فایل ترجمه یکی است ولی دو خواننده دارد؛ اگر این دو هم‌قاعده نباشند متنی
 * که در Razor درست دیده می‌شود در Vue خامِ کلید می‌مانَد. این‌طور هر دو شکلِ
 * نوشتن — تودرتو و کلیدِ نقطه‌دار — در هر دو طرف یکسان خوانده می‌شود، حتی وقتی
 * ‎"phase"‎ هم‌زمان یک برچسبِ رشته‌ای و پیشوندِ ‎"phase.Roll"‎ باشد.
 */
export function flatten(source: Nested, prefix?: string): Record<string, string> {
  const sink: Record<string, string> = {};

  for (const [key, value] of Object.entries(source)) {
    const path = prefix === undefined ? key : `${prefix}.${key}`;

    if (typeof value === 'object' && value !== null) {
      Object.assign(sink, flatten(value, path));
    } else {
      sink[path] = String(value);
    }
  }

  return sink;
}

const catalogs: Record<string, Record<string, string>> = {
  fa: flatten(fa as Nested),
  en: flatten(en as Nested)
};

export const DEFAULT_CULTURE = 'fa';

/** زبان جاری از خودِ سند خوانده می‌شود؛ سرور آن را روی <html lang> گذاشته است. */
export function currentCulture(): string {
  const lang = document.documentElement.lang?.split('-')[0]?.toLowerCase();
  return lang && lang in catalogs ? lang : DEFAULT_CULTURE;
}

export function isRtl(culture = currentCulture()): boolean {
  return culture === 'fa';
}

/**
 * ترجمه با همان کلیدهای سمت سرور. اگر کلید نبود، به فارسی و در نهایت به
 * خودِ کلید برمی‌گردد تا کمبود ترجمه در UI دیده شود.
 */
export function t(key: string, ...args: unknown[]): string {
  const culture = currentCulture();
  const value = catalogs[culture]?.[key] ?? catalogs[DEFAULT_CULTURE]?.[key] ?? key;

  return args.length === 0
    ? value
    : value.replace(/\{(\d+)\}/g, (match, index: string) => String(args[Number(index)] ?? match));
}
