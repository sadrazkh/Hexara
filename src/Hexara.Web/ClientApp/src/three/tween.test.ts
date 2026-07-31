import { describe, expect, it } from 'vitest';
import { backOut, popIn } from './tween';

/**
 * نمونه‌های واقعی از ‎gsap.parseEase('back.out(2.2)')‎، پیش از برداشتنِ کتابخانه.
 *
 * این جدول تنها چیزی است که ثابت می‌کند منحنی عوض نشده — و اگر روزی کسی فرمول
 * را «ساده» کند، همین‌جا لو می‌رود.
 */
const GSAP_BACK_OUT_2_2: readonly (readonly [number, number])[] = [
  [0, 0],
  [0.1, 0.4492],
  [0.2, 0.7696],
  [0.3, 0.9804],
  [0.4, 1.1008],
  [0.5, 1.15],
  [0.6, 1.1472],
  [0.7, 1.1116],
  [0.8, 1.0624],
  [0.9, 1.0188],
  [1, 1],
];

describe('backOut', () => {
  it.each(GSAP_BACK_OUT_2_2)('در t=%s همان عددی است که gsap می‌داد', (t, expected) => {
    expect(backOut(t, 2.2)).toBeCloseTo(expected, 9);
  });

  it('از صفر شروع و به یک ختم می‌شود', () => {
    expect(backOut(0, 2.2)).toBe(0);
    expect(backOut(1, 2.2)).toBe(1);
  });

  /** «back» یعنی از هدف رد می‌شود؛ بی این، انیمیشن فقط یک بزرگ‌شدنِ ساده است. */
  it('از یک رد می‌شود و برمی‌گردد', () => {
    expect(Math.max(...GSAP_BACK_OUT_2_2.map(([t]) => backOut(t, 2.2)))).toBeGreaterThan(1);
  });

  it('با overshoot صفر دیگر رد نمی‌شود', () => {
    for (let i = 0; i <= 10; i++) {
      expect(backOut(i / 10, 0)).toBeLessThanOrEqual(1);
    }
  });
});

describe('popIn', () => {
  /** ساعت و فریمِ دستی، تا آزمون به زمانِ واقعی گره نخورد. */
  function harness() {
    const frames: FrameRequestCallback[] = [];
    const scales: number[] = [];
    let clock = 0;

    const target = { setScalar: (v: number) => scales.push(v) };
    const raf = (cb: FrameRequestCallback): number => frames.push(cb);

    return {
      scales,
      tick(ms: number) {
        clock += ms;
        const pending = frames.splice(0, frames.length);
        for (const f of pending) f(clock);
      },
      pending: () => frames.length,
      start: (opts?: { duration?: number; overshoot?: number; onEnd?: () => void }) =>
        popIn(target, opts, raf, () => clock),
    };
  }

  it('از تقریباً هیچ شروع می‌کند', () => {
    const h = harness();
    h.start();

    expect(h.scales[0]).toBe(0.01);
  });

  it('دقیقاً روی یک تمام می‌شود و دیگر فریمی نمی‌خواهد', () => {
    const h = harness();
    h.start({ duration: 400 });

    h.tick(400);

    expect(h.scales.at(-1)).toBe(1);
    expect(h.pending()).toBe(0);
  });

  it('در میانه‌ی راه از یک رد می‌شود', () => {
    const h = harness();
    h.start({ duration: 400 });

    h.tick(200);

    expect(h.scales.at(-1)).toBeCloseTo(backOut(0.5, 2.2), 9);
    expect(h.scales.at(-1)!).toBeGreaterThan(1);
  });

  /** صحنه ممکن است پیش از تمام شدنِ تویین برچیده شود. */
  it('بریدنِ نیمه‌کاره مقیاس را روی یک می‌گذارد و متوقف می‌شود', () => {
    const h = harness();
    const stop = h.start({ duration: 400 });

    h.tick(100);
    stop();
    const afterStop = h.scales.length;

    h.tick(100);

    expect(h.scales.at(-1)).toBe(1);
    expect(h.scales.length).toBe(afterStop);
  });

  it('بریدنِ دوباره بی‌اثر است', () => {
    const h = harness();
    const stop = h.start({ duration: 400 });

    stop();
    const after = h.scales.length;
    stop();

    expect(h.scales.length).toBe(after);
  });

  it('پایانِ طبیعی onEnd را صدا می‌زند', () => {
    const h = harness();
    let ended = 0;
    h.start({ duration: 400, onEnd: () => ended++ });

    h.tick(400);

    expect(ended).toBe(1);
  });

  /** صحنه با همین خودش را از فهرست پاک می‌کند؛ دوبار صدا زدنش یعنی نشتی. */
  it('بریدنِ نیمه‌کاره هم onEnd را دقیقاً یک بار صدا می‌زند', () => {
    const h = harness();
    let ended = 0;
    const stop = h.start({ duration: 400, onEnd: () => ended++ });

    h.tick(100);
    stop();
    stop();
    h.tick(400);

    expect(ended).toBe(1);
  });
});
