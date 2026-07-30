<script setup lang="ts">
import { computed, ref } from 'vue';
import { t } from '@/i18n';
import Card from './Card.vue';
import { assetFor, type AssetName } from '@/assets/registry';

/**
 * دستِ بازیکن: کارت‌های واقعی روی هم، نه یک فهرستِ عدد.
 *
 * دو تصمیم که شکلِ این کامپوننت را تعیین کرده‌اند:
 *
 * ۱. **دسته، نه فهرست.** چند کارتِ هم‌پوشان به‌ازای هر منبع کشیده می‌شود تا از
 *    فاصله هم بفهمی چه داری، ولی تعدادِ کارتِ *کشیده‌شده* سقف دارد؛ کسی که ۱۲
 *    چوب دارد نباید ۱۲ کارت پهن کند. عددِ روی نشان همیشه دقیق است.
 *
 * ۲. **کلیک انتخاب می‌کند، نه کشیدن.** کشیدن‌ورها کردن روی موبایل و با
 *    صفحه‌خوان دردسر است؛ همان کاری که لازم است با یک ضربه انجام می‌شود.
 */
const props = withDefaults(
  defineProps<{
    /** نام منبع ⇐ تعداد. */
    resources: Record<string, number>;
    /** نام کارت توسعه ⇐ تعداد؛ تهی یعنی اصلاً نشان نده. */
    development?: Record<string, number> | null;
    /** اگر داده شود، کارت‌ها قابل انتخاب می‌شوند و این‌ها انتخاب‌شده‌اند. */
    selection?: Record<string, number> | null;
  }>(),
  { development: null, selection: null },
);

const emit = defineEmits<{ pick: [resource: string] }>();

const ORDER = ['Lumber', 'Brick', 'Wool', 'Grain', 'Ore'] as const;

/** حداکثر کارتی که روی هم کشیده می‌شود؛ بیشتر از این فقط جا می‌گیرد. */
const MAX_FANNED = 4;

const piles = computed(() =>
  ORDER.map((resource) => ({
    resource,
    asset: assetFor('resource', resource) as AssetName,
    count: props.resources[resource] ?? 0,
    picked: props.selection?.[resource] ?? 0,
  })).filter((pile) => pile.count > 0 || props.selection !== null),
);

const devPiles = computed(() => {
  if (!props.development) return [];

  return Object.entries(props.development)
    .filter(([, count]) => count > 0)
    .map(([kind, count]) => ({ kind, asset: assetFor('dev', kind), count }))
    .filter((pile): pile is { kind: string; asset: AssetName; count: number } => pile.asset !== null);
});

const empty = computed(() => piles.value.every((p) => p.count === 0) && devPiles.value.length === 0);

/** چند کارت واقعاً کشیده شود. */
function fanned(count: number): number {
  return Math.max(1, Math.min(count, MAX_FANNED));
}

/** کدام دسته زیر انگشت یا نشانگر است — برای بزرگ‌نمایی و نمایش جزئیات. */
const hovered = ref<string | null>(null);
</script>

<template>
  <div class="hx-hand">
    <p v-if="empty" class="hx-muted hx-small">{{ t('game.handEmpty') }}</p>

    <ul v-else class="hx-hand__row">
      <li
        v-for="pile in piles"
        :key="pile.resource"
        class="hx-hand__pile"
        :class="{ 'is-hovered': hovered === pile.resource, 'is-empty': pile.count === 0 }"
        :style="{ '--hx-pile-depth': fanned(pile.count) }"
        @mouseenter="hovered = pile.resource"
        @mouseleave="hovered = null"
      >
        <!-- کارت‌های زیرین فقط عمق می‌سازند و از دید صفحه‌خوان پنهان‌اند. -->
        <span
          v-for="layer in fanned(pile.count) - 1"
          :key="layer"
          class="hx-hand__shadow"
          :style="{ '--hx-layer': layer }"
          aria-hidden="true"
        >
          <Card :name="pile.asset" />
        </span>

        <component
          :is="selection ? 'button' : 'span'"
          class="hx-hand__top"
          :type="selection ? 'button' : undefined"
          :disabled="selection ? pile.count === 0 : undefined"
          :aria-pressed="selection ? pile.picked > 0 : undefined"
          @click="selection && emit('pick', pile.resource)"
          @focus="hovered = pile.resource"
          @blur="hovered = null"
        >
          <Card
            :name="pile.asset"
            :count="pile.count"
            :selected="pile.picked > 0"
            :disabled="pile.count === 0"
          />
          <span v-if="pile.picked > 0" class="hx-hand__picked">{{ pile.picked }}</span>
        </component>
      </li>
    </ul>

    <template v-if="devPiles.length > 0">
      <h4 class="hx-hand__title">{{ t('game.devCards') }}</h4>
      <ul class="hx-hand__row">
        <li v-for="pile in devPiles" :key="pile.kind" class="hx-hand__pile">
          <Card :name="pile.asset" :count="pile.count" />
        </li>
      </ul>
    </template>
  </div>
</template>
