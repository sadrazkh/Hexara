import { describe, expect, it } from 'vitest';
import { SHELL, buildPrecache, versionOf, type ViteManifestEntry } from './precache';

const manifest: Record<string, ViteManifestEntry> = {
  'src/main.ts': { file: 'assets/main-abc.js', css: ['assets/main-abc.css'] },
  'src/islands/GameLive.vue': { file: 'assets/GameLive-def.js', css: ['assets/GameLive-def.css'] },
  '_three-xyz.js': { file: 'assets/three-xyz.js' },
};

describe('precache list', () => {
  it('takes every script and stylesheet from the vite manifest', () => {
    const precache = buildPrecache(manifest);

    expect(precache).toContain('/dist/assets/main-abc.js');
    expect(precache).toContain('/dist/assets/main-abc.css');
    expect(precache).toContain('/dist/assets/GameLive-def.js');
    expect(precache).toContain('/dist/assets/GameLive-def.css');
    expect(precache).toContain('/dist/assets/three-xyz.js');
  });

  it('includes the offline shell', () => {
    expect(buildPrecache(manifest)).toEqual(expect.arrayContaining(SHELL));
  });

  /**
   * مهم‌ترین چیزی که این تست نگه می‌دارد: هیچ صفحه‌ی وضعیت‌داری پیش‌کش نمی‌شود.
   * بازی سمت سرور است و نشان‌دادن نسخه‌ی کهنه‌اش بدتر از نشان‌ندادن است.
   */
  it('never caches a stateful page', () => {
    const precache = buildPrecache(manifest);
    const forbidden = ['/Lobby', '/Game', '/Board', '/Profile', '/Leaderboard', '/Account', '/hubs'];

    for (const path of forbidden) {
      expect(precache.some((url) => url.startsWith(path))).toBe(false);
    }
  });

  it('has no duplicates and is sorted', () => {
    const precache = buildPrecache(manifest);

    expect(new Set(precache).size).toBe(precache.length);
    expect(precache).toEqual([...precache].sort());
  });

  it('copes with an empty manifest', () => {
    expect(buildPrecache({})).toEqual([...SHELL].sort());
  });
});

describe('version', () => {
  /** نسخه از محتوا می‌آید، پس build دوباره بدون تغییر، سرویس‌ورکر را عوض نمی‌کند. */
  it('is stable for the same assets', () => {
    expect(versionOf(buildPrecache(manifest))).toBe(versionOf(buildPrecache(manifest)));
  });

  it('changes when an asset hash changes', () => {
    const rebuilt = { ...manifest, 'src/main.ts': { file: 'assets/main-zzz.js' } };

    expect(versionOf(buildPrecache(manifest))).not.toBe(versionOf(buildPrecache(rebuilt)));
  });

  it('is short enough to read in a cache name', () => {
    expect(versionOf(['/a'])).toHaveLength(12);
  });
});
