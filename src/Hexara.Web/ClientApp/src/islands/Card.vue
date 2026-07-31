<script setup lang="ts">
import { computed } from 'vue';
import { t } from '@/i18n';
import Asset from '@/assets/Asset.vue';
import { assetSpec, type AssetName } from '@/assets/registry';

/**
 * یک کارت: تصویر + نامِ ترجمه‌شده.
 *
 * نام روی تصویر گذاشته می‌شود و **داخل فایل نیست**. اگر «چوب» را در خودِ SVG
 * می‌کشیدم، کاربر انگلیسی هم همان را می‌دید. این‌طور تصویر بی‌زبان می‌ماند و
 * وقتی تصویرِ حرفه‌ای جایش نشست، باز هم متن درست است.
 */
const props = withDefaults(
  defineProps<{
    name: AssetName;
    /** تعداد؛ تهی یعنی کارتِ تکی و بی‌نشان. */
    count?: number | null;
    selected?: boolean;
    disabled?: boolean;
    width?: string;
  }>(),
  { count: null, selected: false, disabled: false, width: 'var(--hx-card-w)' },
);

const label = computed(() => t(assetSpec(props.name).labelKey));
</script>

<template>
  <span
    class="hx-card2"
    :class="{ 'is-selected': selected, 'is-disabled': disabled }"
    :style="{ '--hx-card-width': width }"
  >
    <Asset :name="name" :width="width" decorative />

    <span class="hx-card2__name">{{ label }}</span>

    <!-- تعداد روی کارت، تا دسته‌ی روی هم را با یک نگاه بشماری. -->
    <span v-if="count !== null" class="hx-card2__count">{{ count }}</span>
  </span>
</template>

<style scoped>
.hx-card2 {
  position: relative;
  display: inline-block;
  inline-size: var(--hx-card-width);
  isolation: isolate;
  border-radius: var(--hx-card-r);
  padding: 3px;
  border: 1px solid color-mix(in srgb, #e6bd6a 72%, var(--hx-border));
  background:
    linear-gradient(145deg, #f0ca79, #8a5a1c 38%, #f6d990 63%, #6b4215);
  box-shadow:
    0 9px 18px rgb(0 0 0 / 36%),
    0 0 0 1px rgb(35 21 8 / 72%),
    inset 0 1px rgb(255 246 207 / 52%);
  transition:
    transform var(--hx-dur-fast) var(--hx-ease),
    box-shadow var(--hx-dur-fast) var(--hx-ease);
}

.hx-card2::before {
  content: '';
  position: absolute;
  inset: 3px;
  z-index: 3;
  border-radius: calc(var(--hx-card-r) - 3px);
  background:
    linear-gradient(180deg, transparent 46%, rgb(4 9 12 / 12%) 62%, rgb(4 9 12 / 88%) 100%);
  pointer-events: none;
}

.hx-card2::after {
  content: '';
  position: absolute;
  inset: 5px;
  z-index: 4;
  border: 1px solid rgb(255 235 186 / 28%);
  border-radius: calc(var(--hx-card-r) - 4px);
  box-shadow:
    inset 0 0 18px rgb(0 0 0 / 18%),
    inset 0 0 0 1px rgb(24 12 3 / 22%);
  pointer-events: none;
}

.hx-card2 :deep(.hx-asset) {
  position: relative;
  z-index: 2;
  display: block;
  inline-size: 100%;
  border-radius: calc(var(--hx-card-r) - 3px);
  filter: saturate(1.04) contrast(1.04);
}

.hx-card2.is-disabled {
  opacity: var(--hx-opacity-disabled);
  filter: grayscale(0.45);
}

.hx-card2.is-selected {
  box-shadow:
    var(--hx-ring-selected),
    0 12px 28px color-mix(in srgb, var(--hx-accent) 22%, transparent);
  transform: translateY(-7px) rotate(-1deg);
}

/* نام پایینِ کارت، روی همان نوارِ تیره‌ای که در خودِ تصویر کشیده شده. */
.hx-card2__name {
  position: absolute;
  inset-inline: 0;
  inset-block-end: 7%;
  z-index: 5;
  text-align: center;
  font-size: var(--hx-text-2xs);
  font-weight: var(--hx-weight-bold);
  letter-spacing: -0.01em;
  color: #f6ecd8;
  text-shadow: 0 1px 3px rgb(0 0 0 / 70%);
  pointer-events: none;
}

.hx-card2__count {
  position: absolute;
  inset-block-start: -6px;
  inset-inline-end: -6px;
  z-index: 6;
  min-inline-size: 1.4rem;
  padding: 0 0.3rem;
  border-radius: var(--hx-r-pill);
  background: var(--hx-accent-grad);
  color: var(--hx-on-accent);
  font-size: var(--hx-text-2xs);
  font-weight: var(--hx-weight-bold);
  font-variant-numeric: tabular-nums;
  text-align: center;
  box-shadow: var(--hx-shadow-sm);
  outline: 2px solid color-mix(in srgb, var(--hx-bg) 58%, transparent);
}
</style>
