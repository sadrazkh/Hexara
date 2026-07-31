import type { GameEvent, Hex } from './connection';

/**
 * خواندنِ «این دور چه چیزی به من رسید» از رویدادهای یک حرکت.
 *
 * جدا از کامپوننت است تا آزمون داشته باشد: رویدادها شکلِ بازِ
 * `Record<string, unknown>` دارند و یک اشتباهِ کوچک در نامِ فیلد بی‌صدا به
 * «هیچ‌چیز نشان داده نشد» ختم می‌شود، نه به خطا.
 */

/** یک سهم، همان‌طور که سرور می‌فرستد. */
interface Grant {
  playerIndex: number;
  resource: string;
  amount: number;
}

/** خانه‌ای که در سهمِ کسی نقش داشت. */
interface Source {
  playerIndex: number;
  hex: Hex;
  resource: string;
}

/** آنچه یک دورِ تولید برای یک صندلی داشت. */
export interface Harvest {
  /** نام منبع ⇐ تعداد؛ فقط چیزهایی که واقعاً رسیده. */
  cards: Record<string, number>;
  /** خانه‌هایی که این کارت‌ها را دادند — برای هایلایت روی برد. */
  hexes: Hex[];
  total: number;
}

const NOTHING: Harvest = { cards: {}, hexes: [], total: 0 };

/**
 * برداشتِ این صندلی از رویدادهای یک حرکت.
 *
 * چند رویدادِ تولید در یک حرکت ممکن است (چیدمان اولیه)، پس همه با هم جمع
 * می‌شوند. صندلیِ تهی — یعنی تماشاچی — هیچ برداشتی ندارد.
 */
export function harvestOf(events: readonly GameEvent[], seat: number | null): Harvest {
  if (seat === null) return NOTHING;

  const cards: Record<string, number> = {};
  const hexes: Hex[] = [];
  const seen = new Set<string>();

  for (const event of events) {
    if (event.$kind !== 'ResourcesProduced') continue;

    for (const grant of (event.grants as Grant[] | undefined) ?? []) {
      if (grant.playerIndex !== seat || grant.amount <= 0) continue;

      cards[grant.resource] = (cards[grant.resource] ?? 0) + grant.amount;
    }

    // رویدادهای قدیمیِ ذخیره‌شده ‎sources‎ ندارند؛ آن‌وقت فقط هایلایت نیست.
    for (const source of (event.sources as Source[] | undefined) ?? []) {
      if (source.playerIndex !== seat) continue;

      const key = `${source.hex.q},${source.hex.r}`;
      if (seen.has(key)) continue;

      seen.add(key);
      hexes.push(source.hex);
    }
  }

  const total = Object.values(cards).reduce((sum, n) => sum + n, 0);

  return total === 0 && hexes.length === 0 ? NOTHING : { cards, hexes, total };
}
