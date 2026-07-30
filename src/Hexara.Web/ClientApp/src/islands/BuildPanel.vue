<script setup lang="ts">
import { computed } from 'vue';
import { t } from '@/i18n';
import type { GameView } from '@/game/connection';

/**
 * ساخت‌وساز: چه می‌توانی بسازی، به چه قیمتی، و چرا نمی‌توانی.
 *
 * پیش از این هیچ‌جا نوشته نبود که یک آبادی چه می‌خواهد — باید از بیرونِ بازی
 * می‌دانستی. بدتر این‌که دکمه‌ی «خرید کارت توسعه» همیشه فعال بود و تنها راهِ
 * فهمیدنِ نداشتنِ منابع این بود که بزنی و سرور ردت کند.
 *
 * جدولِ هزینه از سرور می‌آید (‎view.costs‎)، پس این‌جا هیچ قاعده‌ای دوباره
 * پیاده نشده است.
 */
const props = defineProps<{ view: GameView; canAct: boolean }>();

const emit = defineEmits<{ buyCard: [] }>();

/** چیزهایی که با کلیک روی برد ساخته می‌شوند، به‌اضافه‌ی کارت که دکمه دارد. */
const KINDS = ['Road', 'Settlement', 'City', 'DevelopmentCard'] as const;
type Kind = (typeof KINDS)[number];

const LABEL: Record<Kind, string> = {
  Road: 'game.buildRoad',
  Settlement: 'game.buildSettlement',
  City: 'game.buildCity',
  DevelopmentCard: 'game.buyCard',
};

const hand = computed(() => props.view.hand?.resources ?? {});

function cost(kind: Kind): [string, number][] {
  return Object.entries(props.view.costs?.[kind] ?? {});
}

function affordable(kind: Kind): boolean {
  return cost(kind).every(([resource, amount]) => (hand.value[resource] ?? 0) >= amount);
}

/** جای خالی روی برد؛ پول داشتن کافی نیست، جا هم باید باشد. */
function hasRoom(kind: Kind): boolean {
  const legal = props.view.legal;

  switch (kind) {
    case 'Road':
      return legal.roads.length > 0;
    case 'Settlement':
      return legal.settlements.length > 0;
    case 'City':
      return legal.cities.length > 0;
    default:
      return props.view.developmentDeckCount > 0;
  }
}

/**
 * چرا نمی‌شود ساخت. ترتیب مهم است: اول پول، بعد جا — چون «پول نداری» چیزی است
 * که خودت می‌توانی درستش کنی و «جا نیست» نه.
 */
function blocker(kind: Kind): string | null {
  if (!props.canAct) return 'game.buildNotYourTurn';
  if (!affordable(kind)) return 'game.buildTooExpensive';
  if (!hasRoom(kind)) return kind === 'DevelopmentCard' ? 'game.buildDeckEmpty' : 'game.buildNoRoom';

  return null;
}

function ready(kind: Kind): boolean {
  return blocker(kind) === null;
}

/** چند تا از این قطعه هنوز مانده — وقتی تمام شود، دیگر هرگز ساخته نمی‌شود. */
function piecesLeft(kind: Kind): number | null {
  const me = props.view.seat === null ? null : props.view.players[props.view.seat];
  if (!me) return null;

  switch (kind) {
    case 'Road':
      return me.roadsLeft;
    case 'Settlement':
      return me.settlementsLeft;
    case 'City':
      return me.citiesLeft;
    default:
      return props.view.developmentDeckCount;
  }
}
</script>

<template>
  <ul class="hx-build">
    <li v-for="kind in KINDS" :key="kind" class="hx-build__row" :class="{ 'is-ready': ready(kind) }">
      <div class="hx-build__head">
        <span class="hx-build__name">{{ t(LABEL[kind]) }}</span>
        <span class="hx-build__left">{{ piecesLeft(kind) }}</span>
      </div>

      <!-- هزینه با نام نوشته می‌شود نه فقط رنگ؛ رنگ‌های منابع برای کوررنگ‌ها نزدیک‌اند. -->
      <ul class="hx-build__cost">
        <li
          v-for="[resource, amount] in cost(kind)"
          :key="resource"
          :class="{ 'is-short': (hand[resource] ?? 0) < amount }"
        >
          {{ amount }}× {{ t(`game.resource.${resource}`) }}
        </li>
      </ul>

      <button
        v-if="kind === 'DevelopmentCard'"
        type="button"
        class="hx-btn hx-btn--sm"
        :class="{ 'hx-btn--primary': ready(kind) }"
        :disabled="!ready(kind)"
        @click="emit('buyCard')"
      >
        {{ t('game.buyCard') }}
      </button>

      <p v-else-if="ready(kind)" class="hx-build__hint">{{ t('game.buildPickOnBoard') }}</p>

      <p v-if="blocker(kind)" class="hx-build__why">{{ t(blocker(kind)!) }}</p>
    </li>
  </ul>
</template>
