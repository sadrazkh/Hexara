import * as THREE from 'three';
import { axialToWorld, type Axial } from './hex';

/**
 * چیزهایی که روی زمین می‌رویند: جنگل، کوه، گندم‌زار، چراگاه، تپه و کویر.
 *
 * چهار قاعده‌ای که شکل این ماژول را تعیین کرده‌اند:
 *
 * ۱. **جایِ هر چیز قطعی است.** جای درخت‌ها از مختصات خودِ خانه درمی‌آید، نه از
 *    ‎Math.random‎. پس یک برد برای همه‌ی بازیکنان و در هر بار رسم، دقیقاً یک شکل
 *    دارد. برد چیزی است که آدم‌ها سرش با هم حرف می‌زنند («جنگلِ کنار کوه»)، و
 *    اگر هر بار جابه‌جا شود آن حرف معنا ندارد.
 *
 * ۲. **زینت جلوی بازی را نمی‌گیرد.** میانه‌ی خانه برای ژتون عدد خالی می‌ماند و
 *    گوشه‌ها و لبه‌ها برای آبادی و جاده. پس همه‌چیز در یک حلقه‌ی میانی می‌نشیند.
 *
 * ۳. **تعداد زیاد، فراخوانِ کم.** هر جزء یک ‎InstancedMesh‎ است، پس دویست درخت
 *    همان‌قدر خرج دارد که دو تا. بی این کار، بردِ بزرگ روی موبایل کند می‌شد.
 *
 * ۴. **هر هندسه روی زمین می‌نشیند و اندازه‌اش واحد است.** همه پیش از استفاده
 *    جابه‌جا می‌شوند تا ‎y ∈ [0, 1]‎ و پهنایشان ۱ باشد. پس ‎size‎ و ‎height‎ در
 *    ادامه مستقیماً «چند واحد جهانی» معنا می‌دهند و هیچ جزئی نیاز به حساب
 *    جداگانه‌ی نیم‌ارتفاع ندارد — جایی که اولین بار همین حساب را غلط زدم و
 *    درخت‌ها غول شدند و سنگ‌ها در هوا ماندند.
 */

/**
 * حلقه‌ای که زینت در آن می‌نشیند — بیرونِ ژتون عدد و درونِ لبه‌ی خانه.
 *
 * این دو عدد به *لبه‌ی* شیء نگاه می‌کنند نه مرکزش، پس هر تابعِ چیدن باید نیمِ
 * پهنای پهن‌ترین جزئش را بدهد. اولین بار مرکزها را محدود کردم و نتیجه‌اش این
 * شد که پشته‌های پهنِ کویر به خانه‌ی بغل سرک کشیدند و قله‌ی میانیِ کوه روی
 * ژتون عدد نشست — هر دو را تست گرفت.
 *
 * شعاع ژتون ‎۰٫۳۴‎ است و شعاع درونیِ شش‌ضلعی ‎۰٫۸۶۶‎؛ هر دو سر کمی حاشیه دارند.
 */
const TOKEN_CLEAR = 0.36;
const SAFE_OUTER = 0.8;

/** روی خانه؛ ضخامت خانه ۰٫۳۴ است و مرکزش روی صفر. */
const GROUND = 0.17;

interface Instance {
  x: number;
  z: number;
  lift: number;
  size: number;
  height: number;
  turn: number;
  tilt: number;
  shade: number;
}

type PartName =
  | 'trunk'
  | 'canopy'
  | 'peak'
  | 'snow'
  | 'mound'
  | 'rock'
  | 'blade'
  | 'sheaf'
  | 'sheafTop'
  | 'fleece'
  | 'brick';

type Bag = Record<PartName, Instance[]>;

const PART_NAMES: PartName[] = [
  'trunk',
  'canopy',
  'peak',
  'snow',
  'mound',
  'rock',
  'blade',
  'sheaf',
  'sheafTop',
  'fleece',
  'brick',
];

function emptyBag(): Bag {
  return {
    trunk: [],
    canopy: [],
    peak: [],
    snow: [],
    mound: [],
    rock: [],
    blade: [],
    sheaf: [],
    sheafTop: [],
    fleece: [],
    brick: [],
  };
}

/**
 * مولد شبه‌تصادفیِ کوچک با دانه‌ی صحیح.
 *
 * همان نقشی را دارد که ‎Rng‎ در دامنه: عددهای یکسان برای دانه‌ی یکسان. عمداً
 * ‎Math.random‎ نیست تا چیدمانِ زینت هم مثل خودِ برد بازتولیدپذیر بماند.
 */
function noise(seed: number): () => number {
  let state = seed >>> 0;

  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** دانه از مختصات خانه؛ دو خانه‌ی متفاوت هرگز یک دانه نمی‌گیرند. */
function seedOf(hex: Axial): number {
  return Math.imul(hex.q + 1000, 73856093) ^ Math.imul(hex.r + 1000, 19349663);
}

/**
 * جای‌های پخش‌شده در حلقه‌ی میانی خانه.
 *
 * زاویه‌ها از خانه‌های مساوی شروع می‌شوند و بعد کمی تکان می‌خورند: پخشِ کاملاً
 * تصادفی توده و حفره می‌سازد و به‌جای جنگل، لکه به نظر می‌رسد.
 *
 * ‎half‎ نیمِ پهنای پهن‌ترین جزئی است که روی این نقطه می‌نشیند. حلقه به همان
 * اندازه تنگ‌تر می‌شود تا لبه‌ی شیء از نوارِ مجاز بیرون نزند. اجزاء یک شیء
 * مرکب (تنه و تاج) عمداً همین یک نقطه را می‌گیرند، پس هرگز از هم جدا نمی‌شوند.
 */
function scatter(rand: () => number, count: number, half = 0): { x: number; z: number }[] {
  const spots: { x: number; z: number }[] = [];
  const step = (Math.PI * 2) / count;

  const low = TOKEN_CLEAR + half;
  const high = Math.max(low, SAFE_OUTER - half);

  for (let i = 0; i < count; i++) {
    const angle = i * step + (rand() - 0.5) * step * 0.7;
    const radius = low + rand() * (high - low);

    spots.push({ x: Math.cos(angle) * radius, z: Math.sin(angle) * radius });
  }

  return spots;
}

function at(rand: () => number, x: number, z: number): Instance {
  return {
    x,
    z,
    lift: 0,
    size: 0.1,
    height: 0.1,
    turn: rand() * Math.PI * 2,
    tilt: 0,
    shade: 1,
  };
}

/**
 * جنگل: سوزنی‌برگ‌های ریز و درشت، دو طبقه تاج روی یک تنه.
 *
 * درخت‌ها عمداً کوچک‌اند (بلندترین حدود ۰٫۴۵ واحد، یعنی نیمِ شعاع خانه) تا
 * هفت‌تایشان کنار هم «جنگل» بخوانند. یک مخروطِ بزرگ فقط یک مخروطِ بزرگ است.
 */
function forest(bag: Bag, rand: () => number): void {
  for (const spot of scatter(rand, 6 + Math.floor(rand() * 3), 0.1)) {
    const tall = 0.3 + rand() * 0.16;
    const wide = 0.15 + rand() * 0.05;
    const lean = (rand() - 0.5) * 0.12;

    bag.trunk.push({
      ...at(rand, spot.x, spot.z),
      size: wide * 0.26,
      height: tall * 0.4,
      tilt: lean,
      shade: 0.5 + rand() * 0.12,
    });

    // تاج پایینی پهن‌تر و تیره‌تر، بالایی باریک‌تر و روشن‌تر — همین دو طبقه
    // کافی است که از دور «درخت» خوانده شود نه «مخروط».
    bag.canopy.push({
      ...at(rand, spot.x, spot.z),
      lift: tall * 0.24,
      size: wide,
      height: tall * 0.6,
      tilt: lean,
      shade: 0.72 + rand() * 0.14,
    });

    bag.canopy.push({
      ...at(rand, spot.x, spot.z),
      lift: tall * 0.56,
      size: wide * 0.68,
      height: tall * 0.52,
      tilt: lean,
      shade: 0.98 + rand() * 0.18,
    });
  }
}

/** کوه: چند قله‌ی تیزِ کم‌وجه با کلاهک روشن، بلندترین نزدیک میانه. */
function mountains(bag: Bag, rand: () => number): void {
  // پهن‌ترین قله ‎۰٫۳۶‎ است، پس نیمش ‎۰٫۱۸‎.
  const peaks = scatter(rand, 4 + Math.floor(rand() * 2), 0.18);

  // یکی از قله‌ها بلندتر است تا رشته‌کوه یک راس داشته باشد. جای مرکزِ خانه
  // نمی‌نشیند، چون آن‌جا مالِ ژتون عدد است.
  const summit = Math.floor(rand() * peaks.length);

  for (const [index, spot] of peaks.entries()) {
    const middle = index === summit;
    const tall = middle ? 0.5 + rand() * 0.16 : 0.28 + rand() * 0.16;
    const wide = middle ? 0.34 : 0.24 + rand() * 0.08;

    bag.peak.push({
      ...at(rand, spot.x, spot.z),
      size: wide,
      height: tall,
      shade: 0.68 + rand() * 0.26,
    });

    bag.snow.push({
      ...at(rand, spot.x, spot.z),
      lift: tall * 0.62,
      size: wide * 0.4,
      height: tall * 0.38,
      shade: 1.45 + rand() * 0.2,
    });
  }
}

/**
 * گندم‌زار: ردیف‌های موازی، نه پخشِ تصادفی.
 *
 * تفاوت مهم است: کشتزار دستِ آدم در آن بوده و نظم دارد. پخشِ تصادفی، گندم‌زار
 * را شبیه علفزار می‌کند و آن‌وقت با چراگاه اشتباه می‌شود.
 */
function fields(bag: Bag, rand: () => number): void {
  const lean = rand() * Math.PI;
  const cos = Math.cos(lean);
  const sin = Math.sin(lean);
  const rows = 5;
  const perRow = 7;

  for (let row = 0; row < rows; row++) {
    const across = -SAFE_OUTER + ((row + 0.5) / rows) * SAFE_OUTER * 2;

    for (let step = 0; step < perRow; step++) {
      const along = -SAFE_OUTER + ((step + 0.5) / perRow) * SAFE_OUTER * 2;

      // درونِ دایره‌ی خانه بمان و از ژتون عدد فاصله بگیر. شعاع ژتون ‎۰٫۳۴‎ است و
      // ساقه‌ها بلندترند، پس نزدیک‌تر از این از میان عدد بیرون می‌زدند.
      const radius = Math.hypot(along, across);
      if (radius > SAFE_OUTER - 0.04 || radius < TOKEN_CLEAR + 0.04) continue;

      bag.blade.push({
        ...at(rand, along * cos - across * sin, along * sin + across * cos),
        size: 0.075,
        height: 0.15 + rand() * 0.06,
        turn: lean,
        shade: 0.84 + rand() * 0.32,
      });
    }
  }

  // دو بافه‌ی درو‌شده، تا معلوم شود محصول همین‌جا برداشت می‌شود.
  for (const spot of scatter(rand, 2, 0.07)) {
    bag.sheaf.push({
      ...at(rand, spot.x, spot.z),
      size: 0.1,
      height: 0.16,
      shade: 0.92,
    });

    bag.sheafTop.push({
      ...at(rand, spot.x, spot.z),
      lift: 0.16,
      size: 0.13,
      height: 0.1,
      shade: 1.18,
    });
  }
}

/** چراگاه: بوته‌های علف و دو گوسفند. */
function pasture(bag: Bag, rand: () => number): void {
  for (const spot of scatter(rand, 7 + Math.floor(rand() * 3), 0.08)) {
    // هر بوته سه پره است تا از بالا هم توپُر دیده شود.
    for (let blade = 0; blade < 3; blade++) {
      const angle = (blade / 3) * Math.PI * 2 + rand();

      bag.blade.push({
        ...at(rand, spot.x + Math.cos(angle) * 0.045, spot.z + Math.sin(angle) * 0.045),
        size: 0.06,
        height: 0.08 + rand() * 0.05,
        tilt: (rand() - 0.5) * 0.5,
        shade: 0.78 + rand() * 0.36,
      });
    }
  }

  for (const spot of scatter(rand, 2, 0.08)) {
    bag.fleece.push({
      ...at(rand, spot.x, spot.z),
      size: 0.13 + rand() * 0.03,
      height: 0.11,
      shade: 1.6,
    });
  }
}

/** تپه: پشته‌های نرم خاک و یک چینه‌ی آجر. */
function hills(bag: Bag, rand: () => number): void {
  for (const spot of scatter(rand, 4 + Math.floor(rand() * 2), 0.18)) {
    bag.mound.push({
      ...at(rand, spot.x, spot.z),
      size: 0.28 + rand() * 0.08,
      height: 0.11 + rand() * 0.07,
      shade: 0.76 + rand() * 0.28,
    });
  }

  // آجرها روی هم، هر ردیف کمی چرخیده — نشانه‌ی کوره‌ی آجرپزی.
  const kiln = scatter(rand, 1, 0.1)[0]!;
  const turn = rand() * Math.PI;

  for (let layer = 0; layer < 3; layer++) {
    bag.brick.push({
      ...at(rand, kiln.x, kiln.z),
      lift: layer * 0.045,
      size: 0.2 - layer * 0.03,
      height: 0.045,
      turn: turn + layer * 0.55,
      shade: 1.12 + layer * 0.14,
    });
  }
}

/** کویر: چند پشته‌ی شنی کم‌ارتفاع و یک‌دو سنگ. کم‌تعداد، چون خلوتی خودش معناست. */
function desert(bag: Bag, rand: () => number): void {
  for (const spot of scatter(rand, 3, 0.18)) {
    bag.mound.push({
      ...at(rand, spot.x, spot.z),
      size: 0.28 + rand() * 0.08,
      height: 0.06 + rand() * 0.04,
      shade: 0.9 + rand() * 0.2,
    });
  }

  for (const spot of scatter(rand, 2, 0.08)) {
    bag.rock.push({
      ...at(rand, spot.x, spot.z),
      size: 0.1 + rand() * 0.06,
      height: 0.09 + rand() * 0.05,
      shade: 0.7 + rand() * 0.22,
    });
  }
}

const BUILDERS: Record<string, (bag: Bag, rand: () => number) => void> = {
  Forest: forest,
  Mountains: mountains,
  Fields: fields,
  Pasture: pasture,
  Hills: hills,
  Desert: desert,
};

interface PartSpec {
  /** هندسه‌ی واحد: پهنا و عمق ۱، و ‎y ∈ [0, 1]‎ تا روی زمین بنشیند. */
  geometry: () => THREE.BufferGeometry;
  /** توکنِ رنگ؛ تهی یعنی رنگِ خودِ زمینِ زیرِ پا. */
  token: string | null;
  fallback: number;
  roughness: number;
  shadow: boolean;
}

/** هندسه را می‌برد بالا تا پایش روی صفر بنشیند. */
function standing(geometry: THREE.BufferGeometry): THREE.BufferGeometry {
  geometry.computeBoundingBox();
  const box = geometry.boundingBox!;

  geometry.translate(0, -box.min.y, 0);

  const height = box.max.y - box.min.y;
  if (height > 0) geometry.scale(1, 1 / height, 1);

  return geometry;
}

const PARTS: Record<PartName, PartSpec> = {
  trunk: {
    geometry: () => standing(new THREE.CylinderGeometry(0.4, 0.5, 1, 6)),
    token: '--hx-scenery-bark',
    fallback: 0x6b4a2c,
    roughness: 0.9,
    shadow: true,
  },
  canopy: {
    geometry: () => standing(new THREE.ConeGeometry(0.5, 1, 8)),
    token: null,
    fallback: 0x4a9159,
    roughness: 0.85,
    shadow: true,
  },
  peak: {
    // پنج‌وجهی، تا صخره زاویه‌دار دیده شود نه صابونی.
    geometry: () => standing(new THREE.ConeGeometry(0.5, 1, 5)),
    token: null,
    fallback: 0x98a2b4,
    roughness: 0.95,
    shadow: true,
  },
  snow: {
    geometry: () => standing(new THREE.ConeGeometry(0.5, 1, 5)),
    token: '--hx-scenery-snow',
    fallback: 0xe8eef6,
    roughness: 0.7,
    shadow: false,
  },
  mound: {
    geometry: () =>
      standing(new THREE.SphereGeometry(0.5, 14, 7, 0, Math.PI * 2, 0, Math.PI / 2)),
    token: null,
    fallback: 0xc0603f,
    roughness: 0.9,
    shadow: true,
  },
  rock: {
    geometry: () => standing(new THREE.IcosahedronGeometry(0.5, 0)),
    token: '--hx-scenery-stone',
    fallback: 0x7d7566,
    roughness: 0.95,
    shadow: true,
  },
  blade: {
    geometry: () => standing(new THREE.ConeGeometry(0.5, 1, 4)),
    token: null,
    fallback: 0xe0b23c,
    roughness: 0.8,
    shadow: false,
  },
  sheaf: {
    geometry: () => standing(new THREE.CylinderGeometry(0.42, 0.5, 1, 7)),
    token: null,
    fallback: 0xe0b23c,
    roughness: 0.85,
    shadow: true,
  },
  sheafTop: {
    geometry: () => standing(new THREE.ConeGeometry(0.5, 1, 7)),
    token: null,
    fallback: 0xe0b23c,
    roughness: 0.85,
    shadow: false,
  },
  fleece: {
    geometry: () => standing(new THREE.SphereGeometry(0.5, 10, 7)),
    token: '--hx-scenery-fleece',
    fallback: 0xf3ece0,
    roughness: 0.95,
    shadow: true,
  },
  brick: {
    geometry: () => standing(new THREE.BoxGeometry(1, 1, 0.62)),
    token: null,
    fallback: 0xc0603f,
    roughness: 0.85,
    shadow: true,
  },
};

export interface Scenery {
  group: THREE.Group;
  disposables: { dispose(): void }[];
}

/**
 * زینتِ کل برد را می‌سازد.
 *
 * رنگ اجزائی که ‎token‎ ندارند از خودِ زمین می‌آید، تا با تعویض تم هم درست
 * بمانند — همان قاعده‌ای که برای زمین و دریا برقرار است.
 */
export function buildScenery(
  tiles: { q: number; r: number; terrain: string }[],
  tileSize: number,
  colorOf: (terrain: string) => THREE.Color,
  tokenOf: (name: string, fallback: number) => THREE.Color,
  shadows: boolean,
): Scenery {
  const group = new THREE.Group();
  const disposables: { dispose(): void }[] = [];

  // اول همه‌ی نمونه‌ها شمرده می‌شوند، چون InstancedMesh تعداد را از پیش می‌خواهد.
  const plots: { bag: Bag; terrain: string; x: number; z: number }[] = [];

  for (const tile of tiles) {
    const build = BUILDERS[tile.terrain];
    if (!build) continue;

    const bag = emptyBag();
    build(bag, noise(seedOf(tile)));

    const { x, z } = axialToWorld(tile.q, tile.r, tileSize);
    plots.push({ bag, terrain: tile.terrain, x, z });
  }

  const matrix = new THREE.Matrix4();
  const quaternion = new THREE.Quaternion();
  const euler = new THREE.Euler();
  const position = new THREE.Vector3();
  const scale = new THREE.Vector3();
  const tint = new THREE.Color();

  for (const name of PART_NAMES) {
    const total = plots.reduce((sum, plot) => sum + plot.bag[name].length, 0);
    if (total === 0) continue;

    const spec = PARTS[name];
    const geometry = spec.geometry();
    const material = new THREE.MeshStandardMaterial({ roughness: spec.roughness, metalness: 0.02 });

    const mesh = new THREE.InstancedMesh(geometry, material, total);
    mesh.castShadow = shadows && spec.shadow;
    mesh.receiveShadow = false;

    let index = 0;
    for (const plot of plots) {
      const ground =
        spec.token === null ? colorOf(plot.terrain) : tokenOf(spec.token, spec.fallback);

      for (const item of plot.bag[name]) {
        position.set(
          plot.x + item.x * tileSize,
          GROUND + item.lift,
          plot.z + item.z * tileSize,
        );

        euler.set(item.tilt, item.turn, 0);
        quaternion.setFromEuler(euler);
        scale.set(item.size, item.height, item.size);

        matrix.compose(position, quaternion, scale);
        mesh.setMatrixAt(index, matrix);

        // روشنی هر نمونه کمی فرق دارد تا توده یکدست و پلاستیکی نشود.
        tint.copy(ground).multiplyScalar(item.shade);
        mesh.setColorAt(index, tint);

        index++;
      }
    }

    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;

    group.add(mesh);
    disposables.push(geometry, material);
  }

  return { group, disposables };
}
