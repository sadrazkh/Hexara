import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import type { Plugin } from 'vite';
import {
  buildPrecache,
  versionOf,
  type ViteManifestEntry,
} from './src/pwa/precache';

/**
 * سرویس‌ورکر را بعد از build می‌سازد.
 *
 * منطقِ ساختِ فهرست در ‎src/pwa/precache.ts‎ است تا بشود تستش کرد؛ اینجا فقط
 * خواندن از دیسک و نوشتن روی دیسک است.
 */
export function serviceWorker(options: { outDir: string; template: string }): Plugin {
  return {
    name: 'hexara-service-worker',
    apply: 'build',
    enforce: 'post',

    async closeBundle() {
      const manifestPath = resolve(options.outDir, '.vite/manifest.json');
      const manifest = JSON.parse(await readFile(manifestPath, 'utf8')) as Record<
        string,
        ViteManifestEntry
      >;

      const precache = buildPrecache(manifest);
      const version = versionOf(precache);

      const template = await readFile(resolve(options.template), 'utf8');
      const source = template
        .replace('__HEXARA_VERSION__', version)
        .replace('__HEXARA_PRECACHE__', JSON.stringify(precache, null, 2));

      // بیرون از ‎/dist/‎ نوشته می‌شود تا دامنه‌ی سرویس‌ورکر کل سایت باشد.
      await writeFile(resolve(options.outDir, '..', 'sw.js'), source, 'utf8');
      this.info?.(`service worker ${version} با ${precache.length} فایل پیش‌کش`);
    },
  };
}
