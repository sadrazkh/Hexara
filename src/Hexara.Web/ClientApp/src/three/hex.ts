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

/* ── گوشه‌ها و ضلع‌ها ───────────────────────────────────────────────────
   این بخش آینه‌ی دقیق ‎VertexId.cs‎ و ‎EdgeId.cs‎ در سرور است. هر گوشه سه نمایش
   هم‌ارز دارد و هر ضلع دو تا؛ هر دو طرف باید به یک نمایش «کانونی» برسند وگرنه
   کلیک کاربر روی گوشه‌ای می‌نشیند که سرور آن را جای دیگری می‌شناسد.

   قاعده‌ها: ‎(H, i) ≡ (H+d_i, i+2) ≡ (H+d_{i+1}, i+4)‎ برای گوشه،
   و ‎(H, i) ≡ (H+d_i, i+3)‎ برای ضلع. کانونی = کوچک‌ترین ‎(q, r, index)‎. */

export interface VertexId extends Axial {
  corner: number;
}

export interface EdgeId extends Axial {
  side: number;
}

function normalizeDirection(direction: number): number {
  return ((direction % 6) + 6) % 6;
}

function smaller(a: [number, number, number], b: [number, number, number]): [number, number, number] {
  if (a[0] !== b[0]) return a[0] < b[0] ? a : b;
  if (a[1] !== b[1]) return a[1] < b[1] ? a : b;
  return a[2] <= b[2] ? a : b;
}

export function vertexId(hex: Axial, corner: number): VertexId {
  const c = normalizeDirection(corner);
  const first = neighbor(hex, c);
  const second = neighbor(hex, c + 1);

  const best = smaller(
    smaller([hex.q, hex.r, c], [first.q, first.r, normalizeDirection(c + 2)]),
    [second.q, second.r, normalizeDirection(c + 4)],
  );

  return { q: best[0], r: best[1], corner: best[2] };
}

export function edgeId(hex: Axial, side: number): EdgeId {
  const s = normalizeDirection(side);
  const other = neighbor(hex, s);
  const otherSide = normalizeDirection(s + 3);

  const best = smaller([hex.q, hex.r, s], [other.q, other.r, otherSide]);
  return { q: best[0], r: best[1], side: best[2] };
}

export function vertexKey(vertex: VertexId): string {
  return `${vertex.q},${vertex.r},${vertex.corner}`;
}

export function edgeKey(edge: EdgeId): string {
  return `${edge.q},${edge.r},${edge.side}`;
}

/** سه هگزی که این گوشه را در بر گرفته‌اند. */
export function vertexHexes(vertex: VertexId): Axial[] {
  return [vertex, neighbor(vertex, vertex.corner), neighbor(vertex, vertex.corner + 1)];
}

/**
 * جای گوشه در فضا.
 *
 * گوشه‌ی مشترک سه هگز دقیقاً مرکز ثقل سه مرکز آن‌هاست — هم کوتاه‌ترین راه رسیدن
 * به مختصات است و هم تضمین می‌کند که با تعریف کانونی هم‌خوان بماند.
 */
export function vertexToWorld(vertex: VertexId, size: number): { x: number; z: number } {
  let x = 0;
  let z = 0;

  for (const hex of vertexHexes(vertex)) {
    const world = axialToWorld(hex.q, hex.r, size);
    x += world.x;
    z += world.z;
  }

  return { x: x / 3, z: z / 3 };
}

/** دو سرِ یک ضلع، به همان ترتیبی که سرور می‌شناسد. */
export function edgeEndpoints(edge: EdgeId): [VertexId, VertexId] {
  return [vertexId(edge, edge.side), vertexId(edge, edge.side - 1)];
}

/** میانه‌ی ضلع و زاویه‌ی آن روی صفحه‌ی XZ — برای گذاشتن و چرخاندن جاده. */
export function edgeToWorld(
  edge: EdgeId,
  size: number,
): { x: number; z: number; angle: number; length: number } {
  const [from, to] = edgeEndpoints(edge);
  const a = vertexToWorld(from, size);
  const b = vertexToWorld(to, size);

  return {
    x: (a.x + b.x) / 2,
    z: (a.z + b.z) / 2,
    angle: Math.atan2(b.z - a.z, b.x - a.x),
    length: Math.hypot(b.x - a.x, b.z - a.z),
  };
}
