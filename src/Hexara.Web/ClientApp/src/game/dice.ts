/**
 * منطقِ نمایشِ تاس.
 *
 * **هیچ عددی اینجا تعیین نمی‌شود.** نتیجه‌ی تاس را سرور می‌اندازد و در نما
 * می‌فرستد؛ این فایل فقط وجه‌هایی را می‌سازد که *حین چرخیدن* دیده می‌شوند و
 * می‌گوید خال‌های هر وجه کجا بنشینند. وقتی چرخش تمام شد، رابط به همان عددِ سرور
 * برمی‌گردد و آنچه اینجا ساخته شده دور ریخته می‌شود.
 */

/**
 * خانه‌های روشنِ هر وجه روی شبکه‌ی ۳×۳، سطر به سطر (۰ تا ۸).
 *
 *   0 1 2
 *   3 4 5
 *   6 7 8
 */
const PIPS: Record<number, readonly number[]> = {
  1: [4],
  2: [0, 8],
  3: [0, 4, 8],
  4: [0, 2, 6, 8],
  5: [0, 2, 4, 6, 8],
  6: [0, 2, 3, 5, 6, 8],
};

export const FACES = [1, 2, 3, 4, 5, 6] as const;

/** خانه‌های روشنِ این وجه؛ وجهِ نامعتبر یعنی تاسِ خالی. */
export function pipsOf(face: number | null): readonly number[] {
  return face === null ? [] : (PIPS[face] ?? []);
}

/** برای هر خانه‌ی شبکه: خال دارد یا نه. */
export function pipGrid(face: number | null): boolean[] {
  const on = new Set(pipsOf(face));
  return Array.from({ length: 9 }, (_, cell) => on.has(cell));
}

/**
 * وجهِ بعدی در چرخش.
 *
 * عمداً هرگز همان وجهِ فعلی را برنمی‌گرداند: با انتخابِ کاملاً تصادفی، از هر شش
 * بار یک بار تاس یک تپشِ کامل بی‌حرکت می‌ماند و چرخش تکه‌تکه به نظر می‌رسد.
 */
export function nextFace(current: number | null, random: () => number): number {
  const others = FACES.filter((face) => face !== current);
  const index = Math.min(others.length - 1, Math.floor(random() * others.length));

  return others[index]!;
}

/** مجموعِ دو تاس؛ تهی یعنی هنوز چیزی انداخته نشده. */
export function total(die1: number | null, die2: number | null): number | null {
  return die1 === null || die2 === null ? null : die1 + die2;
}
