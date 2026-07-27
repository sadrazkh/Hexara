/**
 * سوییچ تم بین «شب روی تخته» (تیره) و «پارشمنت» (روشن).
 *
 * تا وقتی کاربر انتخاب نکرده، هیچ ‎data-theme‎ روی ‎<html>‎ نیست و CSS خودش
 * با ‎prefers-color-scheme‎ تصمیم می‌گیرد؛ یعنی سیستم دنبال می‌شود. اولین
 * کلیک انتخاب را صریح می‌کند و در ‎localStorage‎ می‌ماند.
 *
 * اعمالِ اولیه این‌جا نیست — در یک اسکریپت درون‌خطی در ‎<head>‎ انجام می‌شود.
 * این فایل به‌صورت module بارگذاری می‌شود و اجرایش بعد از اولین رنگ‌آمیزی
 * است، پس اگر تم را این‌جا می‌گذاشتیم صفحه یک لحظه با تم غلط پلک می‌زد.
 */
const KEY = 'hx-theme';

export type Theme = 'dark' | 'light';

/** رویدادی که وقتی تم عوض می‌شود روی document منتشر می‌شود. */
export const THEME_CHANGE = 'hx:themechange';

const lightQuery = (): MediaQueryList => window.matchMedia('(prefers-color-scheme: light)');

/** انتخاب صریح کاربر، یا null اگر هنوز انتخاب نکرده. */
function stored(): Theme | null {
  try {
    const value = localStorage.getItem(KEY);
    return value === 'dark' || value === 'light' ? value : null;
  } catch {
    // حالت خصوصی مرورگر: انتخاب ذخیره نمی‌شود ولی سوییچ در همین صفحه کار می‌کند.
    return null;
  }
}

/** تمی که همین حالا روی صفحه دیده می‌شود — چه صریح چه از سیستم. */
export function resolvedTheme(): Theme {
  const attr = document.documentElement.dataset.theme;
  if (attr === 'dark' || attr === 'light') return attr;
  return lightQuery().matches ? 'light' : 'dark';
}

function syncLabel(): void {
  const button = document.querySelector<HTMLElement>('[data-hx-theme-toggle]');
  if (!button) return;

  // برچسب باید بگوید کلیک بعدی چه می‌کند، نه این‌که الان کجاییم.
  const next: Theme = resolvedTheme() === 'dark' ? 'light' : 'dark';
  const label = next === 'light' ? button.dataset.labelLight : button.dataset.labelDark;
  if (!label) return;

  button.setAttribute('aria-label', label);
  button.setAttribute('title', label);
}

function apply(theme: Theme): void {
  const root = document.documentElement;

  // بدون خفه‌کردن transitionها، هر ویژگیِ رنگی جدا و با زمان‌بندی خودش محو
  // می‌شود و تعویض تم گل‌آلود به‌نظر می‌رسد.
  //
  // خواندن offsetHeight عمدی است: مرورگر را مجبور می‌کند سبک‌ها را همان‌جا
  // و به‌صورت هم‌گام دوباره حساب کند، درحالی‌که transitionها خاموش‌اند. پس
  // وقتی کلاس را برمی‌داریم مقادیر از قبل روی مقصدند و چیزی برای انیمیشن
  // نمانده. با requestAnimationFrame این کار نمی‌کنیم چون در تبِ پنهان
  // اجرا نمی‌شود و کلاس گیر می‌کرد — یعنی transitionها برای همیشه خاموش.
  root.classList.add('hx-theming');
  root.dataset.theme = theme;
  void root.offsetHeight;
  root.classList.remove('hx-theming');

  try {
    localStorage.setItem(KEY, theme);
  } catch {
    // بی‌اهمیت — فقط ماندگاری از دست می‌رود.
  }

  syncLabel();

  // برد سه‌بعدی متریال‌هایش را از همین توکن‌ها می‌سازد و باید بازسازی کند.
  document.dispatchEvent(new CustomEvent<{ theme: Theme }>(THEME_CHANGE, { detail: { theme } }));
}

/** مقدار زنده‌ی یک توکن رنگ — برای کدی که رنگ CSS را لازم دارد (مثل Three.js). */
export function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

export function initTheme(): void {
  const button = document.querySelector<HTMLElement>('[data-hx-theme-toggle]');
  button?.addEventListener('click', () => {
    apply(resolvedTheme() === 'dark' ? 'light' : 'dark');
  });

  // اگر انتخاب صریحی نشده، تغییر تم سیستم را دنبال کن؛ CSS خودش رنگ‌ها را
  // عوض می‌کند و ما فقط برچسب و مصرف‌کننده‌های جاوااسکریپتی را هم‌گام می‌کنیم.
  lightQuery().addEventListener('change', () => {
    if (stored()) return;
    syncLabel();
    document.dispatchEvent(
      new CustomEvent<{ theme: Theme }>(THEME_CHANGE, { detail: { theme: resolvedTheme() } }),
    );
  });

  syncLabel();
}
