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

/** معامله با بانک: چه می‌دهی و چه می‌خواهی. */
const bankGive = ref<string | null>(null);
const bankTake = ref<string | null>(null);

/** پیشنهاد به بازیکن‌ها: دو بسته‌ی منابع. */
const offerGive = ref<Record<string, number>>({});
const offerTake = ref<Record<string, number>>({});

/** تاس‌ها قبل از نشستن روی عدد واقعی چند بار می‌چرخند. */
const tumbling = ref<[number, number] | null>(null);

/** ساعت دیواری که هر ثانیه تیک می‌زند — فقط برای شمارش معکوس مهلت نوبت. */
const now = ref(Date.now());

let connection: GameConnection | null = null;
let tumble = 0;
let ticker = 0;

const RESOURCES = ['Lumber', 'Brick', 'Wool', 'Grain', 'Ore'] as const;

const isMyTurn = computed(() => view.value?.legal.isMyTurn ?? false);
const phase = computed(() => view.value?.phase ?? '');

const phaseLabel = computed(() =>
  view.value ? t(`game.phase.${view.value.phase}`) : t('common.loading'),
);

const mustDiscard = computed(() => view.value?.hand?.mustDiscard ?? 0);

/**
 * برنده‌ی بازی — در بازی تیمی همه‌ی هم‌تیمی‌ها برنده‌اند، چون امتیاز در تیم مشترک
 * است. سرور یک صندلی می‌فرستد و بقیه‌ی تیم از روی ‎team‎ درمی‌آید.
 */
const champion = computed(() => {
  const current = view.value;
  if (!current || current.winner === null) return null;

  return current.players[current.winner] ?? null;
});

const winners = computed(() => {
  const current = view.value;
  const first = champion.value;
  if (!current || !first) return [];

  return first.team === null ? [first] : current.players.filter((p) => p.team === first.team);
});

const isOver = computed(() => champion.value !== null);

const iWon = computed(() => {
  const seatNow = view.value?.seat;
  return seatNow !== null && seatNow !== undefined && winners.value.some((p) => p.index === seatNow);
});

/** جدول پایانی: از پُرامتیاز به کم‌امتیاز، چون اول از همه دنبال جای خودت می‌گردی. */
const standings = computed(() =>
  [...(view.value?.players ?? [])].sort(
    (a, b) => b.publicVictoryPoints - a.publicVictoryPoints || a.index - b.index,
  ),
);

/** بازیکنی که همه منتظرش هستند. در مرحله‌ی دور ریختن، بدهکارها منتظرند نه نوبت‌دار. */
const waitingOn = computed(() => {
  const current = view.value;
  if (!current) return null;

  if (current.phase === 'Discard') {
    const owing = Object.keys(current.pendingDiscards).map(Number);
    return owing.length > 0 ? (current.players[owing[0]!] ?? null) : null;
  }

  return current.players[current.currentPlayer] ?? null;
});

/**
 * ثانیه‌های مانده تا بات جای بازیکنِ معطل را بگیرد.
 *
 * مهلتِ کسی که قطع شده کوتاه‌تر است. اگر پوشش خودکار خاموش باشد سرور صفر
 * می‌فرستد و چیزی نشان داده نمی‌شود — ساعتی که کسی نبیند تله است.
 */
const countdown = computed(() => {
  const current = view.value;
  const target = waitingOn.value;
  if (!current || !target || current.deadlineSeconds <= 0 || current.winner !== null) return null;

  const limit = target.isOnline ? current.deadlineSeconds : current.absentGraceSeconds;
  const elapsed = (now.value - Date.parse(current.updatedAt)) / 1000;

  return Math.max(0, Math.ceil(limit - elapsed));
});

/**
 * آیا کسی که منتظرش هستیم غایب است و بات دارد جایش را می‌گیرد؟
 *
 * بعد از تمام شدن بازی دیگر منتظر کسی نیستیم؛ بی این شرط، صفحه‌ی پایانِ بازی
 * هم‌زمان «بات جایش بازی می‌کند» و «منتظر بازیکنان دیگر» را نشان می‌داد.
 */
const covered = computed(
  () =>
    !isOver.value &&
    // مهلتِ صفر یعنی پوشش خودکار خاموش است، پس هیچ باتی جای کسی را نمی‌گیرد.
    // بی این شرط، بازی‌ای که بات ندارد هم می‌گفت «بات جایش بازی می‌کند» و
    // بازیکن بی‌جهت منتظر می‌مانْد.
    (view.value?.deadlineSeconds ?? 0) > 0 &&
    waitingOn.value !== null &&
    !waitingOn.value.isOnline,
);

const discardTotal = computed(() =>
  Object.values(discard.value).reduce((sum, n) => sum + n, 0),
);

// ── معامله ──────────────────────────────────────────────────────────────

const hand = computed(() => view.value?.hand?.resources ?? {});

/** نرخ بانک برای این منبع؛ ۴ پیش‌فرضِ بی‌بندر است. */
function rateOf(resource: string): number {
  return view.value?.legal.tradeRates?.[resource] ?? 4;
}

/** معامله با بانک فقط وقتی ممکن است که به‌اندازه‌ی نرخ از آن منبع داشته باشی. */
const canBankTrade = computed(() => {
  const give = bankGive.value;
  const take = bankTake.value;

  return (
    give !== null && take !== null && give !== take && (hand.value[give] ?? 0) >= rateOf(give)
  );
});

const offer = computed(() => view.value?.pendingTrade ?? null);

const iProposed = computed(() => offer.value !== null && offer.value.proposer === view.value?.seat);

/** آیا از من نظر خواسته شده و هنوز جواب نداده‌ام؟ */
const myResponse = computed(() => {
  const current = offer.value;
  const mySeat = view.value?.seat;
  if (!current || mySeat === null || mySeat === undefined) return null;

  return current.responses[String(mySeat)] ?? null;
});

/** کسانی که پیشنهاد را پذیرفته‌اند — پیشنهاددهنده با یکی‌شان قطعی می‌کند. */
const acceptedBy = computed(() => {
  const current = offer.value;
  if (!current) return [];

  return Object.entries(current.responses)
    .filter(([, response]) => response === 'Accepted')
    .map(([seatIndex]) => view.value?.players[Number(seatIndex)])
    .filter((player): player is NonNullable<typeof player> => player !== undefined);
});

function nameOf(seatIndex: number): string {
  return view.value?.players[seatIndex]?.displayName ?? '';
}

/** بسته‌ی منابع به شکل خواندنی: «۲ چوب، ۱ گندم». */
function describeBundle(bundle: Record<string, number>): string {
  return Object.entries(bundle)
    .filter(([, amount]) => amount > 0)
    .map(([resource, amount]) => `${amount} ${t(`game.resource.${resource}`)}`)
    .join('، ');
}

const offerGiveTotal = computed(() =>
  Object.values(offerGive.value).reduce((sum, n) => sum + n, 0),
);
const offerTakeTotal = computed(() =>
  Object.values(offerTake.value).reduce((sum, n) => sum + n, 0),
);

/** هر دو طرف باید چیزی داشته باشد و آنچه می‌دهی باید در دستت باشد. */
const canPropose = computed(() => {
  if (offerGiveTotal.value === 0 || offerTakeTotal.value === 0) return false;

  return Object.entries(offerGive.value).every(
    ([resource, amount]) => amount <= (hand.value[resource] ?? 0),
  );
});

/** شمارنده‌ی سمتِ «می‌دهم» به دستِ خودم محدود است؛ سمتِ «می‌خواهم» نه. */
function adjustOffer(side: 'give' | 'take', resource: string, delta: number): void {
  const target = side === 'give' ? offerGive : offerTake;
  const ceiling = side === 'give' ? (hand.value[resource] ?? 0) : 19;
  const next = (target.value[resource] ?? 0) + delta;

  target.value = { ...target.value, [resource]: Math.max(0, Math.min(ceiling, next)) };
}

function clearOffer(): void {
  offerGive.value = {};
  offerTake.value = {};
}

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

/** بسته را از صفرها پاک می‌کند؛ موتور بسته‌ی خالی را رد می‌کند. */
function packed(bundle: Record<string, number>): Record<string, number> {
  return Object.fromEntries(Object.entries(bundle).filter(([, amount]) => amount > 0));
}

const actions = {
  roll: () => play({ $kind: 'RollDice', playerIndex: seat() }),
  endTurn: () => play({ $kind: 'EndTurn', playerIndex: seat() }),
  buyCard: () => play({ $kind: 'BuyDevelopmentCard', playerIndex: seat() }),

  bankTrade: () =>
    play({
      $kind: 'MaritimeTrade',
      playerIndex: seat(),
      give: bankGive.value,
      take: bankTake.value,
    }).then(() => {
      bankGive.value = null;
      bankTake.value = null;
    }),

  // گیرندگان خالی یعنی «به همه»؛ موتور خودش بقیه را پر می‌کند.
  propose: () =>
    play({
      $kind: 'ProposeTrade',
      playerIndex: seat(),
      give: packed(offerGive.value),
      take: packed(offerTake.value),
      recipients: [],
    }).then(clearOffer),

  respondToTrade: (accept: boolean) =>
    play({ $kind: 'RespondToTrade', playerIndex: seat(), accept }),

  confirmTrade: (partner: number) =>
    play({ $kind: 'ConfirmTrade', playerIndex: seat(), partner }),

  cancelTrade: () => play({ $kind: 'CancelTrade', playerIndex: seat() }),

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
  ticker = window.setInterval(() => (now.value = Date.now()), 1000);
});

onBeforeUnmount(() => {
  clearInterval(tumble);
  clearInterval(ticker);
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
      <span v-if="champion" class="hx-chip hx-chip--live">
        {{ iWon ? t('game.youWon') : t('game.wonBy', champion.displayName) }}
      </span>

      <span v-if="covered" class="hx-chip hx-chip--muted">{{ t('game.botCovering') }}</span>

      <span v-else-if="countdown !== null && countdown <= 30" class="hx-chip">
        {{ t('game.deadlineIn', countdown) }}
      </span>
    </header>

    <p v-if="problem" class="hx-alert" role="alert">{{ problem }}</p>

    <section
      v-if="champion"
      class="hx-panel hx-result"
      :class="{ 'hx-result--mine': iWon }"
      role="status"
    >
      <p class="hx-result__title">
        {{ iWon ? t('game.youWon') : t('game.wonBy', champion.displayName) }}
      </p>

      <p v-if="champion.team !== null" class="hx-result__team">
        {{ t('game.teamWon', champion.team + 1) }}
      </p>

      <ol class="hx-result__standings">
        <li
          v-for="player in standings"
          :key="player.index"
          :class="{ 'is-winner': winners.some((w) => w.index === player.index) }"
        >
          <span class="hx-avatar hx-avatar--sm" :style="{ '--hx-avatar-color': player.avatarColor }">
            {{ player.displayName.slice(0, 1).toUpperCase() || '?' }}
          </span>
          <span class="hx-result__name">{{ player.displayName }}</span>
          <strong class="hx-result__points">
            {{ player.publicVictoryPoints }} {{ t('lobby.pointsShort') }}
          </strong>
        </li>
      </ol>

      <a class="hx-btn hx-btn--primary" href="/Lobby">{{ t('game.backToLobby') }}</a>
    </section>

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

    <section v-else-if="view && !isOver" class="hx-panel">
      <p class="hx-muted">{{ t('game.waitingForOthers') }}</p>
    </section>

    <!--
      معامله جدا از کنترل‌های نوبت است و این عمدی است: پاسخ دادن به یک پیشنهاد
      روی نوبتِ پیشنهاددهنده اتفاق می‌افتد، پس این بخش باید وقتی نوبتِ من نیست
      هم دیده شود.
    -->
    <section v-if="view && !isOver && (offer || (isMyTurn && phase === 'Main'))" class="hx-panel hx-trade">
      <h3 class="hx-panel__title">{{ t('game.trade') }}</h3>

      <!-- ── پیشنهادِ روی میز ─────────────────────────────────────────── -->
      <template v-if="offer">
        <p class="hx-trade__offer">
          <strong>{{ iProposed ? t('game.tradeYouOffer') : t('game.tradeOfferedBy', nameOf(offer.proposer)) }}</strong>
          {{ describeBundle(offer.give) }}
          <span class="hx-trade__arrow" aria-hidden="true">⇄</span>
          {{ describeBundle(offer.take) }}
        </p>

        <template v-if="iProposed">
          <ul class="hx-trade__answers">
            <li v-for="(response, seatIndex) in offer.responses" :key="seatIndex">
              <span>{{ nameOf(Number(seatIndex)) }}</span>
              <strong :data-answer="response">{{ t(`game.tradeAnswer.${response}`) }}</strong>
            </li>
          </ul>

          <div class="hx-live__choices">
            <button
              v-for="partner in acceptedBy"
              :key="partner.index"
              type="button"
              class="hx-btn hx-btn--sm hx-btn--primary"
              @click="actions.confirmTrade(partner.index)"
            >
              {{ t('game.tradeConfirmWith', partner.displayName) }}
            </button>

            <button type="button" class="hx-btn hx-btn--sm hx-btn--ghost" @click="actions.cancelTrade()">
              {{ t('game.tradeWithdraw') }}
            </button>
          </div>
        </template>

        <div v-else-if="myResponse === 'Pending'" class="hx-live__choices">
          <button type="button" class="hx-btn hx-btn--sm hx-btn--primary" @click="actions.respondToTrade(true)">
            {{ t('game.tradeAccept') }}
          </button>
          <button type="button" class="hx-btn hx-btn--sm hx-btn--ghost" @click="actions.respondToTrade(false)">
            {{ t('game.tradeDecline') }}
          </button>
        </div>

        <p v-else-if="myResponse" class="hx-muted hx-small">
          {{ t(`game.tradeAnswer.${myResponse}`) }}
        </p>
      </template>

      <!-- ── ساختنِ معامله؛ فقط سرِ نوبتِ خودم ────────────────────────── -->
      <template v-else-if="isMyTurn && phase === 'Main'">
        <div class="hx-trade__bank">
          <p class="hx-muted hx-small">{{ t('game.tradeBankHint') }}</p>

          <div class="hx-trade__row">
            <span class="hx-trade__label">{{ t('game.tradeGive') }}</span>
            <button
              v-for="resource in RESOURCES"
              :key="`give-${resource}`"
              type="button"
              class="hx-btn hx-btn--sm"
              :class="{ 'hx-btn--primary': bankGive === resource }"
              :disabled="(hand[resource] ?? 0) < rateOf(resource)"
              @click="bankGive = resource"
            >
              {{ rateOf(resource) }}× {{ t(`game.resource.${resource}`) }}
            </button>
          </div>

          <div class="hx-trade__row">
            <span class="hx-trade__label">{{ t('game.tradeTake') }}</span>
            <button
              v-for="resource in RESOURCES"
              :key="`take-${resource}`"
              type="button"
              class="hx-btn hx-btn--sm"
              :class="{ 'hx-btn--primary': bankTake === resource }"
              :disabled="bankGive === resource"
              @click="bankTake = resource"
            >
              {{ t(`game.resource.${resource}`) }}
            </button>
          </div>

          <button
            type="button"
            class="hx-btn hx-btn--sm hx-btn--primary"
            :disabled="!canBankTrade"
            @click="actions.bankTrade()"
          >
            {{ t('game.tradeWithBank') }}
          </button>
        </div>

        <div class="hx-trade__players">
          <p class="hx-muted hx-small">{{ t('game.tradePlayersHint') }}</p>

          <div v-for="side in (['give', 'take'] as const)" :key="side" class="hx-trade__row">
            <span class="hx-trade__label">
              {{ side === 'give' ? t('game.tradeGive') : t('game.tradeTake') }}
            </span>

            <span v-for="resource in RESOURCES" :key="`${side}-${resource}`" class="hx-live__counter">
              <button type="button" class="hx-btn hx-btn--sm" @click="adjustOffer(side, resource, -1)">
                −
              </button>
              <span>
                {{ t(`game.resource.${resource}`) }}
                {{ (side === 'give' ? offerGive : offerTake)[resource] ?? 0 }}
              </span>
              <button type="button" class="hx-btn hx-btn--sm" @click="adjustOffer(side, resource, 1)">
                +
              </button>
            </span>
          </div>

          <div class="hx-live__choices">
            <button
              type="button"
              class="hx-btn hx-btn--sm hx-btn--primary"
              :disabled="!canPropose"
              @click="actions.propose()"
            >
              {{ t('game.tradePropose') }}
            </button>
            <button type="button" class="hx-btn hx-btn--sm hx-btn--ghost" @click="clearOffer()">
              {{ t('common.cancel') }}
            </button>
          </div>
        </div>
      </template>
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
            <span v-if="player.team !== null" class="hx-chip">
              {{ t('game.team', player.team + 1) }}
            </span>
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
          <li v-if="view.players[view.seat ?? 0]?.team !== null">
            <span>{{ t('game.teamScore') }}</span>
            <strong>{{ view.hand.score }}</strong>
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
