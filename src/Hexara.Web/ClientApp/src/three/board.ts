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

/** رنگ زمین‌ها همان توکن‌های CSS است تا برد و رابط یک‌دست بمانند. */
const TERRAIN_COLOR: Record<string, number> = {
  Desert: 0xcbb187,
  Forest: 0x2f7d4f,
  Hills: 0xc05a3e,
  Pasture: 0x8fc95a,
  Fields: 0xe0b23c,
  Mountains: 0x7d8aa3,
};

const RESOURCE_COLOR: Record<string, number> = {
  Lumber: 0x2f7d4f,
  Brick: 0xc05a3e,
  Wool: 0x8fc95a,
  Grain: 0xe0b23c,
  Ore: 0x7d8aa3,
};

/** رنگ بازیکن‌ها به ترتیب صندلی. */
export const SEAT_COLORS = [0xe0533d, 0x4f9cf9, 0xf2b134, 0x3fbf7f, 0xa06cd5, 0xef7ba8];

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
export type Pick =
  | { kind: 'vertex'; id: VertexId }
  | { kind: 'edge'; id: EdgeId }
  | { kind: 'hex'; id: Axial };

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

  private readonly highlightMaterial = new THREE.MeshBasicMaterial({
    color: 0x5ee7c6,
    transparent: true,
    opacity: 0.55,
    depthWrite: false,
  });

  private readonly robberMaterial = new THREE.MeshStandardMaterial({
    color: 0x11151f,
    roughness: 0.6,
  });

  /** مش‌هایی که برخورد اشعه با آن‌ها یعنی انتخاب. */
  private readonly hotspots: THREE.Mesh[] = [];

  /** قطعه‌هایی که در به‌روزرسانی قبلی هم بودند — فقط تازه‌ها انیمیشن می‌گیرند. */
  private known = new Set<string>();
  private robberAt = '';
  private built = false;

  constructor() {
    this.root.add(this.terrain, this.pieces, this.markers);
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

  update(data: BoardData, highlights: Highlights = EMPTY_HIGHLIGHTS): void {
    if (!this.built) {
      this.buildTerrain(data);
      this.built = true;
    }

    this.rebuildPieces(data);
    this.rebuildMarkers(highlights);
  }

  dispose(): void {
    for (const item of this.disposables) item.dispose();
    this.disposables.length = 0;
    this.hotspots.length = 0;
  }

  // ── زمین ثابت ────────────────────────────────────────────────────────

  private buildTerrain(data: BoardData): void {
    for (const tile of data.tiles) {
      const { x, z } = axialToWorld(tile.q, tile.r, TILE_SIZE);
      const mesh = new THREE.Mesh(this.geo.tile, this.terrainMaterial(tile.terrain));
      mesh.position.set(x, 0, z);
      mesh.receiveShadow = true;
      mesh.castShadow = true;
      this.terrain.add(mesh);

      if (tile.number !== null) {
        this.terrain.add(this.numberToken(tile.number, x, z));
      }
    }

    for (const port of data.ports) {
      this.terrain.add(this.portMarker(port));
    }

    this.terrain.add(this.sea(data.tiles));
  }

  private terrainMaterial(terrain: string): THREE.MeshStandardMaterial {
    const existing = this.terrainMaterials.get(terrain);
    if (existing) return existing;

    const material = new THREE.MeshStandardMaterial({
      color: TERRAIN_COLOR[terrain] ?? TERRAIN_COLOR.Desert!,
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
    ctx.fillStyle = '#f2e8d5';
    ctx.beginPath();
    ctx.arc(64, 64, 60, 0, Math.PI * 2);
    ctx.fill();

    const hot = value === 6 || value === 8;
    ctx.fillStyle = hot ? '#b8352f' : '#2b2b2b';
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
        color: port.resource ? (RESOURCE_COLOR[port.resource] ?? 0xffffff) : 0xe8ecf6,
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

  private sea(tiles: Tile[]): THREE.Mesh {
    const radius = this.extent(tiles) + TILE_SIZE * 1.4;
    const geometry = new THREE.CylinderGeometry(radius, radius, 0.14, 72);
    const material = new THREE.MeshStandardMaterial({
      color: 0x1c4f7c,
      roughness: 0.22,
      metalness: 0.4,
    });
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
      color: SEAT_COLORS[seat % SEAT_COLORS.length],
      roughness: 0.45,
      metalness: 0.15,
    });

    this.seatMaterials.set(seat, material);
    this.disposables.push(material);
    return material;
  }

  // ── نشانه‌های انتخاب ─────────────────────────────────────────────────

  private rebuildMarkers(highlights: Highlights): void {
    this.clear(this.markers);
    this.hotspots.length = 0;

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
