import { createHash } from 'node:crypto';

/** شکل هر ورودی در manifest ویت — فقط چیزهایی که لازم داریم. */
export interface ViteManifestEntry {
  file: string;
  css?: string[];
}

/**
 * پوسته‌ی برنامه: چیزهایی که بدون شبکه هم باید باشند.
 *
 * **اینجا هیچ صفحه‌ای اضافه نکن.** بازی و لابی و ویرایشگر همه وضعیت سمت سرور
 * دارند و نسخه‌ی کهنه‌شان بدتر از نبودنشان است — تنها صفحه‌ی مجاز، صفحه‌ی
 * «آفلاین» است که خودش می‌گوید داده‌ای ندارد.
 */
export const SHELL = ['/offline', '/favicon.svg', '/icons/icon-192.png'];

/**
 * فهرست پیش‌کش از manifest ویت ساخته می‌شود نه دستی: نام فایل‌ها هش دارند و هر
 * build عوض می‌شوند، پس فهرست دستی محکوم به کهنه‌شدن است.
 */
export function buildPrecache(manifest: Record<string, ViteManifestEntry>): string[] {
  const assets = new Set<string>(SHELL);

  for (const entry of Object.values(manifest)) {
    assets.add(`/dist/${entry.file}`);

    for (const css of entry.css ?? []) {
      assets.add(`/dist/${css}`);
    }
  }

  return [...assets].sort();
}

/**
 * نسخه از خودِ فهرست مشتق می‌شود، نه از زمان یا شماره‌ی build. یعنی اگر
 * دارایی‌ها عوض نشده باشند سرویس‌ورکر هم بایت‌به‌بایت همان می‌ماند و مرورگر
 * بی‌دلیل کش را دور نمی‌ریزد.
 */
export function versionOf(precache: readonly string[]): string {
  return createHash('sha256').update(precache.join('|')).digest('hex').slice(0, 12);
}
