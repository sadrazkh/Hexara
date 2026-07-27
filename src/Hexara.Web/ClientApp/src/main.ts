import './styles/app.css';
import { createApp, type Component } from 'vue';

/**
 * الگوی «جزیره‌های Vue»: سرور HTML کامل می‌فرستد و فقط عناصری که شناسه‌ی
 * ثبت‌شده دارند به کامپوننت Vue تبدیل می‌شوند. کامپوننت‌ها با import پویا
 * بارگذاری می‌شوند تا صفحاتی که آن جزیره را ندارند کدش را دانلود نکنند.
 */
type IslandLoader = () => Promise<{ default: Component }>;

const islands: Record<string, IslandLoader> = {
  'island-hero-board': () => import('./islands/HeroBoard.vue'),
  'island-game-live': () => import('./islands/GameLive.vue'),
};

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
        const module = await load();
        el.innerHTML = '';
        createApp(module.default, propsFrom(el)).mount(el);
      } catch (error) {
        console.error(`[hexara] mounting island "${id}" failed`, error);
      }
    }),
  );
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', () => void mountIslands());
} else {
  void mountIslands();
}
