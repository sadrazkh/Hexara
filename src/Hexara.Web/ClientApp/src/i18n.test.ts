import { describe, expect, it } from 'vitest';
import fa from '@locales/fa.json';
import en from '@locales/en.json';
import { flatten } from './i18n';

/**
 * فایل ترجمه یکی است و دو خواننده دارد: ‎UiTranslator‎ در سرور و همین ماژول در
 * کلاینت. این تست‌ها می‌سنجند که خواننده‌ی کلاینت همان قاعده را دارد، چون تفاوتشان
 * جایی لو می‌رود که کاربر است: متن در Razor درست بود و در Vue خامِ کلید می‌مانْد.
 */
describe('flatten', () => {
  it('joins nested objects with dots', () => {
    expect(flatten({ game: { phase: { Roll: 'Roll the dice' } } })).toEqual({
      'game.phase.Roll': 'Roll the dice'
    });
  });

  it('keeps keys that are already written with dots', () => {
    expect(flatten({ game: { 'phase.Roll': 'Roll the dice' } })).toEqual({
      'game.phase.Roll': 'Roll the dice'
    });
  });

  /**
   * شکلِ دقیقِ اشکالی که در بازیِ واقعی دیده شد: ‎phase‎ هم برچسبِ رشته‌ای است و
   * هم پیشوندِ ‎phase.Roll‎. خواننده‌ی قبلی درخت را می‌پیمود، به رشته می‌رسید و
   * جا می‌ماند؛ صاف‌کردن هر دو را می‌بیند.
   */
  it('reads a dotted key whose prefix is also a plain label', () => {
    expect(flatten({ game: { phase: 'Phase', 'phase.Roll': 'Roll the dice' } })).toEqual({
      'game.phase': 'Phase',
      'game.phase.Roll': 'Roll the dice'
    });
  });

  it('stringifies numbers and booleans', () => {
    expect(flatten({ a: 1, b: true })).toEqual({ a: '1', b: 'true' });
  });
});

describe('the shipped catalogs', () => {
  const flatFa = flatten(fa);
  const flatEn = flatten(en);

  /**
   * نام‌های مرحله از ‎TurnPhase‎ در دامنه می‌آیند و همان‌طور که هستند به کلید
   * تبدیل می‌شوند. اگر مرحله‌ای اضافه شد و ترجمه نداشت، اینجا می‌افتد.
   */
  const phases = [
    'SetupSettlement',
    'SetupRoad',
    'Roll',
    'Discard',
    'MoveRobber',
    'Main',
    'GameOver'
  ];

  it.each(phases)('translates the %s phase in both languages', (phase) => {
    expect(flatFa[`game.phase.${phase}`]).toBeTruthy();
    expect(flatEn[`game.phase.${phase}`]).toBeTruthy();
  });

  it('has the same keys in both languages', () => {
    expect(Object.keys(flatEn).sort()).toEqual(Object.keys(flatFa).sort());
  });

  it('has no blank translations', () => {
    const blank = [...Object.entries(flatFa), ...Object.entries(flatEn)]
      .filter(([, value]) => value.trim() === '')
      .map(([key]) => key);

    expect(blank).toEqual([]);
  });
});
