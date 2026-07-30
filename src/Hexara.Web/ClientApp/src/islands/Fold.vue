<script setup lang="ts">
/**
 * پنلی که روی صفحه‌ی کوچک تا می‌شود و روی صفحه‌ی بزرگ باز است.
 *
 * دو ریختِ متفاوت دارد و این عمدی است: روی صفحه‌ی بزرگ یک ‎<section>‎ با تیتر
 * واقعی است، و روی موبایل یک ‎<details>‎ که خودِ مرورگر بازوبستش را می‌داند.
 * راه دیگر این بود که همیشه ‎<details>‎ باشد و روی دسکتاپ بازش نگه داریم، ولی
 * آن‌وقت تیتر یک دکمه‌ی بی‌کار می‌شد که با کیبورد فوکوس می‌گیرد و هیچ کاری
 * نمی‌کند. چند خط تکرار ارزانش است.
 */
const props = defineProps<{
  label: string;
  /** روی صفحه‌ی بزرگ true است: بی‌تا و همیشه باز. */
  always?: boolean;
  open?: boolean;
}>();

const emit = defineEmits<{ 'update:open': [boolean] }>();

function onToggle(event: Event): void {
  const details = event.target as HTMLDetailsElement;

  // ‎toggle‎ در بعضی مرورگرها سرِ رندر اول هم می‌آید؛ فقط تغییرِ واقعی را می‌فرستیم.
  if (details.open !== props.open) emit('update:open', details.open);
}
</script>

<template>
  <section v-if="always" class="hx-panel hx-panel--rail">
    <h3 class="hx-panel__title">{{ label }}</h3>
    <slot />
  </section>

  <details v-else class="hx-panel hx-panel--rail hx-fold" :open="open" @toggle="onToggle">
    <summary class="hx-fold__head">{{ label }}</summary>
    <div class="hx-fold__body"><slot /></div>
  </details>
</template>
