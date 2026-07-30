import { describe, expect, it } from 'vitest';
import * as THREE from 'three';
import { buildScenery } from './scenery';
import { axialToWorld } from './hex';

/**
 * زینتِ زمین چیزی است که دیده می‌شود، پس بیشترِ کیفیتش را تست نمی‌سنجد. آنچه
 * این‌جا سنجیده می‌شود همان چیزهایی است که با چشم دیر لو می‌روند:
 *
 * • قطعی بودن چیدمان — وگرنه دو بازیکن دو بردِ متفاوت می‌بینند.
 * • بیرون نزدن از خانه و روی ژتون عدد نرفتن.
 * • نشستن روی زمین. اولین بار همین را غلط زدم: نیم‌ارتفاع را دو بار حساب کردم
 *   و درخت‌ها غول شدند و سنگ‌ها در هوا ماندند.
 */

const TILE = 1;
const GROUND = 0.17;

/** شعاع ژتون عدد در ‎board.ts‎ — زینت نباید رویش بیفتد. */
const TOKEN_RADIUS = 0.34;

/** شعاع درونیِ شش‌ضلعی؛ دورتر از این یعنی سرک کشیدن به خانه‌ی بغل. */
const INRADIUS = Math.sqrt(3) / 2;

const GREEN = new THREE.Color(0x4a9159);

function colorOf(): THREE.Color {
  return GREEN;
}

function tokenOf(): THREE.Color {
  return GREEN;
}

interface Placed {
  part: string;
  x: number;
  y: number;
  z: number;
  width: number;
  height: number;
}

/** همه‌ی نمونه‌ها را با جا و اندازه‌شان بیرون می‌کشد. */
function placements(tiles: { q: number; r: number; terrain: string }[]): Placed[] {
  const { group } = buildScenery(tiles, TILE, colorOf, tokenOf, true);

  const matrix = new THREE.Matrix4();
  const position = new THREE.Vector3();
  const quaternion = new THREE.Quaternion();
  const scale = new THREE.Vector3();
  const out: Placed[] = [];

  for (const child of group.children) {
    const mesh = child as THREE.InstancedMesh;

    for (let i = 0; i < mesh.count; i++) {
      mesh.getMatrixAt(i, matrix);
      matrix.decompose(position, quaternion, scale);

      out.push({
        part: mesh.geometry.type,
        x: position.x,
        y: position.y,
        z: position.z,
        width: scale.x,
        height: scale.y,
      });
    }
  }

  return out;
}

const ALL_TERRAINS = ['Forest', 'Mountains', 'Fields', 'Pasture', 'Hills', 'Desert'];

describe('buildScenery', () => {
  it.each(ALL_TERRAINS)('puts something on a %s tile', (terrain) => {
    expect(placements([{ q: 0, r: 0, terrain }]).length).toBeGreaterThan(0);
  });

  it('leaves a terrain it does not know bare', () => {
    expect(placements([{ q: 0, r: 0, terrain: 'Volcano' }])).toEqual([]);
  });

  /** جنگل باید چند درخت باشد نه یکی؛ همان چیزی که خواسته شده بود. */
  it('grows a forest out of several trees, not one', () => {
    const trees = placements([{ q: 0, r: 0, terrain: 'Forest' }]).filter(
      (p) => p.part === 'CylinderGeometry',
    );

    expect(trees.length).toBeGreaterThanOrEqual(6);
  });

  it('lays the same board out identically every time', () => {
    const tiles = [
      { q: 0, r: 0, terrain: 'Forest' },
      { q: 1, r: -1, terrain: 'Mountains' },
      { q: -2, r: 1, terrain: 'Fields' },
    ];

    expect(placements(tiles)).toEqual(placements(tiles));
  });

  it('lays two different tiles out differently', () => {
    const here = placements([{ q: 0, r: 0, terrain: 'Forest' }]).map((p) => [p.x, p.z]);
    const there = placements([{ q: 3, r: -1, terrain: 'Forest' }]).map((p) => [p.x, p.z]);

    // جای مطلق که فرق دارد؛ مهم این است که *نقشِ* درون خانه هم یکی نباشد.
    const { x, z } = axialToWorld(3, -1, TILE);
    const shifted = there.map(([px, pz]) => [px! - x, pz! - z]);

    expect(shifted).not.toEqual(here);
  });

  const everywhere = ALL_TERRAINS.map((terrain, index) => ({ q: index - 2, r: 1, terrain }));

  it('keeps every piece inside its own tile', () => {
    for (const tile of everywhere) {
      const centre = axialToWorld(tile.q, tile.r, TILE);

      for (const item of placements([tile])) {
        const reach = Math.hypot(item.x - centre.x, item.z - centre.z) + item.width / 2;

        expect(reach).toBeLessThan(INRADIUS);
      }
    }
  });

  it('keeps the number token clear', () => {
    for (const tile of everywhere) {
      const centre = axialToWorld(tile.q, tile.r, TILE);

      for (const item of placements([tile])) {
        const distance = Math.hypot(item.x - centre.x, item.z - centre.z);

        expect(distance).toBeGreaterThan(TOKEN_RADIUS);
      }
    }
  });

  /** هیچ‌چیز نباید در هوا بماند یا در زمین فرو رفته باشد. */
  it('stands everything on the ground', () => {
    for (const item of placements(everywhere)) {
      expect(item.y).toBeGreaterThanOrEqual(GROUND - 1e-6);
    }
  });

  /** هیچ‌چیز نباید از خودِ خانه بلندتر باشد، وگرنه «یک درخت گنده» می‌شود. */
  it('keeps everything smaller than the tile it stands on', () => {
    for (const item of placements(everywhere)) {
      expect(item.height).toBeLessThan(TILE);
      expect(item.width).toBeLessThan(TILE);
    }
  });

  it('draws one instanced mesh per part, not one mesh per prop', () => {
    const { group } = buildScenery(everywhere, TILE, colorOf, tokenOf, true);
    const instances = group.children.reduce(
      (sum, child) => sum + (child as THREE.InstancedMesh).count,
      0,
    );

    expect(instances).toBeGreaterThan(group.children.length * 3);
    expect(group.children.length).toBeLessThanOrEqual(11);
  });
});
