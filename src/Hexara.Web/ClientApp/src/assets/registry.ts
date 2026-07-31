import artAvatarPlaceholder from './svg/avatar-placeholder.svg';
import artDevBack from './svg/dev-back.svg';
import artDevKnight from './svg/dev-knight.svg';
import artDevMonopoly from './svg/dev-monopoly.svg';
import artDevRoadbuilding from './svg/dev-roadbuilding.svg';
import artDevVictorypoint from './svg/dev-victorypoint.svg';
import artDevYearofplenty from './svg/dev-yearofplenty.svg';
import artIconBank from './svg/icon-bank.svg';
import artIconCards from './svg/icon-cards.svg';
import artIconDice from './svg/icon-dice.svg';
import artIconTrade from './svg/icon-trade.svg';
import artIconVictorypoint from './svg/icon-victorypoint.svg';
import artPieceCity from './svg/piece-city.svg';
import artPieceRoad from './svg/piece-road.svg';
import artPieceRobber from './svg/piece-robber.svg';
import artPieceSettlement from './svg/piece-settlement.svg';
import artPortBrick from './svg/port-brick.svg';
import artPortGeneric from './svg/port-generic.svg';
import artPortGrain from './svg/port-grain.svg';
import artPortLumber from './svg/port-lumber.svg';
import artPortOre from './svg/port-ore.svg';
import artPortWool from './svg/port-wool.svg';
import artResourceBrick from './generated/resources/brick.jpg';
import artResourceGrain from './generated/resources/grain.jpg';
import artResourceLumber from './generated/resources/lumber.jpg';
import artResourceOre from './generated/resources/ore.jpg';
import artResourceWool from './generated/resources/wool.jpg';
import artTerrainDesert01 from './generated/terrain/desert-01.webp';
import artTerrainDesert02 from './generated/terrain/desert-02.webp';
import artTerrainDesert03 from './generated/terrain/desert-03.webp';
import artTerrainFields01 from './generated/terrain/fields-01.webp';
import artTerrainFields02 from './generated/terrain/fields-02.webp';
import artTerrainFields03 from './generated/terrain/fields-03.webp';
import artTerrainForest01 from './generated/terrain/forest-01.webp';
import artTerrainForest02 from './generated/terrain/forest-02.webp';
import artTerrainForest03 from './generated/terrain/forest-03.webp';
import artTerrainHills01 from './generated/terrain/hills-01.webp';
import artTerrainHills02 from './generated/terrain/hills-02.webp';
import artTerrainHills03 from './generated/terrain/hills-03.webp';
import artTerrainMountains01 from './generated/terrain/mountains-01.webp';
import artTerrainMountains02 from './generated/terrain/mountains-02.webp';
import artTerrainMountains03 from './generated/terrain/mountains-03.webp';
import artTerrainPasture01 from './generated/terrain/pasture-01.webp';
import artTerrainPasture02 from './generated/terrain/pasture-02.webp';
import artTerrainPasture03 from './generated/terrain/pasture-03.webp';

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
 * بافت‌های مستقل زمین.
 *
 * این جدول عمداً جدا از ASSETS رابط است: بافت‌ها توسط Three.js مصرف می‌شوند و متن
 * جایگزین یا قاب DOM ندارند. انتخاب variation فقط از مختصات ثابت خانه می‌آید تا
 * refresh ظاهر بورد را عوض نکند و هیچ اثری روی قوانین یا دادهٔ سرور نداشته باشد.
 */
export const TERRAIN_ART = {
  Desert: [artTerrainDesert01, artTerrainDesert02, artTerrainDesert03],
  Fields: [artTerrainFields01, artTerrainFields02, artTerrainFields03],
  Forest: [artTerrainForest01, artTerrainForest02, artTerrainForest03],
  Hills: [artTerrainHills01, artTerrainHills02, artTerrainHills03],
  Mountains: [artTerrainMountains01, artTerrainMountains02, artTerrainMountains03],
  Pasture: [artTerrainPasture01, artTerrainPasture02, artTerrainPasture03],
} as const satisfies Record<string, readonly string[]>;

export type TerrainArtName = keyof typeof TERRAIN_ART;

export function terrainArt(terrain: string, q: number, r: number): string | null {
  const variants = TERRAIN_ART[terrain as TerrainArtName];
  if (!variants) return null;

  // دو عدد اول بزرگ، مختصات axial را به یک seed دیداری پایدار تبدیل می‌کنند.
  const hash = (Math.imul(q, 73_856_093) ^ Math.imul(r, 19_349_663) ^ 0x5f3759df) >>> 0;
  return variants[hash % variants.length] ?? variants[0] ?? null;
}

/**
 * کلیدها با فضای‌نام نوشته می‌شوند تا از روی نام معلوم باشد چه چیزی است، و
 * نامِ عضوهای ‎enum‎ سرور عیناً در کلید می‌آید (‎resource.Lumber‎) تا بشود از
 * روی داده‌ی سرور کلید ساخت.
 */
export const ASSETS = {
  'resource.Lumber': { src: artResourceLumber, labelKey: 'game.resource.Lumber', shape: 'card', tone: 'var(--hx-res-lumber)' },
  'resource.Brick': { src: artResourceBrick, labelKey: 'game.resource.Brick', shape: 'card', tone: 'var(--hx-res-brick)' },
  'resource.Wool': { src: artResourceWool, labelKey: 'game.resource.Wool', shape: 'card', tone: 'var(--hx-res-wool)' },
  'resource.Grain': { src: artResourceGrain, labelKey: 'game.resource.Grain', shape: 'card', tone: 'var(--hx-res-grain)' },
  'resource.Ore': { src: artResourceOre, labelKey: 'game.resource.Ore', shape: 'card', tone: 'var(--hx-res-ore)' },

  'dev.Knight': { src: artDevKnight, labelKey: 'game.dev.Knight', shape: 'card', tone: 'var(--hx-res-ore)' },
  'dev.VictoryPoint': { src: artDevVictorypoint, labelKey: 'game.dev.VictoryPoint', shape: 'card', tone: 'var(--hx-accent)' },
  'dev.RoadBuilding': { src: artDevRoadbuilding, labelKey: 'game.dev.RoadBuilding', shape: 'card', tone: 'var(--hx-res-lumber)' },
  'dev.YearOfPlenty': { src: artDevYearofplenty, labelKey: 'game.dev.YearOfPlenty', shape: 'card', tone: 'var(--hx-res-grain)' },
  'dev.Monopoly': { src: artDevMonopoly, labelKey: 'game.dev.Monopoly', shape: 'card', tone: 'var(--hx-res-brick)' },
  'dev.Back': { src: artDevBack, labelKey: 'game.dev.Back', shape: 'card', tone: 'var(--hx-surface-2)' },

  'piece.Settlement': { src: artPieceSettlement, labelKey: 'game.buildSettlement', shape: 'square' },
  'piece.City': { src: artPieceCity, labelKey: 'game.buildCity', shape: 'square' },
  'piece.Road': { src: artPieceRoad, labelKey: 'game.buildRoad', shape: 'square' },
  'piece.Robber': { src: artPieceRobber, labelKey: 'game.robber', shape: 'square' },

  'icon.Dice': { src: artIconDice, labelKey: 'game.roll', shape: 'square' },
  'icon.Trade': { src: artIconTrade, labelKey: 'game.trade', shape: 'square' },
  'icon.Bank': { src: artIconBank, labelKey: 'game.tradeWithBank', shape: 'square' },
  'icon.Cards': { src: artIconCards, labelKey: 'game.yourHand', shape: 'square' },
  'icon.VictoryPoint': { src: artIconVictorypoint, labelKey: 'game.victoryPoints', shape: 'square' },

  'port.Generic': { src: artPortGeneric, labelKey: 'board.portGeneric', shape: 'square' },
  'port.Lumber': { src: artPortLumber, labelKey: 'game.resource.Lumber', shape: 'square', tone: 'var(--hx-res-lumber)' },
  'port.Brick': { src: artPortBrick, labelKey: 'game.resource.Brick', shape: 'square', tone: 'var(--hx-res-brick)' },
  'port.Wool': { src: artPortWool, labelKey: 'game.resource.Wool', shape: 'square', tone: 'var(--hx-res-wool)' },
  'port.Grain': { src: artPortGrain, labelKey: 'game.resource.Grain', shape: 'square', tone: 'var(--hx-res-grain)' },
  'port.Ore': { src: artPortOre, labelKey: 'game.resource.Ore', shape: 'square', tone: 'var(--hx-res-ore)' },

  'avatar.Placeholder': { src: artAvatarPlaceholder, labelKey: 'game.players', shape: 'square' },
} as const satisfies Record<string, AssetSpec>;

export type AssetName = keyof typeof ASSETS;

/**
 * نمای تایپ‌دار روی جدول بالا.
 *
 * ‎as const‎ لازم است تا ‎AssetName‎ از روی کلیدها ساخته شود، ولی همان تایپِ هر
 * ورودی را به همان چیزهایی محدود می‌کند که در خودش نوشته شده. این نما اجازه
 * می‌دهد کد با ‎AssetSpec‎ کار کند، حتی برای ورودی‌ای که روزی ‎src‎ نداشته باشد.
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

/**
 * دارایی‌هایی که هنوز فایل ندارند و جانشین نشان می‌دهند.
 *
 * الان خالی است، ولی می‌ماند: هر کلیدِ تازه‌ای که اضافه شود پیش از کشیده شدنِ
 * تصویرش از همین‌جا پیدا می‌شود.
 */
export function missingArtwork(): AssetName[] {
  return (Object.keys(SPECS) as AssetName[]).filter((name) => !SPECS[name].src);
}
