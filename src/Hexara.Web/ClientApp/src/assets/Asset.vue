<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { t } from '@/i18n';
import { assetSpec, type AssetName } from './registry';

/**
 * یک دارایی را با کلیدش نمایش می‌دهد.
 *
 * قوطی همیشه پیش از رسیدنِ تصویر رزرو می‌شود، پس نه چیدمان می‌پرد و نه فرقی
 * می‌کند که تصویر باشد یا نباشد. تا وقتی فایلی نیست، جانشینِ برچسب‌دار
 * می‌نشیند — عمداً دیده می‌شود تا «هنوز کشیده نشده» پنهان نماند.
 */
const props = withDefaults(
  defineProps<{
    name: AssetName;
    /** پهنا؛ ارتفاع از نسبتِ خودِ دارایی درمی‌آید. */
    width?: string;
    /** تزئینی است یا معنا دارد؟ تزئینی از دید صفحه‌خوان پنهان می‌شود. */
    decorative?: boolean;
  }>(),
  { width: 'var(--hx-card-w)', decorative: false },
);

const spec = computed(() => assetSpec(props.name));
const label = computed(() => t(spec.value.labelKey));
const failed = ref(false);

watch(
  () => spec.value.src,
  () => (failed.value = false),
);

const ratio = computed(() => {
  switch (spec.value.shape) {
    case 'card':
      return 'var(--hx-card-ratio)';
    case 'hex':
      return '1 / 1.1547';
    case 'wide':
      return '16 / 9';
    default:
      return '1 / 1';
  }
});

/** حرف اولِ برچسب برای جانشینِ ریز، جایی که کل کلمه جا نمی‌شود. */
const initial = computed(() => label.value.trim().slice(0, 1));
</script>

<template>
  <span
    class="hx-asset"
    :class="`hx-asset--${spec.shape}`"
    :style="{ '--hx-asset-w': width, '--hx-asset-ratio': ratio, '--hx-asset-tone': spec.tone }"
    :role="decorative ? undefined : 'img'"
    :aria-label="decorative ? undefined : label"
    :aria-hidden="decorative ? 'true' : undefined"
  >
    <img
      v-if="spec.src && !failed"
      class="hx-asset__img"
      :src="spec.src"
      alt=""
      decoding="async"
      loading="lazy"
      @error="failed = true"
    />

    <span v-else class="hx-asset__fallback" aria-hidden="true">
      <span class="hx-asset__initial">{{ initial }}</span>
      <span class="hx-asset__label">{{ label }}</span>
    </span>
  </span>
</template>

<style scoped>
.hx-asset {
  display: inline-block;
  position: relative;
  inline-size: var(--hx-asset-w);
  aspect-ratio: var(--hx-asset-ratio);
  border-radius: var(--hx-card-r);
  overflow: hidden;
  vertical-align: middle;
}

.hx-asset--square {
  border-radius: var(--hx-r-sm);
}

.hx-asset__img {
  inline-size: 100%;
  block-size: 100%;
  object-fit: cover;
  display: block;
}

/*
 * جانشین: یک کاشیِ رنگی با حرف اول و نام.
 *
 * عمداً به رنگ تنها تکیه نمی‌کند — قاعده‌ی سیستم طراحی همین است و این‌جا
 * دوچندان مهم است، چون رنگ‌های منابع برای کوررنگ‌ها نزدیک‌اند.
 */
.hx-asset__fallback {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.15em;
  padding: 0.25em;
  text-align: center;
  background: color-mix(in srgb, var(--hx-asset-tone, var(--hx-surface-2)) 30%, var(--hx-panel-bg));
  border: 1px dashed color-mix(in srgb, var(--hx-asset-tone, var(--hx-border-strong)) 60%, transparent);
  color: var(--hx-text);
}

.hx-asset__initial {
  font-size: 1.4em;
  font-weight: var(--hx-weight-bold);
  line-height: 1;
}

.hx-asset__label {
  font-size: var(--hx-text-2xs);
  line-height: 1.1;
  overflow: hidden;
  text-overflow: ellipsis;
  inline-size: 100%;
}

/* روی قوطی‌های ریز فقط حرف اول جا می‌شود. */
.hx-asset--square .hx-asset__label {
  display: none;
}
</style>
