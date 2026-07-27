import * as THREE from 'three';
import {
  axialToWorld,
  edgeId,
  edgeKey,
  edgeToWorld,
  vertexId,
  vertexKey,
  vertexToWorld,
  type Axial,
  type EdgeId,
  type VertexId,
} from './hex';

export const TILE_SIZE = 1;

/**
 * رنگ‌ها از توکن‌های CSS خوانده می‌شوند، نه از عددهای کپی‌شده — وگرنه برد و
 * رابط با هم می‌لغزند. زمین و دریا تم‌دارند و با تعویض تم عوض می‌شوند؛
 * قطعه‌ها (مهره، ژتون) نه، چون شیء فیزیکی‌اند. جدول `tokens.css` را ببین.
 */
const TERRAIN_TOKEN: Record<string, string> = {
  Desert: '--hx-res-desert',
  Forest: '--hx-res-lumber',
  Hills: '--hx-res-brick',
  Pasture: '--hx-res-wool',
  Fields: '--hx-res-grain',
  Mountains: '--hx-res-ore',
};

const RESOURCE_TOKEN: Record<string, string> = {
  Lumber: '--hx-res-lumber',
  Brick: '--hx-res-brick',
  Wool: '--hx-res-wool',
  Grain: '--hx-res-grain',
  Ore: '--hx-res-ore',
};

const SEAT_TOKENS = [
  '--hx-seat-1',
  '--hx-seat-2',
  '--hx-seat-3',
  '--hx-seat-4',
  '--hx-seat-5',
  '--hx-seat-6',
];

/**
 * رنگ یک توکن CSS به‌صورت رنگِ Three.
 *
 * عمداً تنبل خوانده می‌شود و نه در سطح ماژول: اگر موقع import صدا زده شود،
 * ممکن است شیوه‌نامه هنوز اعمال نشده باشد و همه‌چیز سیاه دربیاید. رنگ
 * جایگزین هم برای همان حالت است — بردِ بدرنگ بهتر از بردِ نامرئی است.
 */
function tokenColor(name: string, fallback: number): THREE.Color {
  const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  if (!raw) return new THREE.Color(fallback);

  try {
    return new THREE.Color(raw);
  } catch {
    return new THREE.Color(fallback);
  }
}

export interface Tile extends Axial {
  terrain: string;
  number: number | null;
}

export interface Port extends Axial {
  side: number;
  resource: string | null;
}

export interface BuildingAt extends Axial {
  corner: number;
  playerIndex: number;
  kind: string;
}

export interface RoadAt extends Axial {
  side: number;
  playerIndex: number;
}

/** چیزی که کاربر می‌تواند رویش کلیک کند. */
/**
 * تنظیمات نمایش. حالت ویرایش دو فرق دارد: خودِ خانه‌ها و بندرها قابل انتخاب
 * می‌شوند، و زمین با هر تغییر دوباره چیده می‌شود (چون در ویرایشگر زمین ثابت نیست).
 */
export interface BoardOptions {
  editable?: boolean;
  selected?: Axial | null;
}

export type Pick =
  | { kind: 'vertex'; id: VertexId }
  | { kind: 'edge'; id: EdgeId }
  | { kind: 'hex'; id: Axial }
  | { kind: 'port'; index: number };

export interface BoardData {
  tiles: Tile[];
  ports: Port[];
  buildings: BuildingAt[];
  roads: RoadAt[];
  robber: Axial;
}

export interface Highlights {
  vertices: VertexId[];
  edges: EdgeId[];
  hexes: Axial[];
}

const EMPTY_HIGHLIGHTS: Highlights = { vertices: [], edges: [], hexes: [] };

/**
 * صحنه‌ی برد.
 *
 * تقسیم کار عمدی است: این کلاس فقط «شکل» را می‌سازد و به‌روز نگه می‌دارد و هیچ
 * چیزی درباره‌ی Vue، هاب یا قوانین بازی نمی‌داند. کامپوننت میزبان دوربین، رندر و
 * کلیک را دارد.
 *
 * زمین یک‌بار ساخته می‌شود؛ ساخت‌وسازها و نشانه‌ها با هر به‌روزرسانی دوباره چیده
 * می‌شوند. هندسه‌ها و متریال‌ها همه از قبل ساخته و به اشتراک گذاشته می‌شوند —
 * چون در یک بازی ممکن است صدها به‌روزرسانی بیاید و ساختن هربارِ آن‌ها نشتی است.
 * روی شبکه‌ی منظم، طول همه‌ی ضلع‌ها برابر شعاع هگز است، پس یک هندسه‌ی جاده کافی است.
 */
export class BoardScene {
  readonly root = new THREE.Group();

  /** دریا — یک‌بار ساخته می‌شود و هرگز دوباره چیده نمی‌شود. */
  private readonly backdrop = new THREE.Group();
  private readonly terrain = new THREE.Group();
  private readonly pieces = new THREE.Group();
  private readonly markers = new THREE.Group();

  private readonly disposables: { dispose(): void }[] = [];
  private readonly seatMaterials = new Map<number, THREE.MeshStandardMaterial>();
  private readonly terrainMaterials = new Map<string, THREE.MeshStandardMaterial>();
  private readonly numberTextures = new Map<number, THREE.Texture>();
  private readonly tokenMaterials = new Map<number, THREE.MeshBasicMaterial>();
  private readonly portMaterials = new Map<string, THREE.MeshStandardMaterial>();

  private readonly geo = {
    tile: new THREE.CylinderGeometry(TILE_SIZE * 0.97, TILE_SIZE * 0.97, 0.34, 6),
    token: new THREE.CircleGeometry(TILE_SIZE * 0.34, 32),
    port: new THREE.ConeGeometry(TILE_SIZE * 0.12, TILE_SIZE * 0.3, 4),
    road: new THREE.BoxGeometry(TILE_SIZE * 0.78, 0.1, 0.14),
    settlement: new THREE.BoxGeometry(0.26, 0.2, 0.26),
    settlementRoof: new THREE.ConeGeometry(0.19, 0.18, 4),
    city: new THREE.BoxGeometry(0.26, 0.3, 0.26),
    cityRoof: new THREE.ConeGeometry(0.22, 0.18, 4),
    robber: new THREE.CapsuleGeometry(0.16, 0.24, 4, 12),
    markVertex: new THREE.CylinderGeometry(0.16, 0.16, 0.06, 16),
    markEdge: new THREE.BoxGeometry(TILE_SIZE * 0.7, 0.06, 0.16),
    markHex: new THREE.CylinderGeometry(TILE_SIZE * 0.5, TILE_SIZE * 0.5, 0.05, 6),
  };

  /** نشانه‌ی انتخاب یک نشانگرِ رابط است روی برد، پس رنگ accent را می‌گیرد. */
  private readonly highlightMaterial = new THREE.MeshBasicMaterial({
    color: tokenColor('--hx-accent-2', 0xf2cf7a),
    transparent: true,
    opacity: 0.55,
    depthWrite: false,
  });

  private readonly robberMaterial = new THREE.MeshStandardMaterial({
    color: tokenColor('--hx-piece-robber', 0x17130d),
    roughness: 0.6,
  });

  /** دریا موقع تعویض تم باید به‌روز شود، پس ارجاعش نگه داشته می‌شود. */
  private seaMaterial: THREE.MeshStandardMaterial | null = null;

  /** مش‌هایی که برخورد اشعه با آن‌ها یعنی انتخاب. */
  private readonly hotspots: THREE.Mesh[] = [];

  /** قطعه‌هایی که در به‌روزرسانی قبلی هم بودند — فقط تازه‌ها انیمیشن می‌گیرند. */
  private known = new Set<string>();
  private robberAt = '';
  private built = false;

  constructor() {
    this.root.add(this.backdrop, this.terrain, this.pieces, this.markers);
    this.disposables.push(...Object.values(this.geo), this.highlightMaterial, this.robberMaterial);
  }

  get pickables(): THREE.Mesh[] {
    return this.hotspots;
  }

  /** شعاع تقریبی برد — دوربین از روی همین تنظیم می‌شود. */
  extent(tiles: Tile[]): number {
    let max = TILE_SIZE;
    for (const tile of tiles) {
      const { x, z } = axialToWorld(tile.q, tile.r, TILE_SIZE);
      max = Math.max(max, Math.hypot(x, z));
    }
    return max + TILE_SIZE;
  }

  update(
    data: BoardData,
    highlights: Highlights = EMPTY_HIGHLIGHTS,
    options: BoardOptions = {},
  ): void {
    if (!this.built) {
      this.backdrop.add(this.sea(data.tiles));
      this.built = true;
    }

    // بیرون از ویرایشگر زمین ثابت است و یک‌بار چیده می‌شود؛ در ویرایشگر با هر
    // تغییرِ زمین یا عدد دوباره چیده می‌شود.
    if (options.editable || this.terrain.children.length === 0) {
      this.buildTerrain(data, options.editable === true);
    }

    this.rebuildPieces(data);
    this.rebuildMarkers(highlights, options);
  }

  /**
   * رنگ‌های تم‌دار را دوباره از CSS می‌خواند — بعد از عوض‌شدن تم صدا زده شود.
   *
   * متریال‌ها *جای خودشان* به‌روز می‌شوند و ساخته نمی‌شوند، چون مش‌ها همین
   * نمونه‌ها را به اشتراک گذاشته‌اند؛ ساختن دوباره یعنی باید کل زمین را هم
   * از نو چید. قطعه‌ها و ژتون‌ها عمداً دست‌نخورده می‌مانند: شیء فیزیکی‌اند.
   */
  refreshTheme(): void {
    for (const [terrain, material] of this.terrainMaterials) {
      material.color.copy(tokenColor(TERRAIN_TOKEN[terrain] ?? TERRAIN_TOKEN.Desert!, 0xd9c18f));
    }

    for (const [key, material] of this.portMaterials) {
      material.color.copy(
        key === 'generic'
          ? tokenColor('--hx-port-generic', 0xe8dcc0)
          : tokenColor(RESOURCE_TOKEN[key] ?? '--hx-port-generic', 0xffffff),
      );
    }

    this.seaMaterial?.color.copy(tokenColor('--hx-res-sea', 0x4a90c4));
    this.highlightMaterial.color.copy(tokenColor('--hx-accent-2', 0xf2cf7a));
  }

  dispose(): void {
    for (const item of this.disposables) item.dispose();
    this.disposables.length = 0;
    this.hotspots.length = 0;
    this.seaMaterial = null;
  }

  // ── زمین ثابت ────────────────────────────────────────────────────────

  private buildTerrain(data: BoardData, editable: boolean): void {
    this.clear(this.terrain);

    for (const tile of data.tiles) {
      const { x, z } = axialToWorld(tile.q, tile.r, TILE_SIZE);
      const mesh = new THREE.Mesh(this.geo.tile, this.terrainMaterial(tile.terrain));
      mesh.position.set(x, 0, z);
      mesh.receiveShadow = true;
      mesh.castShadow = true;

      if (editable) {
        mesh.userData.pick = { kind: 'hex', id: { q: tile.q, r: tile.r } } satisfies Pick;
      }

      this.terrain.add(mesh);

      if (tile.number !== null) {
        this.terrain.add(this.numberToken(tile.number, x, z));
      }
    }

    for (const [index, port] of data.ports.entries()) {
      const mesh = this.portMarker(port);

      if (editable) {
        mesh.userData.pick = { kind: 'port', index } satisfies Pick;
      }

      this.terrain.add(mesh);
    }
  }

  private terrainMaterial(terrain: string): THREE.MeshStandardMaterial {
    const existing = this.terrainMaterials.get(terrain);
    if (existing) return existing;

    const material = new THREE.MeshStandardMaterial({
      color: tokenColor(TERRAIN_TOKEN[terrain] ?? TERRAIN_TOKEN.Desert!, 0xd9c18f),
      roughness: 0.78,
      metalness: 0.04,
    });

    this.terrainMaterials.set(terrain, material);
    this.disposables.push(material);
    return material;
  }

  private numberToken(value: number, x: number, z: number): THREE.Mesh {
    let material = this.tokenMaterials.get(value);
    if (!material) {
      material = new THREE.MeshBasicMaterial({
        map: this.numberTexture(value),
        transparent: true,
        depthWrite: false,
      });
      this.tokenMaterials.set(value, material);
      this.disposables.push(material);
    }

    const mesh = new THREE.Mesh(this.geo.token, material);
    mesh.rotation.x = -Math.PI / 2;
    mesh.position.set(x, 0.176, z);
    return mesh;
  }

  /** ژتون عدد روی یک بوم کوچک کشیده می‌شود؛ ۶ و ۸ قرمزند چون پرتکرارترند. */
  private numberTexture(value: number): THREE.Texture {
    const cached = this.numberTextures.get(value);
    if (cached) return cached;

    const canvas = document.createElement('canvas');
    canvas.width = 128;
    canvas.height = 128;

    const ctx = canvas.getContext('2d')!;
    ctx.fillStyle = tokenColor('--hx-token-face', 0xf2e8d5).getStyle();
    ctx.beginPath();
    ctx.arc(64, 64, 60, 0, Math.PI * 2);
    ctx.fill();

    const hot = value === 6 || value === 8;
    ctx.fillStyle = tokenColor(hot ? '--hx-token-hot' : '--hx-token-ink', hot ? 0xb8352f : 0x2b2b2b)
      .getStyle();
    ctx.font = 'bold 62px system-ui, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(value), 64, 62);

    // نقطه‌های احتمال: هرچه بیشتر، عدد پرتکرارتر.
    const pips = 6 - Math.abs(7 - value);
    for (let i = 0; i < pips; i++) {
      ctx.beginPath();
      ctx.arc(64 + (i - (pips - 1) / 2) * 11, 104, 3.4, 0, Math.PI * 2);
      ctx.fill();
    }

    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.anisotropy = 4;
    this.numberTextures.set(value, texture);
    this.disposables.push(texture);

    return texture;
  }

  private portMarker(port: Port): THREE.Mesh {
    const edge = edgeToWorld(edgeId(port, port.side), TILE_SIZE);
    const key = port.resource ?? 'generic';

    let material = this.portMaterials.get(key);
    if (!material) {
      material = new THREE.MeshStandardMaterial({
        color: port.resource
          ? tokenColor(RESOURCE_TOKEN[port.resource] ?? '--hx-port-generic', 0xffffff)
          : tokenColor('--hx-port-generic', 0xe8dcc0),
        roughness: 0.5,
        metalness: 0.2,
      });
      this.portMaterials.set(key, material);
      this.disposables.push(material);
    }

    const mesh = new THREE.Mesh(this.geo.port, material);

    // کمی بیرون از ساحل تا روی خودِ خانه ننشیند.
    const push = 1.3;
    mesh.position.set(edge.x * push, 0.28, edge.z * push);
    mesh.castShadow = true;
    return mesh;
  }

  /** یک‌بار در طول عمر صحنه ساخته می‌شود — بازساختنش با هر تغییر نشتی است. */
  private sea(tiles: Tile[]): THREE.Mesh {
    const radius = this.extent(tiles) + TILE_SIZE * 1.4;
    const geometry = new THREE.CylinderGeometry(radius, radius, 0.14, 72);
    const material = new THREE.MeshStandardMaterial({
      color: tokenColor('--hx-res-sea', 0x4a90c4),
      roughness: 0.22,
      metalness: 0.4,
    });
    this.seaMaterial = material;
    this.disposables.push(geometry, material);

    const mesh = new THREE.Mesh(geometry, material);
    mesh.position.y = -0.14;
    mesh.receiveShadow = true;
    return mesh;
  }

  // ── ساخت‌وسازها و دزد ────────────────────────────────────────────────

  private rebuildPieces(data: BoardData): void {
    this.clear(this.pieces);
    const present = new Set<string>();

    for (const road of data.roads) {
      const key = `r:${edgeKey(edgeId(road, road.side))}`;
      present.add(key);

      const mesh = this.roadMesh(road);
      mesh.userData.spawn = !this.known.has(key);
      this.pieces.add(mesh);
    }

    for (const building of data.buildings) {
      const key = `b:${vertexKey(vertexId(building, building.corner))}:${building.kind}`;
      present.add(key);

      const group = this.buildingMesh(building);
      group.userData.spawn = !this.known.has(key);
      this.pieces.add(group);
    }

    const robberKey = `${data.robber.q},${data.robber.r}`;
    const robber = this.robberMesh(data.robber);
    robber.userData.spawn = this.robberAt !== '' && this.robberAt !== robberKey;
    this.robberAt = robberKey;
    this.pieces.add(robber);

    this.known = present;
  }

  /** قطعه‌هایی که همین حالا تازه ظاهر شده‌اند؛ میزبان آن‌ها را انیمیت می‌کند. */
  takeSpawned(): THREE.Object3D[] {
    const spawned = this.pieces.children.filter((child) => child.userData.spawn === true);
    for (const child of spawned) child.userData.spawn = false;
    return spawned;
  }

  private roadMesh(road: RoadAt): THREE.Mesh {
    const edge = edgeToWorld(edgeId(road, road.side), TILE_SIZE);

    const mesh = new THREE.Mesh(this.geo.road, this.seatMaterial(road.playerIndex));
    mesh.position.set(edge.x, 0.2, edge.z);
    mesh.rotation.y = -edge.angle;
    mesh.castShadow = true;
    return mesh;
  }

  private buildingMesh(building: BuildingAt): THREE.Group {
    const group = new THREE.Group();
    const { x, z } = vertexToWorld(vertexId(building, building.corner), TILE_SIZE);
    const isCity = building.kind === 'City';
    const material = this.seatMaterial(building.playerIndex);

    const body = new THREE.Mesh(isCity ? this.geo.city : this.geo.settlement, material);
    body.position.y = isCity ? 0.32 : 0.27;
    body.castShadow = true;
    group.add(body);

    // شهر بلندتر و پهن‌تر است تا از دور هم از آبادی قابل تشخیص باشد.
    const top = new THREE.Mesh(isCity ? this.geo.cityRoof : this.geo.settlementRoof, material);
    top.position.y = isCity ? 0.56 : 0.46;
    top.rotation.y = Math.PI / 4;
    top.castShadow = true;
    group.add(top);

    group.position.set(x, 0, z);
    return group;
  }

  private robberMesh(hex: Axial): THREE.Mesh {
    const { x, z } = axialToWorld(hex.q, hex.r, TILE_SIZE);

    const mesh = new THREE.Mesh(this.geo.robber, this.robberMaterial);
    mesh.position.set(x, 0.42, z);
    mesh.castShadow = true;
    return mesh;
  }

  private seatMaterial(seat: number): THREE.MeshStandardMaterial {
    const existing = this.seatMaterials.get(seat);
    if (existing) return existing;

    const material = new THREE.MeshStandardMaterial({
      color: tokenColor(SEAT_TOKENS[seat % SEAT_TOKENS.length]!, 0xc0392b),
      roughness: 0.45,
      metalness: 0.15,
    });

    this.seatMaterials.set(seat, material);
    this.disposables.push(material);
    return material;
  }

  // ── نشانه‌های انتخاب ─────────────────────────────────────────────────

  private rebuildMarkers(highlights: Highlights, options: BoardOptions): void {
    this.clear(this.markers);
    this.hotspots.length = 0;

    // در ویرایشگر خودِ زمین و بندرها هدف کلیک‌اند، نه نشانه‌های جداگانه.
    if (options.editable) {
      for (const child of this.terrain.children) {
        if (child instanceof THREE.Mesh && child.userData.pick) {
          this.hotspots.push(child);
        }
      }

      if (options.selected) {
        const { x, z } = axialToWorld(options.selected.q, options.selected.r, TILE_SIZE);
        const ring = new THREE.Mesh(this.geo.markHex, this.highlightMaterial);
        ring.position.set(x, 0.2, z);
        ring.userData.pulse = true;
        this.markers.add(ring);
      }
    }

    for (const vertex of highlights.vertices) {
      const { x, z } = vertexToWorld(vertex, TILE_SIZE);

      const mesh = new THREE.Mesh(this.geo.markVertex, this.highlightMaterial);
      mesh.position.set(x, 0.24, z);
      mesh.userData.pick = { kind: 'vertex', id: vertex } satisfies Pick;
      mesh.userData.pulse = true;
      this.markers.add(mesh);
      this.hotspots.push(mesh);
    }

    for (const edge of highlights.edges) {
      const world = edgeToWorld(edge, TILE_SIZE);

      const mesh = new THREE.Mesh(this.geo.markEdge, this.highlightMaterial);
      mesh.position.set(world.x, 0.22, world.z);
      mesh.rotation.y = -world.angle;
      mesh.userData.pick = { kind: 'edge', id: edge } satisfies Pick;
      mesh.userData.pulse = true;
      this.markers.add(mesh);
      this.hotspots.push(mesh);
    }

    for (const hex of highlights.hexes) {
      const { x, z } = axialToWorld(hex.q, hex.r, TILE_SIZE);

      const mesh = new THREE.Mesh(this.geo.markHex, this.highlightMaterial);
      mesh.position.set(x, 0.2, z);
      mesh.userData.pick = { kind: 'hex', id: hex } satisfies Pick;
      mesh.userData.pulse = true;
      this.markers.add(mesh);
      this.hotspots.push(mesh);
    }
  }

  /** نشانه‌ها آرام نبض می‌زنند تا چشم جای مجاز را پیدا کند. */
  animateMarkers(elapsed: number): void {
    const scale = 1 + Math.sin(elapsed * 3) * 0.12;

    for (const child of this.markers.children) {
      if (child.userData.pulse) {
        child.scale.setScalar(scale);
      }
    }
  }

  private clear(group: THREE.Group): void {
    for (let i = group.children.length - 1; i >= 0; i--) {
      group.remove(group.children[i]!);
    }
  }
}
