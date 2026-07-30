<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { t } from '@/i18n';
import type { Link } from '@/game/connection';
import { RoomConnection, type RoomSettingsInput, type RoomView } from '@/room/connection';

const props = defineProps<{
  code: string;
  userId: string;
  boardEditUrl: string;
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

let connection: RoomConnection | null = null;

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
  };
}

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
    onLink: (value) => (link.value = value),
    onRoom: (value) => {
      const first = room.value === null;
      room.value = value;

      // فرم فقط وقتی پر می‌شود که میزبان مشغول تایپ نباشد؛ بار اول و هر بار که
      // تنظیمات از جای دیگری عوض شده باشد.
      if (first || !saving.value) fillForm(value);
    },
    onClosed: () => window.location.assign('/Lobby'),
    onError: (message) => (problem.value = message),
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
  </div>
</template>
