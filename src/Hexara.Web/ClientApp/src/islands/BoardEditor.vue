<script setup lang="ts">
import { computed, ref } from 'vue';
import { t } from '@/i18n';
import GameBoard from './GameBoard.vue';
import type { BoardData, Pick } from '@/three/board';
import type { Axial } from '@/three/hex';

interface Tile {
  q: number;
  r: number;
  terrain: string;
  number: number | null;
}

interface Port {
  q: number;
  r: number;
  side: number;
  resource: string | null;
}

interface Draft {
  radius: number;
  tiles: Tile[];
  ports: Port[];
}

// ‎main.ts‎ هر ‎data-*‎ را به prop تبدیل می‌کند و مقدارهای JSON را همان‌جا باز می‌کند.
const props = defineProps<{
  roomId: string;
  roomCode: string;
  draft: Draft;
  code: string;
  saved: boolean;
}>();

/**
 * قالبِ کد فقط سمت سرور پیاده شده. ویرایشگر آرایه‌ی خانه‌ها را دستکاری می‌کند و
 * برای ساختن، خواندن و ذخیره‌ی کد به سرور می‌آید — یک پیاده‌سازی یعنی دو طرف
 * هرگز از هم جدا نمی‌افتند.
 */
const draft = ref<Draft>(props.draft);
const code = ref(props.code);
const saved = ref(props.saved);
const selected = ref<Axial | null>(null);
const problem = ref<string | null>(null);
const busy = ref(false);
const seed = ref('');
const pasted = ref('');

/** زمین‌ها به ترتیبی که در پالت دیده می‌شوند. آیکون همراه رنگ می‌آید — نه به‌جایش. */
const TERRAINS = [
  { key: 'Forest', icon: 'M12 3l5 8h-3l4 7H6l4-7H7z' },
  { key: 'Hills', icon: 'M3 18l5-7 4 5 3-4 6 6z' },
  { key: 'Pasture', icon: 'M4 17c3-6 13-6 16 0M8 11V7M16 11V7' },
  { key: 'Fields', icon: 'M6 20V9M12 20V6M18 20v-8M4 20h16' },
  { key: 'Mountains', icon: 'M2 19l6-11 4 6 3-4 7 9z' },
  { key: 'Desert', icon: 'M3 17h18M7 17c0-4 2-6 5-6s5 2 5 6' },
] as const;

const PORTS = ['generic', 'Lumber', 'Brick', 'Wool', 'Grain', 'Ore'] as const;

/** اعدادی که می‌شود روی یک خانه گذاشت — ۷ سهم دزد است و در فهرست نیست. */
const NUMBERS = [2, 3, 4, 5, 6, 8, 9, 10, 11, 12];

const board = computed<BoardData>(() => ({
  tiles: draft.value.tiles,
  ports: draft.value.ports,
  buildings: [],
  roads: [],
  // دزد روی بیابان می‌نشیند تا پیش‌نمایش همان چیزی باشد که بازی نشان می‌دهد.
  robber: draft.value.tiles.find((tile) => tile.terrain === 'Desert') ?? { q: 0, r: 0 },
}));

const options = computed(() => ({ editable: true, selected: selected.value }));

const current = computed(() =>
  selected.value
    ? (draft.value.tiles.find((t) => t.q === selected.value!.q && t.r === selected.value!.r) ?? null)
    : null,
);

/** شمارش زمین‌ها — برای اینکه بشود دید چیدمان چقدر از حالت کلاسیک دور شده. */
const counts = computed(() => {
  const tally: Record<string, number> = {};
  for (const tile of draft.value.tiles) {
    tally[tile.terrain] = (tally[tile.terrain] ?? 0) + 1;
  }
  return tally;
});

/** ۶ و ۸ کنار هم بازی را نامتوازن می‌کند؛ هشدار می‌دهیم ولی جلویش را نمی‌گیریم. */
const clashes = computed(() => {
  const directions = [
    [1, 0],
    [1, -1],
    [0, -1],
    [-1, 0],
    [-1, 1],
    [0, 1],
  ];

  const hot = new Set(
    draft.value.tiles.filter((t) => t.number === 6 || t.number === 8).map((t) => `${t.q},${t.r}`),
  );

  let found = 0;
  for (const key of hot) {
    const [q, r] = key.split(',').map(Number) as [number, number];
    for (const [dq, dr] of directions) {
      if (hot.has(`${q + dq!},${r + dr!}`)) found++;
    }
  }

  return found / 2;
});

function token(): string {
  return (
    document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]')?.value ?? ''
  );
}

async function post<T>(url: string, body: unknown): Promise<T | null> {
  problem.value = null;
  busy.value = true;

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', RequestVerificationToken: token() },
      body: JSON.stringify(body),
    });

    const payload = (await response.json()) as T & { error?: string };

    if (!response.ok) {
      problem.value = payload.error ?? t('common.somethingWrong');
      return null;
    }

    return payload;
  } catch {
    problem.value = t('common.somethingWrong');
    return null;
  } finally {
    busy.value = false;
  }
}

function accept(result: { draft: Draft; code: string } | null, persisted = false): void {
  if (!result) return;

  draft.value = result.draft;
  code.value = result.code;
  saved.value = persisted;
  selected.value = null;
}

async function randomize(): Promise<void> {
  accept(
    await post<{ draft: Draft; code: string }>('/Board/Random', {
      radius: draft.value.radius,
      seed: seed.value.trim() || null,
    }),
  );
}

async function read(): Promise<void> {
  const result = await post<{ draft: Draft; code: string }>('/Board/Read', { code: pasted.value });
  if (result) {
    pasted.value = '';
    accept(result);
  }
}

async function save(): Promise<void> {
  accept(
    await post<{ draft: Draft; code: string }>('/Board/Save', {
      roomId: props.roomId,
      draft: draft.value,
    }),
    true,
  );
}

async function copy(): Promise<void> {
  try {
    await navigator.clipboard.writeText(code.value);
  } catch {
    // بدون دسترسی به کلیپ‌بورد، کد در همان کادر انتخاب‌شدنی است.
  }
}

function onPick(pick: Pick): void {
  if (pick.kind === 'hex') {
    selected.value = pick.id;
    return;
  }

  // بندر با هر کلیک به نوع بعدی می‌رود؛ جایش ثابت می‌ماند تا حتماً ساحلی بماند.
  if (pick.kind === 'port') {
    const ports = [...draft.value.ports];
    const port = ports[pick.index];
    if (!port) return;

    const at = PORTS.indexOf((port.resource ?? 'generic') as (typeof PORTS)[number]);
    const next = PORTS[(at + 1) % PORTS.length]!;

    ports[pick.index] = { ...port, resource: next === 'generic' ? null : next };
    draft.value = { ...draft.value, ports };
    touch();
  }
}

function setTerrain(terrain: string): void {
  const tile = current.value;
  if (!tile) return;

  const tiles = draft.value.tiles.map((t) =>
    t.q === tile.q && t.r === tile.r
      ? // بیابان عدد ندارد و بقیه باید داشته باشند — سرور هم همین را می‌خواهد.
        { ...t, terrain, number: terrain === 'Desert' ? null : (t.number ?? 6) }
      : t,
  );

  draft.value = { ...draft.value, tiles };
  touch();
}

function setNumber(value: number): void {
  const tile = current.value;
  if (!tile || tile.terrain === 'Desert') return;

  const tiles = draft.value.tiles.map((t) =>
    t.q === tile.q && t.r === tile.r ? { ...t, number: value } : t,
  );

  draft.value = { ...draft.value, tiles };
  touch();
}

/** هر ویرایشی کدِ ذخیره‌شده را کهنه می‌کند تا دکمه‌ی ذخیره دوباره معنا پیدا کند. */
function touch(): void {
  saved.value = false;
  code.value = '';
}
</script>

<template>
  <div class="hx-editor">
    <p v-if="problem" class="hx-alert" role="alert">{{ problem }}</p>

    <GameBoard :board="board" :highlights="{ vertices: [], edges: [], hexes: [] }" :options="options" @pick="onPick">
      <template #fallback>{{ t('game.noWebgl') }}</template>
    </GameBoard>

    <p class="hx-muted hx-small">{{ t('board.hint') }}</p>

    <section class="hx-panel">
      <h2 class="hx-panel__title">
        {{ current ? t('board.selected', current.q, current.r) : t('board.nothingSelected') }}
      </h2>

      <div v-if="current" class="hx-editor__palette">
        <button
          v-for="terrain in TERRAINS"
          :key="terrain.key"
          type="button"
          class="hx-btn hx-btn--sm hx-editor__terrain"
          :class="{ 'hx-editor__terrain--on': current.terrain === terrain.key }"
          :aria-pressed="current.terrain === terrain.key"
          @click="setTerrain(terrain.key)"
        >
          <span class="hx-swatch" :style="{ '--hx-swatch': `var(--hx-res-${terrain.key.toLowerCase()})` }" />
          <svg class="hx-btn__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor"
               stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path :d="terrain.icon" />
          </svg>
          <span>{{ t(`board.terrain.${terrain.key}`) }}</span>
        </button>
      </div>

      <div v-if="current && current.terrain !== 'Desert'" class="hx-editor__numbers">
        <span class="hx-field__label">{{ t('board.number') }}</span>
        <button
          v-for="value in NUMBERS"
          :key="value"
          type="button"
          class="hx-btn hx-btn--sm"
          :class="{ 'hx-editor__terrain--on': current.number === value }"
          :aria-pressed="current.number === value"
          @click="setNumber(value)"
        >
          {{ value }}
        </button>
      </div>
    </section>

    <section class="hx-panel">
      <h2 class="hx-panel__title">{{ t('board.mix') }}</h2>
      <ul class="hx-facts">
        <li v-for="terrain in TERRAINS" :key="terrain.key">
          <span>{{ t(`board.terrain.${terrain.key}`) }}</span>
          <strong>{{ counts[terrain.key] ?? 0 }}</strong>
        </li>
      </ul>
      <p v-if="clashes > 0" class="hx-muted hx-small">{{ t('board.hotClash', clashes) }}</p>
    </section>

    <section class="hx-panel">
      <h2 class="hx-panel__title">{{ t('board.random') }}</h2>
      <div class="hx-form__row">
        <label class="hx-field">
          <span class="hx-field__label">{{ t('board.seed') }}</span>
          <input v-model="seed" class="hx-input" dir="ltr" :placeholder="t('board.seedPlaceholder')" />
        </label>
      </div>
      <button type="button" class="hx-btn" :disabled="busy" @click="randomize()">
        {{ t('board.shuffle') }}
      </button>
    </section>

    <section class="hx-panel">
      <h2 class="hx-panel__title">{{ t('board.share') }}</h2>

      <label class="hx-field">
        <span class="hx-field__label">{{ t('board.code') }}</span>
        <textarea class="hx-input hx-editor__code" rows="3" readonly dir="ltr"
                  :value="code || t('board.saveFirst')"></textarea>
      </label>

      <div class="hx-editor__actions">
        <button type="button" class="hx-btn hx-btn--sm" :disabled="!code" @click="copy()">
          {{ t('board.copy') }}
        </button>
      </div>

      <label class="hx-field">
        <span class="hx-field__label">{{ t('board.paste') }}</span>
        <textarea v-model="pasted" class="hx-input hx-editor__code" rows="3" dir="ltr"
                  :placeholder="'H1~2~…'"></textarea>
      </label>

      <button type="button" class="hx-btn" :disabled="busy || !pasted.trim()" @click="read()">
        {{ t('board.load') }}
      </button>
    </section>

    <div class="hx-editor__actions">
      <button type="button" class="hx-btn hx-btn--primary hx-btn--lg" :disabled="busy || saved" @click="save()">
        {{ saved ? t('board.savedAlready') : t('board.save') }}
      </button>
      <a class="hx-btn hx-btn--ghost" :href="`/Lobby/Room/${props.roomCode}`">{{ t('common.back') }}</a>
    </div>
  </div>
</template>
