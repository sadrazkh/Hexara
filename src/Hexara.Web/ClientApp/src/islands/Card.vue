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
  border-radius: var(--hx-card-r);
  transition:
    transform var(--hx-dur-fast) var(--hx-ease),
    box-shadow var(--hx-dur-fast) var(--hx-ease);
}

.hx-card2.is-disabled {
  opacity: var(--hx-opacity-disabled);
}

.hx-card2.is-selected {
  box-shadow: var(--hx-ring-selected);
  transform: translateY(-6px);
}

/* نام پایینِ کارت، روی همان نوارِ تیره‌ای که در خودِ تصویر کشیده شده. */
.hx-card2__name {
  position: absolute;
  inset-inline: 0;
  inset-block-end: 6%;
  text-align: center;
  font-size: var(--hx-text-2xs);
  font-weight: var(--hx-weight-medium);
  color: #f6ecd8;
  text-shadow: 0 1px 3px rgb(0 0 0 / 70%);
  pointer-events: none;
}

.hx-card2__count {
  position: absolute;
  inset-block-start: -6px;
  inset-inline-end: -6px;
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
}
</style>
