import './styles/app.css';
import { createApp, type Component } from 'vue';
import { initTheme } from './theme';
import { initPwa } from './pwa';

/**
 * الگوی «جزیره‌های Vue»: سرور HTML کامل می‌فرستد و فقط عناصری که شناسه‌ی
 * ثبت‌شده دارند به کامپوننت Vue تبدیل می‌شوند. کامپوننت‌ها با import پویا
 * بارگذاری می‌شوند تا صفحاتی که آن جزیره را ندارند کدش را دانلود نکنند.
 */
type IslandLoader = () => Promise<{ default: Component }>;

const islands: Record<string, IslandLoader> = {
  'island-hero-board': () => import('./islands/HeroBoard.vue'),
  'island-game-live': () => import('./islands/GameLive.vue'),
  'island-room-live': () => import('./islands/RoomLive.vue'),
  'island-board-editor': () => import('./islands/BoardEditor.vue'),
};

/**
 * جزیره‌هایی که سوار شدنشان می‌تواند صبر کند.
 *
 * بردِ سه‌بعدیِ صفحه‌ی اول تزئینی است ولی نیم مگابایت ‎three.js‎ می‌آورد — یعنی
 * صفحه‌ای که کاربر هنوز ثبت‌نام هم نکرده، پیش از هر چیز دیگری منتظر آن می‌ماند.
 * پس تا بی‌کار شدنِ مرورگر عقب می‌افتد. **چیزی از ظاهر کم نمی‌شود**، فقط ترتیب
 * عوض می‌شود: بقیه‌ی صفحه اول می‌آید.
 *
 * جزیره‌های دیگر خودِ صفحه‌اند (بازی، اتاق، ویرایشگر) و عقب انداختنشان یعنی
 * نشان‌دادنِ یک صفحه‌ی خالی.
 */
const DEFERRED = new Set(['island-hero-board']);

/** تا بی‌کار شدنِ مرورگر صبر می‌کند؛ اگر پشتیبانی نشد، یک تیکِ کوتاه. */
function whenIdle(): Promise<void> {
  return new Promise((resolve) => {
    const idle = window.requestIdleCallback as typeof window.requestIdleCallback | undefined;

    if (idle) idle(() => resolve(), { timeout: 2000 });
    else window.setTimeout(resolve, 200);
  });
}

/** تمام ‎data-*‎ عنصر میزبان به عنوان prop به کامپوننت داده می‌شود. */
function propsFrom(el: HTMLElement): Record<string, unknown> {
  const props: Record<string, unknown> = {};
  for (const [rawKey, rawValue] of Object.entries(el.dataset)) {
    if (rawValue === undefined) continue;

    if (rawValue === 'true' || rawValue === 'false') {
      props[rawKey] = rawValue === 'true';
    } else if (rawValue !== '' && !Number.isNaN(Number(rawValue))) {
      props[rawKey] = Number(rawValue);
    } else if (rawValue.startsWith('{') || rawValue.startsWith('[')) {
      try {
        props[rawKey] = JSON.parse(rawValue);
      } catch {
        props[rawKey] = rawValue;
      }
    } else {
      props[rawKey] = rawValue;
    }
  }
  return props;
}

async function mountIslands(): Promise<void> {
  await Promise.all(
    Object.entries(islands).map(async ([id, load]) => {
      const el = document.getElementById(id);
      if (!el) return;

      try {
        if (DEFERRED.has(id)) await whenIdle();

        const module = await load();
        el.innerHTML = '';
        createApp(module.default, propsFrom(el)).mount(el);
      } catch (error) {
        console.error(`[hexara] mounting island "${id}" failed`, error);
      }
    }),
  );
}

function start(): void {
  initTheme();
  initPwa();
  void mountIslands();
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', start);
} else {
  start();
}
