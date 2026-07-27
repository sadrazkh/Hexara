/**
 * ثبت سرویس‌ورکر و دکمه‌ی نصب.
 *
 * سرویس‌ورکر فقط در build واقعی ثبت می‌شود. در حالت توسعه، ویت فایل‌ها را از
 * حافظه و با آدرس‌های دیگری می‌دهد؛ کش‌کردنشان یعنی ساعت‌ها جنگیدن با نسخه‌ی
 * کهنه‌ای که نمی‌دانی از کجا می‌آید.
 */

/** رویدادی که مرورگرهای مبتنی بر Chromium قبل از پیشنهاد نصب می‌فرستند. */
interface InstallPrompt extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

let pending: InstallPrompt | null = null;

function wireInstallButton(): void {
  const button = document.querySelector<HTMLElement>('[data-hx-install]');
  if (!button) return;

  window.addEventListener('beforeinstallprompt', (event) => {
    // بدون این، مرورگر نوار پیشنهاد خودش را نشان می‌دهد و ما کنترلی روی جایش نداریم.
    event.preventDefault();
    pending = event as InstallPrompt;
    button.hidden = false;
  });

  button.addEventListener('click', () => {
    void (async () => {
      if (!pending) return;

      await pending.prompt();
      await pending.userChoice;

      // پیشنهاد یک‌بارمصرف است؛ بعد از استفاده باید دور انداخته شود.
      pending = null;
      button.hidden = true;
    })();
  });

  // بعد از نصب دیگر دکمه معنا ندارد.
  window.addEventListener('appinstalled', () => {
    pending = null;
    button.hidden = true;
  });
}

export function initPwa(): void {
  wireInstallButton();

  if (!import.meta.env.PROD || !('serviceWorker' in navigator)) {
    return;
  }

  window.addEventListener('load', () => {
    void navigator.serviceWorker.register('/sw.js').catch((error) => {
      // نبودِ سرویس‌ورکر نباید چیزی را بشکند؛ برنامه بدون آن هم کامل کار می‌کند.
      console.warn('[hexara] service worker registration failed', error);
    });
  });
}
