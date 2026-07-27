/**
 * هندسه‌ی مشترک شبکه‌ی شش‌ضلعی.
 *
 * از مختصات محوری (axial) با چیدمان «نوک‌تیز به بالا» (pointy-top) استفاده می‌کنیم:
 * ردیف‌ها در راستای X پشت سر هم می‌آیند و رأس هگزها به سمت ‎±Z‎ است — دقیقاً همان
 * جهتی که ‎CylinderGeometry‎ با ‎radialSegments = 6‎ تولید می‌کند.
 *
 * این ماژول در فاز ۳ مبنای ساخت کل برد است، بنابراین عمداً بدون وابستگی به
 * three نگه داشته شده تا در تست و منطق UI هم قابل استفاده باشد.
 */

export interface Axial {
  q: number;
  r: number;
}

export const SQRT3 = Math.sqrt(3);

/** تبدیل مختصات محوری به مختصات جهانی روی صفحه‌ی XZ. */
export function axialToWorld(q: number, r: number, size: number): { x: number; z: number } {
  return {
    x: size * SQRT3 * (q + r / 2),
    z: size * 1.5 * r,
  };
}

/** فاصله‌ی محوری بین دو هگز (تعداد گام تا رسیدن). */
export function axialDistance(a: Axial, b: Axial): number {
  const dq = a.q - b.q;
  const dr = a.r - b.r;
  return (Math.abs(dq) + Math.abs(dq + dr) + Math.abs(dr)) / 2;
}

/** شش جهت همسایگی، به ترتیب ساعتگرد از راست. */
export const AXIAL_DIRECTIONS: readonly Axial[] = [
  { q: 1, r: 0 },
  { q: 1, r: -1 },
  { q: 0, r: -1 },
  { q: -1, r: 0 },
  { q: -1, r: 1 },
  { q: 0, r: 1 },
];

export function neighbor(hex: Axial, direction: number): Axial {
  const d = AXIAL_DIRECTIONS[((direction % 6) + 6) % 6];
  return { q: hex.q + d.q, r: hex.r + d.r };
}

/** تمام هگزهای یک صفحه‌ی شش‌ضلعی با شعاع داده‌شده (شعاع ۲ ⇒ ۱۹ هگز). */
export function hexDisc(radius: number): Axial[] {
  const cells: Axial[] = [];
  for (let q = -radius; q <= radius; q++) {
    const from = Math.max(-radius, -q - radius);
    const to = Math.min(radius, -q + radius);
    for (let r = from; r <= to; r++) {
      cells.push({ q, r });
    }
  }
  return cells;
}

export function axialKey(hex: Axial): string {
  return `${hex.q},${hex.r}`;
}
