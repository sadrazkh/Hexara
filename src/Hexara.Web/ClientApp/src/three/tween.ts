/**
 * کمترین چیزی که برای انیمیشنِ «قطعه از هیچ بزرگ می‌شود» لازم است.
 *
 * پیش از این یک کتابخانه‌ی کاملِ انیمیشن برای همین **یک خط** بارگذاری می‌شد —
 * ۶۸ کیلوبایت خام (۳۲ فشرده) روی صفحه‌ی بازی، به‌ازای یک تویینِ مقیاس. منحنی
 * دقیقاً همان است (تا حدِ دقتِ اعشاری سنجیده شده)، فقط بی آن وزن.
 */

/**
 * شتاب‌گیرِ ‎back.out‎: کمی از هدف رد می‌شود و برمی‌گردد.
 *
 * ‎overshoot‎ همان عددی است که در ‎back.out(n)‎ نوشته می‌شد.
 */
export function backOut(t: number, overshoot: number): number {
  const u = t - 1;
  return u * u * ((overshoot + 1) * u + overshoot) + 1;
}

export interface PopInOptions {
  duration?: number;
  overshoot?: number;
  /**
   * وقتی تویین تمام شد — چه خودش، چه با بریده‌شدن.
   *
   * صحنه از همین برای دور انداختنِ تویینِ تمام‌شده استفاده می‌کند؛ بی آن،
   * فهرستِ تویین‌ها در یک بازی طولانی همین‌طور بلند می‌شود.
   */
  onEnd?: () => void;
}

/** چیزی که می‌شود مقیاسش را عوض کرد — همان شکلی که ‎Vector3‎ دارد. */
export interface Scalable {
  setScalar(value: number): unknown;
}

/**
 * قطعه را از تقریباً هیچ تا اندازه‌ی کامل بزرگ می‌کند.
 *
 * ساعت از ‎requestAnimationFrame‎ می‌آید نه از شمارنده‌ی فریم، پس روی نمایشگرِ
 * ۱۲۰ هرتز هم همان‌قدر طول می‌کشد که روی ۶۰.
 *
 * تابعِ برگشتی انیمیشن را نیمه‌کاره می‌بُرد و مقیاس را روی ۱ می‌گذارد؛ لازم است
 * چون صحنه ممکن است پیش از تمام شدنِ تویین برچیده شود.
 */
export function popIn(
  target: Scalable,
  { duration = 420, overshoot = 2.2, onEnd }: PopInOptions = {},
  raf: (cb: FrameRequestCallback) => number = requestAnimationFrame,
  now: () => number = () => performance.now(),
): () => void {
  const start = now();
  let alive = true;

  target.setScalar(0.01);

  const finish = (): void => {
    alive = false;
    target.setScalar(1);
    onEnd?.();
  };

  const step = (): void => {
    if (!alive) return;

    const elapsed = now() - start;
    if (elapsed >= duration) {
      finish();
      return;
    }

    target.setScalar(backOut(elapsed / duration, overshoot));
    raf(step);
  };

  raf(step);

  return () => {
    if (alive) finish();
  };
}
