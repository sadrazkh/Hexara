<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { t } from '@/i18n';
import {
  GameConnection,
  type GameEvent,
  type GameView,
  type Hex,
  type Link,
  type Vertex,
} from '@/game/connection';

const props = defineProps<{ gameId: string }>();

const link = ref<Link>('connecting');
const view = ref<GameView | null>(null);
const log = ref<string[]>([]);
const problem = ref<string | null>(null);

/** وقتی خانه‌ی دزد انتخاب شد، هنوز باید قربانی معلوم شود. */
const robberHex = ref<Hex | null>(null);
const discard = ref<Record<string, number>>({});

let connection: GameConnection | null = null;

const RESOURCES = ['Lumber', 'Brick', 'Wool', 'Grain', 'Ore'] as const;

const isMyTurn = computed(() => view.value?.legal.isMyTurn ?? false);

const phaseLabel = computed(() =>
  view.value ? t(`game.phase.${view.value.phase}`) : t('common.loading'),
);

const discardTotal = computed(() =>
  Object.values(discard.value).reduce((sum, n) => sum + n, 0),
);

/** بازیکنانی که می‌شود از آن‌ها دزدید: ساختمانی کنار این خانه دارند و کارت دارند. */
const robberVictims = computed(() => {
  const current = view.value;
  const hex = robberHex.value;
  if (!current || !hex || current.seat == null) return [];

  const owners = new Set<number>();
  for (const building of current.buildings) {
    if (touches(building, hex)) owners.add(building.playerIndex);
  }

  return current.players.filter(
    (p) => owners.has(p.index) && p.index !== current.seat && p.cardCount > 0,
  );
});

/** آیا این گوشه به آن خانه می‌رسد؟ گوشه‌ها کانونی‌اند، پس سه نمایش هم‌ارز بررسی می‌شود. */
function touches(vertex: Vertex, hex: Hex): boolean {
  const directions = [
    [1, 0],
    [1, -1],
    [0, -1],
    [-1, 0],
    [-1, 1],
    [0, 1],
  ];

  const first = directions[vertex.corner % 6]!;
  const second = directions[(vertex.corner + 1) % 6]!;

  return (
    (vertex.q === hex.q && vertex.r === hex.r) ||
    (vertex.q + first[0]! === hex.q && vertex.r + first[1]! === hex.r) ||
    (vertex.q + second[0]! === hex.q && vertex.r + second[1]! === hex.r)
  );
}

function describe(event: GameEvent): string {
  const kind = String(event.$kind);
  const key = `game.event.${kind}`;
  const label = t(key);

  return label === key ? kind : label;
}

async function play(action: Record<string, unknown>): Promise<void> {
  problem.value = null;
  const outcome = await connection?.play(action);

  if (outcome && outcome.status !== 'Applied') {
    problem.value =
      outcome.status === 'Rejected' ? t(`game.error.${outcome.error}`) : t(`game.error.${outcome.status}`);
  }
}

function seat(): number {
  return view.value?.seat ?? 0;
}

const actions = {
  roll: () => play({ $kind: 'RollDice', playerIndex: seat() }),
  endTurn: () => play({ $kind: 'EndTurn', playerIndex: seat() }),
  buyCard: () => play({ $kind: 'BuyDevelopmentCard', playerIndex: seat() }),

  setupSettlement: (v: Vertex) =>
    play({ $kind: 'PlaceInitialSettlement', playerIndex: seat(), vertex: vertexId(v) }),
  setupRoad: (e: { q: number; r: number; side: number }) =>
    play({ $kind: 'PlaceInitialRoad', playerIndex: seat(), edge: edgeId(e) }),

  settlement: (v: Vertex) =>
    play({ $kind: 'BuildSettlement', playerIndex: seat(), vertex: vertexId(v) }),
  road: (e: { q: number; r: number; side: number }) =>
    play({ $kind: 'BuildRoad', playerIndex: seat(), edge: edgeId(e) }),
  city: (v: Vertex) => play({ $kind: 'BuildCity', playerIndex: seat(), vertex: vertexId(v) }),

  moveRobber: (victim: number | null) =>
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

// شناسه‌های هندسی روی سیم رشته‌اند — همان قالبی که مبدل‌های سرور می‌فهمند.
function vertexId(v: Vertex): string {
  return `${v.q},${v.r},${v.corner}`;
}

function edgeId(e: { q: number; r: number; side: number }): string {
  return `${e.q},${e.r},${e.side}`;
}

function adjust(resource: string, delta: number): void {
  const owned = view.value?.hand?.resources[resource] ?? 0;
  const next = (discard.value[resource] ?? 0) + delta;
  discard.value = { ...discard.value, [resource]: Math.max(0, Math.min(owned, next)) };
}

onMounted(() => {
  connection = new GameConnection(props.gameId, {
    onLink: (value) => (link.value = value),
    onView: (value) => (view.value = value),
    onEvents: (events) => {
      log.value = [...events.map(describe), ...log.value].slice(0, 40);
    },
    onPresence: () => {
      /* حضور از راه نمای تازه هم می‌رسد؛ اینجا فقط برای واکنش سریع‌تر است. */
    },
    onError: (message) => (problem.value = message),
  });

  void connection.start();
});

onBeforeUnmount(() => void connection?.stop());
</script>

<template>
  <div class="hx-live">
    <header class="hx-live__bar">
      <span class="hx-live__link" :data-link="link">{{ t(`game.link.${link}`) }}</span>
      <span v-if="view">{{ t('game.turn') }} {{ view.turnNumber }} · {{ phaseLabel }}</span>
      <span v-if="view?.die1">🎲 {{ view.die1 }} + {{ view.die2 }}</span>
    </header>

    <p v-if="problem" class="hx-alert" role="alert">{{ problem }}</p>

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
            <span class="hx-avatar hx-avatar--sm" :style="{ '--hx-avatar-color': player.avatarColor }">
              {{ (player.displayName || '?').slice(0, 1).toUpperCase() }}
            </span>
            <span class="hx-seat__name">{{ player.displayName }}</span>
            <span class="hx-chip">{{ player.publicVictoryPoints }} ★</span>
            <span class="hx-chip">{{ player.cardCount }} 🂠</span>
            <span v-if="!player.isOnline" class="hx-chip hx-chip--muted">{{ t('game.link.offline') }}</span>
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

    <!-- تابلوی موقت فرمان: در فاز ۶ جای خودش را به برد سه‌بعدی می‌دهد. -->
    <section v-if="view && isMyTurn" class="hx-panel hx-live__controls">
      <h3 class="hx-panel__title">{{ t('game.yourTurn') }}</h3>

      <template v-if="view.hand && view.hand.mustDiscard > 0">
        <p>{{ t('game.discardPrompt', view.hand.mustDiscard) }}</p>
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
          :disabled="discardTotal !== view.hand.mustDiscard"
          @click="actions.discard()"
        >
          {{ t('game.discard') }}
        </button>
      </template>

      <template v-else-if="view.phase === 'MoveRobber'">
        <template v-if="!robberHex">
          <p>{{ t('game.pickRobberHex') }}</p>
          <div class="hx-live__choices">
            <button
              v-for="hex in view.legal.robberTargets"
              :key="`${hex.q},${hex.r}`"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--outline"
              @click="robberHex = hex"
            >
              {{ hex.q }},{{ hex.r }}
            </button>
          </div>
        </template>
        <template v-else>
          <p>{{ t('game.pickVictim') }}</p>
          <div class="hx-live__choices">
            <button
              v-for="victim in robberVictims"
              :key="victim.index"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--outline"
              @click="actions.moveRobber(victim.index)"
            >
              {{ victim.displayName }}
            </button>
            <button
              v-if="robberVictims.length === 0"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--primary"
              @click="actions.moveRobber(null)"
            >
              {{ t('common.confirm') }}
            </button>
          </div>
        </template>
      </template>

      <template v-else-if="view.phase === 'SetupSettlement' || view.phase === 'SetupRoad'">
        <p>{{ t(`game.phase.${view.phase}`) }}</p>
        <div class="hx-live__choices">
          <button
            v-for="spot in view.legal.settlements"
            :key="`v${spot.q},${spot.r},${spot.corner}`"
            type="button"
            class="hx-btn hx-btn--sm hx-btn--outline"
            @click="actions.setupSettlement(spot)"
          >
            {{ spot.q }},{{ spot.r }},{{ spot.corner }}
          </button>
          <button
            v-for="spot in view.legal.roads"
            :key="`e${spot.q},${spot.r},${spot.side}`"
            type="button"
            class="hx-btn hx-btn--sm hx-btn--outline"
            @click="actions.setupRoad(spot)"
          >
            {{ spot.q }},{{ spot.r }},{{ spot.side }}
          </button>
        </div>
      </template>

      <template v-else-if="view.phase === 'Roll'">
        <button type="button" class="hx-btn hx-btn--primary hx-btn--lg" @click="actions.roll()">
          {{ t('game.roll') }}
        </button>
      </template>

      <template v-else-if="view.phase === 'Main'">
        <div class="hx-live__choices">
          <button type="button" class="hx-btn hx-btn--sm" @click="actions.buyCard()">
            {{ t('game.buyCard') }}
          </button>
          <button type="button" class="hx-btn hx-btn--sm hx-btn--primary" @click="actions.endTurn()">
            {{ t('game.endTurn') }}
          </button>
        </div>

        <details>
          <summary>{{ t('game.buildRoad') }}</summary>
          <div class="hx-live__choices">
            <button
              v-for="spot in view.legal.roads"
              :key="`r${spot.q},${spot.r},${spot.side}`"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--outline"
              @click="actions.road(spot)"
            >
              {{ spot.q }},{{ spot.r }},{{ spot.side }}
            </button>
          </div>
        </details>

        <details>
          <summary>{{ t('game.buildSettlement') }}</summary>
          <div class="hx-live__choices">
            <button
              v-for="spot in view.legal.settlements"
              :key="`s${spot.q},${spot.r},${spot.corner}`"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--outline"
              @click="actions.settlement(spot)"
            >
              {{ spot.q }},{{ spot.r }},{{ spot.corner }}
            </button>
          </div>
        </details>

        <details v-if="view.legal.cities.length > 0">
          <summary>{{ t('game.buildCity') }}</summary>
          <div class="hx-live__choices">
            <button
              v-for="spot in view.legal.cities"
              :key="`c${spot.q},${spot.r},${spot.corner}`"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--outline"
              @click="actions.city(spot)"
            >
              {{ spot.q }},{{ spot.r }},{{ spot.corner }}
            </button>
          </div>
        </details>
      </template>
    </section>

    <section v-else-if="view" class="hx-panel">
      <p class="hx-muted">{{ t('game.waitingForOthers') }}</p>
    </section>

    <section v-if="log.length > 0" class="hx-panel">
      <h3 class="hx-panel__title">{{ t('game.log') }}</h3>
      <ul class="hx-live__log">
        <li v-for="(line, index) in log" :key="index">{{ line }}</li>
      </ul>
    </section>
  </div>
</template>
