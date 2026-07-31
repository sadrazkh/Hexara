<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { t } from '@/i18n';
import type { ChatMessage, Link } from '@/game/connection';
import {
  RoomConnection,
  type HouseRules,
  type RoomSettingsInput,
  type RoomView,
} from '@/room/connection';
import Chat from './Chat.vue';
import Voice from './Voice.vue';

const props = defineProps<{
  code: string;
  userId: string;
  boardEditUrl: string;
  chatEnabled?: boolean;
  voiceEnabled?: boolean;
}>();

/**
 * کد اتاق ممکن است همه رقم باشد (الفبایش رقم هم دارد) و ‎propsFrom‎ در ‎main.ts‎
 * رشته‌ی عددنما را به عدد تبدیل می‌کند. پس صریح رشته‌اش می‌کنیم، وگرنه اتاقی با
 * کدی مثل ‎234567‎ اصلاً وصل نمی‌شد.
 */
const code = String(props.code);
const me = String(props.userId);

const link = ref<Link>('connecting');
const room = ref<RoomView | null>(null);
const problem = ref<string | null>(null);
const saving = ref(false);

/** فرمِ تنظیمات جدا از وضعیت نگه داشته می‌شود تا تایپِ نیمه‌کاره‌ی میزبان پاک نشود. */
const form = ref<RoomSettingsInput>({
  maxPlayers: 4,
  victoryPoints: 10,
  boardRadius: 2,
  friendlyRobber: false,
  teams: false,
});

/**
 * گفت‌وگوی اتاق انتظار.
 *
 * فقط برای کسانی که صندلی دارند — سرور هم همین را رعایت می‌کند و پنل هم اصلاً
 * ساخته نمی‌شود. با شروع بازی، همین گفت‌وگو به صفحه‌ی بازی می‌رود.
 */
const chat = ref<ChatMessage[]>([]);

let connection: RoomConnection | null = null;

/** صندلی خودم در اتاق؛ تهی یعنی فقط تماشا می‌کنم. */
const mySeat = computed(() => room.value?.seats.find((s) => s.userId === me)?.seat ?? null);

/** اهالیِ اتاق، در همان دو شکلی که پنل‌های گفت‌وگو و صدا می‌خواهند. */
const chatPeople = computed(() =>
  (room.value?.seats ?? []).map((s) => ({
    seat: s.seat,
    displayName: s.displayName,
    avatarColor: s.avatarColor,
  })),
);

const voicePeople = computed(() =>
  (room.value?.seats ?? []).map((s) => ({
    userId: s.userId,
    displayName: s.displayName,
    avatarColor: s.avatarColor,
  })),
);

function sendChat(text: string): void {
  void connection?.sendChat(text);
}

function voiceTicket() {
  return connection?.voiceTicket() ?? Promise.resolve(null);
}

/** تاریخچه هر بار که اتصال زنده می‌شود از نو گرفته می‌شود، نه فقط اولِ کار. */
async function refreshChat(): Promise<void> {
  if (!props.chatEnabled) return;

  chat.value = (await connection?.chatHistory()) ?? [];
}

const isHost = computed(() => room.value?.hostId === me);
const isMember = computed(() => room.value?.seats.some((s) => s.userId === me) ?? false);
const started = computed(() => room.value?.status === 'Started');
const closed = computed(() => room.value?.status === 'Closed');

/** صندلی‌ها تا سقفِ اتاق، با جاهای خالی — تا کسی که منتظر است ببیند چند جا مانده. */
const seats = computed(() => {
  const current = room.value;
  if (!current) return [];

  const taken = new Map(current.seats.map((s) => [s.seat, s]));
  return Array.from({ length: current.maxPlayers }, (_, seat) => taken.get(seat) ?? null);
});

const canStart = computed(() => room.value?.canStart ?? false);

function fillForm(view: RoomView): void {
  form.value = {
    maxPlayers: view.maxPlayers,
    victoryPoints: view.victoryPoints,
    boardRadius: view.boardRadius,
    friendlyRobber: view.friendlyRobber,
    teams: view.teams,
    rules: { ...view.rules },
  };
}

/**
 * قواعد خانگی که در فرم دیده می‌شوند.
 *
 * برچسب و **بازه** از اینجا می‌آید تا هر قانونِ تازه یک جا اضافه شود. بازه‌ها
 * همان‌هایی هستند که سرور می‌سنجد؛ اینجا فقط راهنمای ورودی‌اند و تکیه‌گاه
 * نیستند — سرور هر عددِ بیرون از محدوده را رد می‌کند.
 */
const HOUSE_RULES = [
  { key: 'discardLimit', min: 2, max: 30 },
  { key: 'bankTradeRate', min: 2, max: 6 },
  { key: 'longestRoadMinimum', min: 2, max: 15 },
  { key: 'largestArmyMinimum', min: 1, max: 10 },
  { key: 'tradeWindowSeconds', min: 5, max: 300 },
  { key: 'roadsPerPlayer', min: 2, max: 30 },
  { key: 'settlementsPerPlayer', min: 2, max: 10 },
  { key: 'citiesPerPlayer', min: 1, max: 10 },
  { key: 'bankPerResource', min: 5, max: 60 },
  { key: 'friendlyRobberThreshold', min: 0, max: 10 },
] as const satisfies readonly { key: keyof HouseRules; min: number; max: number }[];

/** بخشِ قواعد بسته می‌ماند مگر اینکه اتاق قواعد سفارشی داشته باشد. */
const rulesOpen = ref(false);

/** برگرداندنِ همه‌چیز به بازیِ کلاسیک — یک دکمه، نه ده بار پاک‌کردن. */
function resetRules(): void {
  form.value = { ...form.value, rules: { ...CLASSIC } };
}

/**
 * قواعد کلاسیک از **اولین نمایی** که سرور می‌دهد گرفته می‌شوند نه از عددهای
 * تایپ‌شده اینجا: اتاقِ تازه همیشه کلاسیک است، پس همان یک منبعِ حقیقت می‌ماند.
 */
let CLASSIC: HouseRules = {
  discardLimit: 7,
  bankPerResource: 19,
  friendlyRobberThreshold: 2,
  longestRoadMinimum: 5,
  largestArmyMinimum: 3,
  bankTradeRate: 4,
  tradeWindowSeconds: 30,
  settlementsPerPlayer: 5,
  citiesPerPlayer: 4,
  roadsPerPlayer: 15,
};

/**
 * وقتی بازی شروع شد همه می‌روند — همین چیزی است که قبلاً نبود و هر کس باید
 * خودش صفحه را رفرش می‌کرد تا بفهمد بازی راه افتاده.
 */
watch(
  () => room.value?.gameId,
  (gameId) => {
    if (gameId && started.value) {
      window.location.assign(`/Game/Play/${gameId}`);
    }
  },
);

async function save(): Promise<void> {
  if (!connection || saving.value) return;

  saving.value = true;
  problem.value = null;

  const result = await connection.updateSettings({ ...form.value });
  saving.value = false;

  if (result && !result.success) {
    problem.value = t(`lobby.error.${result.error}`);
  }
}

async function startGame(): Promise<void> {
  if (!connection) return;

  problem.value = null;
  const result = await connection.startGame();

  if (result && !result.success) {
    problem.value = t(`lobby.error.${result.error}`);
  }
}

async function clearBoard(): Promise<void> {
  if (!connection) return;

  problem.value = null;
  const result = await connection.clearBoard();

  if (result && !result.success) {
    problem.value = t(`lobby.error.${result.error}`);
  }
}

async function takeSeat(): Promise<void> {
  if (!connection) return;

  problem.value = null;
  const result = await connection.takeSeat();

  if (result && !result.success) {
    problem.value = t(`lobby.error.${result.error}`);
  }
}

async function leave(): Promise<void> {
  if (!connection) return;

  const result = await connection.leaveRoom();

  if (result && !result.success) {
    problem.value = t(`lobby.error.${result.error}`);
    return;
  }

  window.location.assign('/Lobby');
}

function initial(name: string): string {
  return name.length > 0 ? name[0]!.toUpperCase() : '?';
}

onMounted(() => {
  connection = new RoomConnection(code, {
    onLink: (value) => {
      link.value = value;
      if (value === 'live') void refreshChat();
    },
    onRoom: (value) => {
      const first = room.value === null;
      room.value = value;

      // فرم فقط وقتی پر می‌شود که میزبان مشغول تایپ نباشد؛ بار اول و هر بار که
      // تنظیمات از جای دیگری عوض شده باشد.
      if (first || !saving.value) fillForm(value);

      // بخشِ قواعد فقط وقتی خودش باز می‌شود که اتاق واقعاً قاعده‌ی سفارشی دارد.
      if (first && value.customRules) rulesOpen.value = true;
    },
    onClosed: () => window.location.assign('/Lobby'),
    onError: (message) => (problem.value = message),
    onChat: (message) => {
      chat.value = [...chat.value, message];
    },
  });

  void connection.start();
});

onBeforeUnmount(() => {
  void connection?.stop();
  connection = null;
});
</script>

<template>
  <div class="hx-roomlive">
    <p v-if="link !== 'live'" class="hx-live__link" :data-link="link">
      {{ t(`game.link.${link}`) }}
    </p>

    <p v-if="problem" class="hx-alert" role="alert">{{ problem }}</p>

    <p v-if="closed" class="hx-alert" role="status">{{ t('lobby.closedNotice') }}</p>
    <p v-else-if="started" class="hx-chip hx-chip--live">{{ t('lobby.startingNow') }}</p>

    <div class="hx-panel">
      <h2 class="hx-panel__title">{{ t('lobby.seats') }}</h2>

      <ol class="hx-seats">
        <li
          v-for="(seat, index) in seats"
          :key="index"
          class="hx-seat"
          :class="{ 'hx-seat--empty': seat === null }"
        >
          <span class="hx-seat__number">{{ index + 1 }}</span>

          <template v-if="seat === null">
            <span class="hx-seat__name hx-muted">{{ t('lobby.emptySeat') }}</span>
          </template>
          <template v-else>
            <span
              class="hx-avatar hx-avatar--sm"
              :style="{ '--hx-avatar-color': seat.avatarColor }"
            >
              {{ initial(seat.displayName) }}
            </span>
            <span class="hx-seat__name">{{ seat.displayName }}</span>
            <span v-if="seat.isHost" class="hx-chip">{{ t('lobby.host') }}</span>
            <span v-if="seat.isGuest" class="hx-chip hx-chip--muted">{{ t('lobby.guest') }}</span>
          </template>
        </li>
      </ol>
    </div>

    <div v-if="room" class="hx-panel">
      <h2 class="hx-panel__title">{{ t('lobby.settings') }}</h2>

      <template v-if="isHost">
        <div class="hx-form">
          <div class="hx-form__row">
            <label class="hx-field">
              <span class="hx-field__label">{{ t('lobby.maxPlayers') }}</span>
              <select v-model.number="form.maxPlayers" class="hx-input">
                <option v-for="n in [2, 3, 4, 5, 6]" :key="n" :value="n">{{ n }}</option>
              </select>
            </label>

            <label class="hx-field">
              <span class="hx-field__label">{{ t('lobby.victoryPoints') }}</span>
              <input
                v-model.number="form.victoryPoints"
                class="hx-input"
                type="number"
                min="3"
                max="20"
              />
            </label>

            <label class="hx-field">
              <span class="hx-field__label">{{ t('lobby.boardRadius') }}</span>
              <select v-model.number="form.boardRadius" class="hx-input">
                <option :value="2">{{ t('lobby.boardClassic') }}</option>
                <option :value="3">{{ t('lobby.boardLarge') }}</option>
                <option :value="4">{{ t('lobby.boardHuge') }}</option>
              </select>
            </label>
          </div>

          <label class="hx-check">
            <input v-model="form.friendlyRobber" type="checkbox" />
            <span>{{ t('lobby.friendlyRobber') }}</span>
          </label>

          <label class="hx-check">
            <input v-model="form.teams" type="checkbox" />
            <span>{{ t('lobby.teams') }}</span>
          </label>

          <button type="button" class="hx-btn hx-btn--ghost" :disabled="saving" @click="save()">
            {{ t('common.save') }}
          </button>
        </div>

        <!--
          قواعد خانگی بسته می‌مانند مگر اتاق واقعاً سفارشی باشد.

          ده عددِ باز، پنلِ تنظیمات را از کار می‌اندازد؛ کسی که کلاسیک بازی
          می‌کند — یعنی تقریباً همه — نباید با آن‌ها روبه‌رو شود.
        -->
        <details class="hx-rules-form" :open="rulesOpen">
          <summary>
            {{ t('lobby.houseRules') }}
            <span v-if="room.customRules" class="hx-chip hx-chip--award">
              {{ t('lobby.customRules') }}
            </span>
          </summary>

          <p class="hx-muted hx-small">{{ t('lobby.houseRulesHint') }}</p>

          <div class="hx-rules-form__grid">
            <label v-for="rule in HOUSE_RULES" :key="rule.key" class="hx-field">
              <span>{{ t(`lobby.rule.${rule.key}`) }}</span>
              <input
                v-model.number="form.rules![rule.key]"
                class="hx-input"
                type="number"
                :min="rule.min"
                :max="rule.max"
              />
            </label>
          </div>

          <div class="hx-rules-form__actions">
            <button type="button" class="hx-btn hx-btn--ghost hx-btn--sm" @click="resetRules()">
              {{ t('lobby.resetRules') }}
            </button>
            <button
              type="button"
              class="hx-btn hx-btn--sm"
              :disabled="saving"
              @click="save()"
            >
              {{ t('common.save') }}
            </button>
          </div>
        </details>

        <div class="hx-room-page__actions">
          <a class="hx-btn hx-btn--outline hx-btn--sm" :href="boardEditUrl">
            {{ t('board.edit') }}
          </a>
          <span class="hx-chip">
            {{ room.boardCode ? t('board.custom') : t('board.randomBoard') }}
          </span>
          <button
            v-if="room.boardCode"
            type="button"
            class="hx-btn hx-btn--ghost hx-btn--sm"
            @click="clearBoard()"
          >
            {{ t('board.clear') }}
          </button>
        </div>
      </template>

      <ul v-else class="hx-facts">
        <li>
          <span>{{ t('lobby.maxPlayers') }}</span><strong>{{ room.maxPlayers }}</strong>
        </li>
        <li>
          <span>{{ t('lobby.victoryPoints') }}</span><strong>{{ room.victoryPoints }}</strong>
        </li>
        <li>
          <span>{{ t('lobby.friendlyRobber') }}</span>
          <strong>{{ room.friendlyRobber ? t('common.yes') : t('common.no') }}</strong>
        </li>
        <li>
          <span>{{ t('lobby.teams') }}</span>
          <strong>{{ room.teams ? t('common.yes') : t('common.no') }}</strong>
        </li>

        <!-- بقیه هم باید بدانند با چه قواعدی بازی می‌کنند، حتی اگر نتوانند عوضش کنند. -->
        <template v-if="room.customRules">
          <li v-for="rule in HOUSE_RULES" :key="rule.key">
            <span>{{ t(`lobby.rule.${rule.key}`) }}</span>
            <strong>{{ room.rules[rule.key] }}</strong>
          </li>
        </template>
      </ul>
    </div>

    <div class="hx-room-page__actions">
      <button
        v-if="isMember && !started"
        type="button"
        class="hx-btn hx-btn--ghost"
        @click="leave()"
      >
        {{ t('lobby.leave') }}
      </button>

      <button
        v-else-if="!isMember && !started && !closed"
        type="button"
        class="hx-btn hx-btn--outline"
        @click="takeSeat()"
      >
        {{ t('lobby.joinButton') }}
      </button>

      <template v-if="isHost && !started">
        <button
          type="button"
          class="hx-btn hx-btn--primary hx-btn--lg"
          :disabled="!canStart"
          @click="startGame()"
        >
          {{ t('lobby.start') }}
        </button>

        <p v-if="!canStart" class="hx-muted hx-small">{{ t('lobby.needTwoPlayers') }}</p>
      </template>
    </div>

    <!--
      صدا و گفت‌وگو فقط برای کسانی که صندلی دارند.

      اتاق را هر کسی که کد را دارد تماشا می‌کند، پس شرطِ ‎mySeat‎ لازم است — و
      سرور هم مستقل از این، همان قاعده را می‌سنجد. اینجا فقط چیزی نشان داده
      نمی‌شود که کار نکند.
    -->
    <div v-if="voiceEnabled && room && !started && mySeat !== null" class="hx-panel">
      <h2 class="hx-panel__title">{{ t('game.voice') }}</h2>
      <Voice :people="voicePeople" :ticket="voiceTicket" />
    </div>

    <div v-if="chatEnabled && room && !started && mySeat !== null" class="hx-panel">
      <h2 class="hx-panel__title">{{ t('game.chat') }}</h2>
      <Chat
        :messages="chat"
        :people="chatPeople"
        :seat="mySeat"
        :live="link === 'live'"
        @send="sendChat($event)"
      />
      <p class="hx-muted hx-small">{{ t('lobby.chatFollows') }}</p>
    </div>
  </div>
</template>
