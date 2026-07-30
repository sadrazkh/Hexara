/**
 * تنها جایی که می‌داند کدام تصویر در کدام فایل است.
 *
 * چرا وجود دارد: قرار است تصویرهای حرفه‌ای جای تصویرهای موقت بنشینند بی‌آنکه
 * منطق یا چیدمان دست بخورد. اگر مسیر فایل‌ها در کامپوننت‌ها پخش باشد، آن روز
 * باید ده جا را عوض کرد و هر کدام یک اندازه‌ی متفاوت می‌گیرد. این‌جا هر دارایی
 * یک *کلید* دارد؛ کامپوننت‌ها فقط کلید را می‌شناسند.
 *
 * سه قاعده که این ماژول نگه می‌دارد:
 *
 * ۱. **قوطیِ ثابت.** هر دارایی نسبتِ خودش را اعلام می‌کند و ‎Asset.vue‎ همان را
 *    رزرو می‌کند. پس عوض شدن تصویر هرگز چیدمان را نمی‌لرزاند.
 * ۲. **همیشه چیزی دیده می‌شود.** دارایی‌ای که هنوز فایل ندارد با برچسبِ
 *    ترجمه‌شده و رنگِ خودش نمایش داده می‌شود، نه یک مربعِ خالی یا آیکنِ شکسته.
 * ۳. **متن جدا از تصویر.** برچسب یک کلیدِ ترجمه است، نه یک رشته‌ی انگلیسی.
 */

/** نسبت‌های استاندارد؛ هر چیزی که خارج از این‌ها باشد باید دلیل داشته باشد. */
export type AssetShape = 'card' | 'square' | 'hex' | 'wide';

export interface AssetSpec {
  /**
   * نشانیِ فایل. تهی یعنی هنوز کشیده نشده و نسخه‌ی جانشین نمایش داده می‌شود —
   * یعنی «هنوز نیست» در رابط دیده می‌شود، نه این‌که بی‌صدا غیب شود.
   */
  src?: string;

  /** کلید ترجمه‌ی نام؛ هم برای ‎alt‎ و هم برای متنِ جانشین. */
  labelKey: string;

  shape: AssetShape;

  /** رنگِ جانشین. توکنِ CSS است تا با تم عوض شود. */
  tone?: string;
}

/**
 * کلیدها با فضای‌نام نوشته می‌شوند تا از روی نام معلوم باشد چه چیزی است، و
 * نامِ عضوهای ‎enum‎ سرور عیناً در کلید می‌آید (‎resource.Lumber‎) تا بشود از
 * روی داده‌ی سرور کلید ساخت.
 */
export const ASSETS = {
  'resource.Lumber': { labelKey: 'game.resource.Lumber', shape: 'card', tone: 'var(--hx-res-lumber)' },
  'resource.Brick': { labelKey: 'game.resource.Brick', shape: 'card', tone: 'var(--hx-res-brick)' },
  'resource.Wool': { labelKey: 'game.resource.Wool', shape: 'card', tone: 'var(--hx-res-wool)' },
  'resource.Grain': { labelKey: 'game.resource.Grain', shape: 'card', tone: 'var(--hx-res-grain)' },
  'resource.Ore': { labelKey: 'game.resource.Ore', shape: 'card', tone: 'var(--hx-res-ore)' },

  'dev.Knight': { labelKey: 'game.dev.Knight', shape: 'card', tone: 'var(--hx-res-ore)' },
  'dev.VictoryPoint': { labelKey: 'game.dev.VictoryPoint', shape: 'card', tone: 'var(--hx-accent)' },
  'dev.RoadBuilding': { labelKey: 'game.dev.RoadBuilding', shape: 'card', tone: 'var(--hx-res-lumber)' },
  'dev.YearOfPlenty': { labelKey: 'game.dev.YearOfPlenty', shape: 'card', tone: 'var(--hx-res-grain)' },
  'dev.Monopoly': { labelKey: 'game.dev.Monopoly', shape: 'card', tone: 'var(--hx-res-brick)' },
  'dev.Back': { labelKey: 'game.dev.Back', shape: 'card', tone: 'var(--hx-surface-2)' },

  'piece.Settlement': { labelKey: 'game.buildSettlement', shape: 'square' },
  'piece.City': { labelKey: 'game.buildCity', shape: 'square' },
  'piece.Road': { labelKey: 'game.buildRoad', shape: 'square' },
  'piece.Robber': { labelKey: 'game.robber', shape: 'square' },

  'icon.Dice': { labelKey: 'game.roll', shape: 'square' },
  'icon.Trade': { labelKey: 'game.trade', shape: 'square' },
  'icon.Bank': { labelKey: 'game.tradeWithBank', shape: 'square' },
  'icon.Cards': { labelKey: 'game.yourHand', shape: 'square' },
  'icon.VictoryPoint': { labelKey: 'game.victoryPoints', shape: 'square' },

  'port.Generic': { labelKey: 'board.portGeneric', shape: 'square' },
  'port.Lumber': { labelKey: 'game.resource.Lumber', shape: 'square', tone: 'var(--hx-res-lumber)' },
  'port.Brick': { labelKey: 'game.resource.Brick', shape: 'square', tone: 'var(--hx-res-brick)' },
  'port.Wool': { labelKey: 'game.resource.Wool', shape: 'square', tone: 'var(--hx-res-wool)' },
  'port.Grain': { labelKey: 'game.resource.Grain', shape: 'square', tone: 'var(--hx-res-grain)' },
  'port.Ore': { labelKey: 'game.resource.Ore', shape: 'square', tone: 'var(--hx-res-ore)' },

  'avatar.Placeholder': { labelKey: 'game.players', shape: 'square' },
} as const satisfies Record<string, AssetSpec>;

export type AssetName = keyof typeof ASSETS;

/**
 * نمای تایپ‌دار روی جدول بالا.
 *
 * ‎as const‎ لازم است تا ‎AssetName‎ از روی کلیدها ساخته شود، ولی همان باعث
 * می‌شود تایپِ هر ورودی فقط همان چیزهایی را داشته باشد که نوشته شده‌اند — و
 * ‎src‎ که هنوز هیچ‌کدام ندارند اصلاً وجود نداشته باشد. این نما هر دو را می‌دهد.
 */
const SPECS: Record<AssetName, AssetSpec> = ASSETS;

export function assetSpec(name: AssetName): AssetSpec {
  return SPECS[name];
}

/**
 * کلیدسازی از نام عضوِ سرور. اگر عضوی بیاید که دارایی ندارد، ‎null‎ برمی‌گردد و
 * صدا زننده تصمیم می‌گیرد — بهتر از ساختنِ کلیدی که وجود ندارد.
 */
export function assetFor(namespace: string, member: string): AssetName | null {
  const key = `${namespace}.${member}`;
  return key in ASSETS ? (key as AssetName) : null;
}

/** آیا همه‌ی دارایی‌ها فایل دارند؟ تا وقتی false است، جانشین‌ها دیده می‌شوند. */
export function missingArtwork(): AssetName[] {
  return (Object.keys(SPECS) as AssetName[]).filter((name) => !SPECS[name].src);
}
