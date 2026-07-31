import type { LegalMoves, RoadAt } from './connection';

/**
 * منطقِ کوچکِ نمایشِ کارت‌های توسعه.
 *
 * اینجا **هیچ قاعده‌ی بازی** نوشته نمی‌شود: چه کارتی قابل بازی است و کدام یال
 * قانونی است، هر دو از سرور می‌آیند. این فایل فقط همان داده‌ها را برای رابط
 * می‌چیند — و جدا از کامپوننت است تا بشود تستش کرد، چون همین دو تکه قبلاً
 * جای خطای بی‌سروصدا بودند.
 */

/** کلید فشرده‌ی یک یال؛ همان قالبی که سرور هم کلیدهایش را با آن می‌سازد. */
export function edgeKey(edge: { q: number; r: number; side: number }): string {
  return `${edge.q},${edge.r},${edge.side}`;
}

/**
 * یال‌هایی که کارت جاده‌سازی در این لحظه می‌پذیرد.
 *
 * برای انتخاب اول همان فهرست سرور است. برای انتخاب دوم، جاهایی که *به لطف*
 * جاده‌ی اول باز شده‌اند هم اضافه می‌شوند — جاده‌ی دوم روی وضعیتِ بعد از اولی
 * سنجیده می‌شود و بی این کار نمی‌شد با این کارت زنجیره ساخت.
 */
export function freeRoadChoices(legal: LegalMoves, picks: readonly string[]): RoadAt[] {
  const [first] = picks;
  const pool = first ? [...legal.freeRoads, ...(legal.followUpRoads[first] ?? [])] : legal.freeRoads;

  const seen = new Set(picks);

  return pool.filter((edge) => {
    const key = edgeKey(edge);
    if (seen.has(key)) return false;

    seen.add(key);
    return true;
  });
}

/** یک دسته کارت توسعه در دست. */
export interface DevelopmentPile {
  kind: string;
  /** همه‌ی کارت‌های این نوع در دست. */
  count: number;
  /** چند تایشان همین نوبت خریده شده‌اند و هنوز قفل‌اند. */
  fresh: number;
}

/**
 * دو دسته‌ی سرور را در یک دسته‌ی دستی جمع می‌کند.
 *
 * **جمع، نه جایگزینی.** سرور کارت‌های آماده و کارت‌های همین نوبت را جدا
 * می‌فرستد؛ اگر روی هم ریخته شوند، کسی که یک شوالیه‌ی آماده و دو شوالیه‌ی تازه
 * دارد عددِ غلط می‌بیند.
 */
export function developmentPiles(
  owned: Record<string, number> | null | undefined,
  fresh: Record<string, number> | null | undefined,
): DevelopmentPile[] {
  const piles = new Map<string, DevelopmentPile>();

  for (const [kind, count] of Object.entries(owned ?? {})) {
    if (count > 0) piles.set(kind, { kind, count, fresh: 0 });
  }

  for (const [kind, count] of Object.entries(fresh ?? {})) {
    if (count <= 0) continue;

    const pile = piles.get(kind);
    if (pile) {
      pile.count += count;
      pile.fresh += count;
    } else {
      piles.set(kind, { kind, count, fresh: count });
    }
  }

  return [...piles.values()];
}
