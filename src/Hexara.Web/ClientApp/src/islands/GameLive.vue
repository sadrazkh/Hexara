<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { t } from '@/i18n';
import GameBoard from './GameBoard.vue';
import Fold from './Fold.vue';
import BuildPanel from './BuildPanel.vue';
import Hand from './Hand.vue';
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

/**
 * روی صفحه‌ی پهن، ستون کنارِ برد جا دارد و همه‌ی پنل‌ها باز می‌مانند؛ روی باریک
 * آکاردئون می‌شوند تا برد بالای صفحه بچسبد و هر دو دیده شوند.
 *
 * نقطه‌ی شکست با همان ‎@media‎ در ‎app.css‎ یکی است. عمداً از JavaScript خوانده
 * می‌شود چون ‎<details>‎ را نمی‌شود با CSS باز نگه داشت.
 */
const WIDE = '(min-width: 1024px)';
const wide = ref(false);

/** کدام تاشوها روی موبایل بازند. دستِ خودت و نوبتت از اول باز. */
const folds = ref<Record<string, boolean>>({
  turn: true,
  build: false,
  trade: true,
  hand: true,
  players: false,
  log: false,
});

/**
 * روی موبایل، ریل یک برگه‌ی پایین‌کش است و این می‌گوید بازست یا نه.
 *
 * خودِ ریل *یک بار* رندر می‌شود و با CSS جابه‌جا می‌شود؛ دو نسخه‌ی جدا برای
 * دسکتاپ و موبایل یعنی هر پنلِ تازه باید دو جا اضافه شود و یک روز یکی‌شان
 * فراموش می‌شود.
 */
const sheetOpen = ref(false);

/**
 * کدام دکمه‌ی نوارِ پایین روشن است.
 *
 * جدا از ‎folds‎ نگه داشته می‌شود و این عمدی است: چند پنل از اول بازند، پس اگر
 * روشنیِ دکمه را از ‎folds‎ می‌خواندیم چهار دکمه هم‌زمان روشن می‌شدند و دیگر
 * معلوم نبود کجا هستی.
 */
const activeTab = ref<string | null>(null);

/** دکمه‌های نوار پایینِ موبایل. ترتیب از روی این‌که چقدر بهشان سر می‌زنی. */
const TABS = [
  { key: 'turn', label: 'game.yourTurn' },
  { key: 'build', label: 'game.build' },
  { key: 'trade', label: 'game.trade' },
  { key: 'hand', label: 'game.yourHand' },
  { key: 'players', label: 'game.players' },
] as const;

function closeSheet(): void {
  sheetOpen.value = false;
  activeTab.value = null;
}

/** یک برگه را باز می‌کند و همان پنل را هم باز می‌کند و می‌آوردش جلوی چشم. */
function openPanel(key: string): void {
  if (sheetOpen.value && activeTab.value === key) {
    closeSheet();
    return;
  }

  folds.value = { ...folds.value, [key]: true };
  activeTab.value = key;
  sheetOpen.value = true;

  requestAnimationFrame(() => {
    document.getElementById(`hx-panel-${key}`)?.scrollIntoView({ block: 'start' });
  });
}

let wideQuery: MediaQueryList | null = null;

function onWideChange(event: MediaQueryListEvent | MediaQueryList): void {
  wide.value = event.matches;
}

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

/**
 * کلیک روی کارت یکی اضافه می‌کند و از سقف که رد شد به صفر برمی‌گردد.
 *
 * چرخه‌ای است و نه دو دکمه‌ی جدا، چون روی موبایل کنارِ هر کارت جا برای دکمه‌ی
 * منفی نبود. متنِ راهنما همین را می‌گوید تا کسی دنبال دکمه نگردد.
 */
function cycleOffer(side: 'give' | 'take', resource: string): void {
  const target = side === 'give' ? offerGive : offerTake;
  const ceiling = side === 'give' ? (hand.value[resource] ?? 0) : 4;
  const now = target.value[resource] ?? 0;

  target.value = { ...target.value, [resource]: now >= ceiling ? 0 : now + 1 };
}

/** سمتِ «می‌خواهم» از دستِ خودت نمی‌آید، پس هر پنج منبع نشان داده می‌شود. */
const WANTABLE = Object.fromEntries(RESOURCES.map((r) => [r, 1]));

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

  wideQuery = window.matchMedia(WIDE);
  onWideChange(wideQuery);
  wideQuery.addEventListener('change', onWideChange);
});

onBeforeUnmount(() => {
  clearInterval(tumble);
  clearInterval(ticker);
  wideQuery?.removeEventListener('change', onWideChange);
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

    <div class="hx-live__stage">
      <div class="hx-live__board">
        <GameBoard
          v-if="view"
          class="hx-board--fill"
          :board="board"
          :highlights="highlights"
          @pick="onPick"
        >
          <template #fallback>{{ t('game.noWebgl') }}</template>
        </GameBoard>

        <!--
          روکشِ سبک روی برد. عمداً کوچک و گوشه‌ای است: چیزی که همیشه لازم داری
          (نوبتِ که؟ چقدر وقت؟) نباید تو را از نقشه بکَند، ولی نباید نقشه را هم
          بپوشاند. کلیک از رویش رد می‌شود تا جلوی انتخابِ خانه را نگیرد.
        -->
        <div v-if="view && !isOver" class="hx-live__overlay">
          <span
            class="hx-avatar hx-avatar--sm"
            :style="{ '--hx-avatar-color': waitingOn?.avatarColor }"
          >
            {{ (waitingOn?.displayName || '?').slice(0, 1).toUpperCase() }}
          </span>

          <span class="hx-live__overlay-text">
            <strong>{{ isMyTurn ? t('game.yourTurn') : waitingOn?.displayName }}</strong>
            <span class="hx-live__overlay-phase">{{ phaseLabel }}</span>
          </span>

          <span v-if="countdown !== null && countdown <= 60" class="hx-live__overlay-clock">
            {{ countdown }}
          </span>
        </div>
      </div>

      <aside class="hx-live__rail" :class="{ 'is-open': sheetOpen }">
        <Fold
          v-if="view && isMyTurn"
          :label="t('game.yourTurn')"
          :always="wide"
          :id="`hx-panel-turn`"
          :open="folds.turn"
          @update:open="folds.turn = $event"
        >

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
        </Fold>

        <p v-else-if="view && !isOver" class="hx-panel hx-panel--rail hx-muted">
          {{ t('game.waitingForOthers') }}
        </p>

        <Fold
          v-if="view && !isOver"
          id="hx-panel-build"
          :label="t('game.build')"
          :always="wide"
          :open="folds.build"
          @update:open="folds.build = $event"
        >
          <BuildPanel
            :view="view"
            :can-act="isMyTurn && phase === 'Main'"
            @buy-card="actions.buyCard()"
          />
        </Fold>

    <!--
      معامله جدا از کنترل‌های نوبت است و این عمدی است: پاسخ دادن به یک پیشنهاد
      روی نوبتِ پیشنهاددهنده اتفاق می‌افتد، پس این بخش باید وقتی نوبتِ من نیست
      هم دیده شود.
    -->
        <Fold
          v-if="view && !isOver && (offer || (isMyTurn && phase === 'Main'))"
          class="hx-trade"
          :label="t('game.trade')"
          :always="wide"
          :id="`hx-panel-trade`"
          :open="folds.trade"
          @update:open="folds.trade = $event"
        >

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

          <p class="hx-muted hx-small">{{ t('game.tradePickHint') }}</p>

          <div class="hx-trade__side">
            <span class="hx-trade__label">{{ t('game.tradeGive') }}</span>
            <Hand
              :resources="hand"
              :selection="offerGive"
              @pick="cycleOffer('give', $event)"
            />
          </div>

          <div class="hx-trade__side">
            <span class="hx-trade__label">{{ t('game.tradeTake') }}</span>
            <Hand
              :resources="WANTABLE"
              :selection="offerTake"
              @pick="cycleOffer('take', $event)"
            />
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
        </Fold>

        <Fold
          v-if="view && view.hand"
          :label="t('game.yourHand')"
          :always="wide"
          :id="`hx-panel-hand`"
          :open="folds.hand"
          @update:open="folds.hand = $event"
        >
          <Hand
            :resources="view.hand.resources"
            :development="{ ...view.hand.developmentCards, ...view.hand.newDevelopmentCards }"
          />

          <ul class="hx-facts hx-hand__facts">
            <li>
              <span>{{ t('game.victoryPoints') }}</span>
              <strong>{{ view.hand.victoryPoints }}</strong>
            </li>
            <li v-if="view.players[view.seat ?? 0]?.team !== null">
              <span>{{ t('game.teamScore') }}</span>
              <strong>{{ view.hand.score }}</strong>
            </li>
          </ul>
        </Fold>

        <Fold
          v-if="view"
          :label="t('game.players')"
          :always="wide"
          :id="`hx-panel-players`"
          :open="folds.players"
          @update:open="folds.players = $event"
        >
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
        </Fold>

        <Fold
          v-if="log.length > 0"
          :label="t('game.log')"
          :always="wide"
          :id="`hx-panel-log`"
          :open="folds.log"
          @update:open="folds.log = $event"
        >
          <ul class="hx-live__log">
            <li v-for="(line, index) in log" :key="index">{{ line }}</li>
          </ul>
        </Fold>
      </aside>
    </div>

    <!--
      نوار پایینِ موبایل. روی صفحه‌ی پهن اصلاً رندر نمی‌شود، چون آن‌جا ریل
      همیشه کنارِ برد باز است و یک نوارِ اضافه فقط جا می‌گیرد.

      دکمه‌ها پایین‌اند تا با شست برسند؛ مهم‌ترین کارها (نوبت و ساخت) اول.
    -->
    <template v-if="!wide && view && !isOver">
      <div v-if="sheetOpen" class="hx-live__scrim" @click="closeSheet()"></div>

      <nav class="hx-live__tabs" :aria-label="t('game.title')">
        <button
          v-for="tab in TABS"
          :key="tab.key"
          type="button"
          class="hx-live__tab"
          :class="{ 'is-active': sheetOpen && activeTab === tab.key }"
          :aria-expanded="sheetOpen && activeTab === tab.key"
          @click="openPanel(tab.key)"
        >
          {{ t(tab.label) }}
        </button>
      </nav>
    </template>
  </div>
</template>
