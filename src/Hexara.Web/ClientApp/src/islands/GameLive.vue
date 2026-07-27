<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { t } from '@/i18n';
import GameBoard from './GameBoard.vue';
import type { BoardData, Highlights, Pick } from '@/three/board';
import { vertexHexes, vertexKey } from '@/three/hex';
import {
  GameConnection,
  type GameEvent,
  type GameView,
  type Hex,
  type Link,
} from '@/game/connection';

const props = defineProps<{ gameId: string }>();

const link = ref<Link>('connecting');
const view = ref<GameView | null>(null);
const log = ref<string[]>([]);
const problem = ref<string | null>(null);

/** وقتی خانه‌ی دزد انتخاب شد، هنوز باید قربانی معلوم شود. */
const robberHex = ref<Hex | null>(null);
const discard = ref<Record<string, number>>({});

/** تاس‌ها قبل از نشستن روی عدد واقعی چند بار می‌چرخند. */
const tumbling = ref<[number, number] | null>(null);

let connection: GameConnection | null = null;
let tumble = 0;

const RESOURCES = ['Lumber', 'Brick', 'Wool', 'Grain', 'Ore'] as const;

const isMyTurn = computed(() => view.value?.legal.isMyTurn ?? false);
const phase = computed(() => view.value?.phase ?? '');

const phaseLabel = computed(() =>
  view.value ? t(`game.phase.${view.value.phase}`) : t('common.loading'),
);

const mustDiscard = computed(() => view.value?.hand?.mustDiscard ?? 0);

const discardTotal = computed(() =>
  Object.values(discard.value).reduce((sum, n) => sum + n, 0),
);

const board = computed<BoardData>(() => ({
  tiles: view.value?.tiles ?? [],
  ports: view.value?.ports ?? [],
  buildings: view.value?.buildings ?? [],
  roads: view.value?.roads ?? [],
  robber: view.value?.robber ?? { q: 0, r: 0 },
}));

/**
 * جاهایی که همین حالا می‌شود رویشان کلیک کرد.
 *
 * وقتی دزد در کار است فقط خانه‌ها روشن می‌شوند تا انتخاب دو مرحله‌ای (خانه، بعد
 * قربانی) با ساخت‌وساز قاطی نشود.
 */
const highlights = computed<Highlights>(() => {
  const current = view.value;
  if (!current || !current.legal.isMyTurn || mustDiscard.value > 0) {
    return { vertices: [], edges: [], hexes: [] };
  }

  if (current.phase === 'MoveRobber') {
    return { vertices: [], edges: [], hexes: robberHex.value ? [] : current.legal.robberTargets };
  }

  return {
    vertices: [...current.legal.settlements, ...current.legal.cities],
    edges: current.legal.roads,
    hexes: [],
  };
});

/** بازیکنانی که می‌شود از آن‌ها دزدید: ساختمانی کنار خانه‌ی دزد دارند و کارت دارند. */
const robberVictims = computed(() => {
  const current = view.value;
  const hex = robberHex.value;
  if (!current || !hex || current.seat === null) return [];

  const owners = new Set<number>();
  for (const building of current.buildings) {
    if (vertexTouches(building, hex)) owners.add(building.playerIndex);
  }

  return current.players.filter(
    (p) => owners.has(p.index) && p.index !== current.seat && p.cardCount > 0,
  );
});

/** آیا این گوشه به آن خانه می‌رسد؟ همان سه هگزی که سرور هم می‌شمارد. */
function vertexTouches(vertex: { q: number; r: number; corner: number }, hex: Hex): boolean {
  return vertexHexes(vertex).some((h) => h.q === hex.q && h.r === hex.r);
}

/**
 * چرخیدن کوتاه تاس‌ها. عمداً دوبعدی است: عددی که مهم است سرور تعیین کرده و
 * انیمیشن فقط باید لحظه‌ی انداختن را حس‌دار کند، نه اینکه نتیجه را معلق نگه دارد.
 */
function rollDice(): void {
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

  clearInterval(tumble);
  const until = Date.now() + 620;

  tumble = window.setInterval(() => {
    if (Date.now() >= until) {
      clearInterval(tumble);
      tumbling.value = null;
      return;
    }

    tumbling.value = [1 + Math.floor(Math.random() * 6), 1 + Math.floor(Math.random() * 6)];
  }, 70);
}

function describe(event: GameEvent): string {
  const kind = String(event.$kind);
  const label = t(`game.event.${kind}`);

  return label === `game.event.${kind}` ? kind : label;
}

function seat(): number {
  return view.value?.seat ?? 0;
}

async function play(action: Record<string, unknown>): Promise<void> {
  problem.value = null;
  const outcome = await connection?.play(action);

  if (outcome && outcome.status !== 'Applied') {
    problem.value =
      outcome.status === 'Rejected'
        ? t(`game.error.${outcome.error}`)
        : t(`game.error.${outcome.status}`);
  }
}

/** کلیک روی برد. کدام حرکت است، از روی مرحله و فهرست مجازها معلوم می‌شود. */
function onPick(pick: Pick): void {
  const current = view.value;
  if (!current || !current.legal.isMyTurn) return;

  if (pick.kind === 'hex') {
    robberHex.value = pick.id;
    return;
  }

  if (pick.kind === 'vertex') {
    const key = vertexKey(pick.id);
    const id = `${pick.id.q},${pick.id.r},${pick.id.corner}`;

    if (current.phase === 'SetupSettlement') {
      void play({ $kind: 'PlaceInitialSettlement', playerIndex: seat(), vertex: id });
      return;
    }

    // شهر روی آبادی خودت ساخته می‌شود و آبادی روی گوشه‌ی خالی؛ این دو هرگز یکی نیستند.
    if (current.legal.cities.some((c) => vertexKey(c) === key)) {
      void play({ $kind: 'BuildCity', playerIndex: seat(), vertex: id });
    } else {
      void play({ $kind: 'BuildSettlement', playerIndex: seat(), vertex: id });
    }

    return;
  }

  // بندر فقط در ویرایشگر قابل انتخاب است؛ سرِ بازی چنین کلیکی نمی‌آید.
  if (pick.kind !== 'edge') return;

  const id = `${pick.id.q},${pick.id.r},${pick.id.side}`;
  const action = current.phase === 'SetupRoad' ? 'PlaceInitialRoad' : 'BuildRoad';
  void play({ $kind: action, playerIndex: seat(), edge: id });
}

const actions = {
  roll: () => play({ $kind: 'RollDice', playerIndex: seat() }),
  endTurn: () => play({ $kind: 'EndTurn', playerIndex: seat() }),
  buyCard: () => play({ $kind: 'BuyDevelopmentCard', playerIndex: seat() }),

  rob: (victim: number | null) =>
    play({
      $kind: 'MoveRobber',
      playerIndex: seat(),
      hex: `${robberHex.value!.q},${robberHex.value!.r}`,
      victim,
    }).then(() => {
      robberHex.value = null;
    }),

  discard: () =>
    play({ $kind: 'DiscardCards', playerIndex: seat(), cards: { ...discard.value } }).then(() => {
      discard.value = {};
    }),
};

function adjust(resource: string, delta: number): void {
  const owned = view.value?.hand?.resources[resource] ?? 0;
  const next = (discard.value[resource] ?? 0) + delta;
  discard.value = { ...discard.value, [resource]: Math.max(0, Math.min(owned, next)) };
}

onMounted(() => {
  connection = new GameConnection(props.gameId, {
    onLink: (value) => (link.value = value),
    onView: (value) => {
      view.value = value;

      // اگر مرحله عوض شد، انتخاب نیمه‌کاره‌ی دزد دیگر معنا ندارد.
      if (value.phase !== 'MoveRobber') robberHex.value = null;
    },
    onEvents: (events) => {
      if (events.some((e) => e.$kind === 'DiceRolled')) rollDice();
      log.value = [...events.map(describe), ...log.value].slice(0, 40);
    },
    onPresence: () => {
      /* حضور از راه نمای تازه هم می‌رسد؛ این فقط واکنش سریع‌تر است. */
    },
    onError: (message) => (problem.value = message),
  });

  void connection.start();
});

onBeforeUnmount(() => {
  clearInterval(tumble);
  void connection?.stop();
});
</script>

<template>
  <div class="hx-live">
    <header class="hx-live__bar">
      <span class="hx-live__link" :data-link="link">{{ t(`game.link.${link}`) }}</span>
      <span v-if="view">{{ t('game.turn') }} {{ view.turnNumber }} · {{ phaseLabel }}</span>
      <span v-if="tumbling" class="hx-live__dice hx-live__dice--rolling">
        🎲 {{ tumbling[0] }} + {{ tumbling[1] }}
      </span>
      <span v-else-if="view?.die1" class="hx-live__dice">🎲 {{ view.die1 }} + {{ view.die2 }}</span>
      <span v-if="view?.winner !== null && view?.winner !== undefined" class="hx-chip hx-chip--live">
        {{ t('game.event.GameWon') }}
      </span>
    </header>

    <p v-if="problem" class="hx-alert" role="alert">{{ problem }}</p>

    <GameBoard v-if="view" :board="board" :highlights="highlights" @pick="onPick">
      <template #fallback>{{ t('game.noWebgl') }}</template>
    </GameBoard>

    <section v-if="view && isMyTurn" class="hx-panel hx-live__controls">
      <h3 class="hx-panel__title">{{ t('game.yourTurn') }}</h3>

      <template v-if="mustDiscard > 0">
        <p>{{ t('game.discardPrompt', mustDiscard) }}</p>
        <div class="hx-live__counters">
          <div v-for="resource in RESOURCES" :key="resource" class="hx-live__counter">
            <button type="button" class="hx-btn hx-btn--sm" @click="adjust(resource, -1)">−</button>
            <span>{{ t(`game.resource.${resource}`) }} {{ discard[resource] ?? 0 }}</span>
            <button type="button" class="hx-btn hx-btn--sm" @click="adjust(resource, 1)">+</button>
          </div>
        </div>
        <button
          type="button"
          class="hx-btn hx-btn--primary"
          :disabled="discardTotal !== mustDiscard"
          @click="actions.discard()"
        >
          {{ t('game.discard') }}
        </button>
      </template>

      <template v-else-if="phase === 'MoveRobber'">
        <p v-if="!robberHex">{{ t('game.pickRobberHex') }}</p>
        <template v-else>
          <p>{{ t('game.pickVictim') }}</p>
          <div class="hx-live__choices">
            <button
              v-for="victim in robberVictims"
              :key="victim.index"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--outline"
              @click="actions.rob(victim.index)"
            >
              {{ victim.displayName }}
            </button>
            <button
              v-if="robberVictims.length === 0"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--primary"
              @click="actions.rob(null)"
            >
              {{ t('common.confirm') }}
            </button>
            <button type="button" class="hx-btn hx-btn--sm hx-btn--ghost" @click="robberHex = null">
              {{ t('common.back') }}
            </button>
          </div>
        </template>
      </template>

      <template v-else-if="phase === 'Roll'">
        <button type="button" class="hx-btn hx-btn--primary hx-btn--lg" @click="actions.roll()">
          {{ t('game.roll') }}
        </button>
      </template>

      <template v-else-if="phase === 'Main'">
        <p class="hx-muted hx-small">{{ t('game.clickTheBoard') }}</p>
        <div class="hx-live__choices">
          <button type="button" class="hx-btn hx-btn--sm" @click="actions.buyCard()">
            {{ t('game.buyCard') }}
          </button>
          <button type="button" class="hx-btn hx-btn--sm hx-btn--primary" @click="actions.endTurn()">
            {{ t('game.endTurn') }}
          </button>
        </div>
      </template>

      <template v-else>
        <p class="hx-muted hx-small">{{ t('game.clickTheBoard') }}</p>
      </template>
    </section>

    <section v-else-if="view" class="hx-panel">
      <p class="hx-muted">{{ t('game.waitingForOthers') }}</p>
    </section>

    <div v-if="view" class="hx-live__grid">
      <section class="hx-panel">
        <h3 class="hx-panel__title">{{ t('game.players') }}</h3>
        <ol class="hx-seats">
          <li
            v-for="player in view.players"
            :key="player.index"
            class="hx-seat"
            :class="{ 'hx-seat--active': player.index === view.currentPlayer }"
          >
            <span
              class="hx-avatar hx-avatar--sm"
              :style="{ '--hx-avatar-color': player.avatarColor }"
            >
              {{ (player.displayName || '?').slice(0, 1).toUpperCase() }}
            </span>
            <span class="hx-seat__name">{{ player.displayName }}</span>
            <span class="hx-chip">{{ player.publicVictoryPoints }} ★</span>
            <span class="hx-chip">{{ player.cardCount }} 🂠</span>
            <span v-if="!player.isOnline" class="hx-chip hx-chip--muted">
              {{ t('game.link.offline') }}
            </span>
          </li>
        </ol>
      </section>

      <section v-if="view.hand" class="hx-panel">
        <h3 class="hx-panel__title">{{ t('game.yourHand') }}</h3>
        <ul class="hx-facts">
          <li v-for="resource in RESOURCES" :key="resource">
            <span>{{ t(`game.resource.${resource}`) }}</span>
            <strong>{{ view.hand.resources[resource] ?? 0 }}</strong>
          </li>
          <li>
            <span>{{ t('game.victoryPoints') }}</span>
            <strong>{{ view.hand.victoryPoints }}</strong>
          </li>
        </ul>
      </section>
    </div>

    <section v-if="log.length > 0" class="hx-panel">
      <h3 class="hx-panel__title">{{ t('game.log') }}</h3>
      <ul class="hx-live__log">
        <li v-for="(line, index) in log" :key="index">{{ line }}</li>
      </ul>
    </section>
  </div>
</template>
