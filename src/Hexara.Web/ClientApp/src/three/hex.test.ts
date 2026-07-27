import { createHash } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import {
  axialToWorld,
  edgeEndpoints,
  edgeId,
  edgeKey,
  edgeToWorld,
  hexDisc,
  neighbor,
  vertexHexes,
  vertexId,
  vertexKey,
  vertexToWorld,
  type Axial,
} from './hex';

const RADIUS = 2;

/**
 * همان اثر انگشتی که ‎CanonicalFingerprintTests.cs‎ در سرور بررسی می‌کند.
 * اگر این دو از هم جدا بیفتند، کلیک کاربر روی گوشه‌ای می‌نشیند که سرور آن را
 * جای دیگری می‌شناسد — و هیچ تست دیگری این را نمی‌گیرد.
 */
function fingerprint(ids: string[]): string {
  const sorted = [...ids].sort();
  const hash = createHash('sha256').update(sorted.join('|')).digest('hex');

  return `${sorted.length}:${hash.slice(0, 16)}`;
}

function allVertexKeys(): string[] {
  const seen = new Set<string>();
  for (const hex of hexDisc(RADIUS)) {
    for (let corner = 0; corner < 6; corner++) {
      seen.add(vertexKey(vertexId(hex, corner)));
    }
  }
  return [...seen];
}

function allEdgeKeys(): string[] {
  const seen = new Set<string>();
  for (const hex of hexDisc(RADIUS)) {
    for (let side = 0; side < 6; side++) {
      seen.add(edgeKey(edgeId(hex, side)));
    }
  }
  return [...seen];
}

describe('canonical ids match the server', () => {
  it('produces the same vertex set as Hexara.Domain', () => {
    expect(fingerprint(allVertexKeys())).toBe('54:7f85baa2ae18b258');
  });

  it('produces the same edge set as Hexara.Domain', () => {
    expect(fingerprint(allEdgeKeys())).toBe('72:28cda893d823f973');
  });

  it('agrees on the named samples', () => {
    expect(vertexKey(vertexId({ q: 0, r: 0 }, 0))).toBe('0,0,0');
    expect(vertexKey(vertexId({ q: 0, r: 0 }, 3))).toBe('-1,0,5');
    expect(vertexKey(vertexId({ q: 1, r: 0 }, 2))).toBe('0,0,0');
    expect(vertexKey(vertexId({ q: 1, r: -1 }, 4))).toBe('0,0,0');

    expect(edgeKey(edgeId({ q: 0, r: 0 }, 0))).toBe('0,0,0');
    expect(edgeKey(edgeId({ q: 1, r: 0 }, 3))).toBe('0,0,0');
    expect(edgeKey(edgeId({ q: 0, r: 0 }, 3))).toBe('-1,0,0');
  });
});

describe('canonicalisation', () => {
  it('collapses all three representations of a corner', () => {
    for (const hex of hexDisc(RADIUS)) {
      for (let corner = 0; corner < 6; corner++) {
        const canonical = vertexKey(vertexId(hex, corner));

        expect(vertexKey(vertexId(neighbor(hex, corner), corner + 2))).toBe(canonical);
        expect(vertexKey(vertexId(neighbor(hex, corner + 1), corner + 4))).toBe(canonical);
      }
    }
  });

  it('collapses both representations of an edge', () => {
    for (const hex of hexDisc(RADIUS)) {
      for (let side = 0; side < 6; side++) {
        expect(edgeKey(edgeId(neighbor(hex, side), side + 3))).toBe(edgeKey(edgeId(hex, side)));
      }
    }
  });

  it('is idempotent', () => {
    const once = vertexId({ q: 1, r: -1 }, 4);
    expect(vertexKey(vertexId(once, once.corner))).toBe(vertexKey(once));
  });

  it('wraps negative and oversized directions', () => {
    expect(vertexKey(vertexId({ q: 0, r: 0 }, -5))).toBe(vertexKey(vertexId({ q: 0, r: 0 }, 1)));
    expect(edgeKey(edgeId({ q: 0, r: 0 }, -4))).toBe(edgeKey(edgeId({ q: 0, r: 0 }, 2)));
  });
});

describe('world positions', () => {
  const SIZE = 1;

  it('places every corner at the hex radius from its centre', () => {
    for (const hex of hexDisc(1)) {
      const centre = axialToWorld(hex.q, hex.r, SIZE);

      for (let corner = 0; corner < 6; corner++) {
        const point = vertexToWorld(vertexId(hex, corner), SIZE);
        const distance = Math.hypot(point.x - centre.x, point.z - centre.z);

        expect(distance).toBeCloseTo(SIZE, 6);
      }
    }
  });

  it('gives every corner three surrounding hexes', () => {
    for (const hex of hexDisc(RADIUS)) {
      for (let corner = 0; corner < 6; corner++) {
        const hexes = vertexHexes(vertexId(hex, corner));
        const keys = new Set(hexes.map((h: Axial) => `${h.q},${h.r}`));

        expect(keys.size).toBe(3);
      }
    }
  });

  it('makes edges exactly one hex-side long', () => {
    for (const hex of hexDisc(1)) {
      for (let side = 0; side < 6; side++) {
        expect(edgeToWorld(edgeId(hex, side), SIZE).length).toBeCloseTo(SIZE, 6);
      }
    }
  });

  it('puts an edge midway between its two corners', () => {
    const edge = edgeId({ q: 0, r: 0 }, 2);
    const [from, to] = edgeEndpoints(edge);
    const a = vertexToWorld(from, SIZE);
    const b = vertexToWorld(to, SIZE);
    const mid = edgeToWorld(edge, SIZE);

    expect(mid.x).toBeCloseTo((a.x + b.x) / 2, 6);
    expect(mid.z).toBeCloseTo((a.z + b.z) / 2, 6);
  });
});
