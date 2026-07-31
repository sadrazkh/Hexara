/*
 * سرویس‌ورکر هگزارا — این فایل قالب است؛ نسخه‌ی نهایی هنگام build ساخته و در
 * ‎wwwroot/sw.js‎ نوشته می‌شود (‎vite-plugin-service-worker.ts‎). دستی ویرایشش نکن.
 *
 * سیاست عمداً محافظه‌کارانه است. این یک بازی زنده و سمت‌سرور است؛ نشان‌دادن
 * نسخه‌ی کهنه‌ی یک صفحه بدتر از نشان‌ندادن آن است. پس:
 *
 *   • فقط ‎GET‎ و فقط هم‌ریشه دست‌کاری می‌شود.
 *   • دارایی‌های ‎/dist/‎ اول از کش (نامشان هش دارد، پس هرگز کهنه نمی‌شوند).
 *   • صفحه‌ها اول از شبکه، و اگر شبکه نبود صفحه‌ی «آفلاین».
 *   • هاب، ورود، و هر چیز دیگری اصلاً از اینجا رد نمی‌شود.
 */

const VERSION = '__HEXARA_VERSION__';
const CACHE = `hexara-${VERSION}`;
const PRECACHE = __HEXARA_PRECACHE__;

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches
      .open(CACHE)
      // یک فایلِ ازدست‌رفته نباید کل نصب را بیندازد، پس تک‌تک اضافه می‌شوند.
      .then((cache) => Promise.all(PRECACHE.map((url) => cache.add(url).catch(() => undefined))))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  );
});

/** چیزهایی که هرگز نباید از کش رد شوند. */
function isOffLimits(url) {
  return (
    url.pathname.startsWith('/hubs/') ||
    url.pathname.startsWith('/Account/') ||
    url.pathname.startsWith('/Board/') ||
    url.pathname === '/health'
  );
}

async function fromCacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;

  const response = await fetch(request);
  if (response.ok) {
    (await caches.open(CACHE)).put(request, response.clone());
  }

  return response;
}

/**
 * صفحه‌ها اول از شبکه. اگر شبکه نبود، صفحه‌ی آفلاین — و نه نسخه‌ی کهنه‌ی همان
 * صفحه، چون کاربر باید بفهمد که آفلاین است، نه اینکه وضعیت قدیمی را باور کند.
 */
async function fromNetworkFirst(request) {
  try {
    // ‎no-store‎ یعنی کشِ ‎HTTP‎ خودِ مرورگر هم دور زده می‌شود، نه فقط کشِ ما.
    //
    // بی این، ‎fetch‎ می‌توانست همان صفحه‌ی کهنه‌ای را برگرداند که مرورگر نگه
    // داشته بود و ما فکر می‌کردیم «از شبکه گرفتیم». سرور هم ‎no-store‎ می‌فرستد؛
    // این دومی برای صفحه‌هایی است که پیش از آن تغییر کش شده‌اند.
    return await fetch(request, { cache: 'no-store' });
  } catch {
    return (await caches.match('/offline')) ?? Response.error();
  }
}

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin || isOffLimits(url)) return;

  if (url.pathname.startsWith('/dist/') || url.pathname.startsWith('/icons/')) {
    event.respondWith(fromCacheFirst(request));
    return;
  }

  if (request.mode === 'navigate') {
    event.respondWith(fromNetworkFirst(request));
  }
});
