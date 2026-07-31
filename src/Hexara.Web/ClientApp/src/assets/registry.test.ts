import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import fa from '@locales/fa.json';
import en from '@locales/en.json';
import { flatten } from '@/i18n';
import {
  ASSETS,
  TERRAIN_ART,
  assetFor,
  assetSpec,
  terrainArt,
  type AssetName,
} from './registry';

const names = Object.keys(ASSETS) as AssetName[];
const flatFa = flatten(fa);
const flatEn = flatten(en);

describe('the asset registry', () => {
  it('is not empty', () => {
    expect(names.length).toBeGreaterThan(0);
  });

  /**
   * برچسبِ هر دارایی هم ‎alt‎ می‌شود و هم متنِ جانشین، پس نبودنش یعنی کاربر
   * یک کلیدِ خام می‌بیند یا صفحه‌خوان چیزی برای گفتن ندارد.
   */
  it.each(names)('gives %s a label that exists in both languages', (name) => {
    const { labelKey } = assetSpec(name);

    expect(flatFa[labelKey], `fa: ${labelKey}`).toBeTruthy();
    expect(flatEn[labelKey], `en: ${labelKey}`).toBeTruthy();
  });

  it('gives every asset a shape, so the box is reserved before the art arrives', () => {
    for (const name of names) {
      expect(['card', 'square', 'hex', 'wide']).toContain(assetSpec(name).shape);
    }
  });

  it('builds keys from server enum names', () => {
    expect(assetFor('resource', 'Lumber')).toBe('resource.Lumber');
    expect(assetFor('dev', 'Knight')).toBe('dev.Knight');
  });

  it('returns null for something it has no art for, instead of a broken key', () => {
    expect(assetFor('resource', 'Unobtainium')).toBeNull();
  });
});

describe('terrain art variations', () => {
  it('registers three optimized variations for every terrain family', () => {
    for (const variants of Object.values(TERRAIN_ART)) {
      expect(variants).toHaveLength(3);
      expect(new Set(variants).size).toBe(3);
    }
  });

  it('chooses a stable variation from axial coordinates', () => {
    expect(terrainArt('Forest', -2, 1)).toBe(terrainArt('Forest', -2, 1));
    expect(TERRAIN_ART.Forest).toContain(terrainArt('Forest', -2, 1));
  });

  it('returns null for an unknown terrain', () => {
    expect(terrainArt('Ocean', 0, 0)).toBeNull();
  });
});

/**
 * قاعده‌ی فاز صفر: هیچ کامپوننتی حق ندارد مستقیم به فایل تصویر اشاره کند.
 *
 * دلیلش این است که قرار است تصویرهای حرفه‌ای جای موقتی‌ها بنشینند؛ اگر مسیرها
 * پخش باشند آن روز باید ده جا عوض شود و هر کدام اندازه‌ی خودش را می‌گیرد. تنها
 * ‎registry.ts‎ اجازه دارد فایل بشناسد.
 */
describe('no component reaches for an image file directly', () => {
  const root = join(__dirname, '..');
  const allowed = new Set(['registry.ts', 'registry.test.ts']);

  /**
   * ‎pwa/‎ بیرون از این قاعده است و باید باشد: کارگرِ سرویس فهرست پیش‌کش را از
   * مانیفست ‎Vite‎ می‌سازد، پس کارش *همین* شناختن نام فایل‌هاست. قاعده درباره‌ی
   * چیزی است که تصویر را نشان می‌دهد، نه چیزی که فایل‌ها را کش می‌کند.
   */
  const exempt = new Set(['pwa']);

  function sources(dir: string): string[] {
    return readdirSync(dir).flatMap((entry) => {
      const path = join(dir, entry);
      if (statSync(path).isDirectory()) return exempt.has(entry) ? [] : sources(path);

      return /\.(vue|ts)$/.test(entry) && !allowed.has(entry) ? [path] : [];
    });
  }

  it('keeps every image path inside the registry', () => {
    // مسیرهای تصویر، نه هر رشته‌ای که نقطه دارد؛ ‎favicon‎ و آیکن‌های PWA در
    // Razor و مانیفست‌اند و ربطی به کامپوننت‌ها ندارند.
    const pattern = /['"`][^'"`]*\.(svg|png|jpe?g|webp|gif)['"`]/i;

    const offenders = sources(root)
      .filter((path) => pattern.test(readFileSync(path, 'utf8')))
      .map((path) => path.slice(root.length + 1));

    expect(offenders).toEqual([]);
  });
});
