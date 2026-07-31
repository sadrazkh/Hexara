<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from 'vue';
import { t } from '@/i18n';
import type { Track } from 'livekit-client';
import { VoiceSession, type VoiceParticipant, type VoiceState } from '@/voice/session';

/** همان کمترین چیزی که برای نامِ کنارِ صدا لازم است. */
export interface VoicePerson {
  userId: string;
  displayName: string;
  avatarColor: string;
}

/**
 * پنل صدا و تصویر.
 *
 * **بازی هرگز منتظر این نمی‌ماند.** پیوستن دستی است نه خودکار — کسی که وارد بازی
 * می‌شود نباید ناگهان میکروفونش باز شود — و هر خرابی (اجازه‌ی ردشده، سرورِ
 * خاموش، میکروفونِ نبوده) فقط همین پنل را به حالت «نشد» می‌برد.
 *
 * نامِ کنارِ هر نفر از فهرست بازیکن‌های خودِ بازی درمی‌آید و با شناسه‌ی کاربر به
 * آن وصل می‌شود، نه با نامی که LiveKit می‌دهد؛ همان قاعده‌ای که در چت هم هست.
 */
const props = defineProps<{
  people: VoicePerson[];
  /** بلیت را می‌گیرد؛ تهی یعنی نمی‌شود پیوست. */
  ticket: () => Promise<{ url: string; token: string; room: string } | null>;
}>();

const state = ref<VoiceState>('off');
const people = ref<VoiceParticipant[]>([]);

/** تصویرِ هر نفر. ‎shallowRef‎ چون این‌ها شیءهای SDK هستند نه داده‌ی ساده. */
const videos = shallowRef(new Map<string, Track>());

const micOn = ref(true);
const camOn = ref(false);

const session = new VoiceSession({
  onState: (value) => (state.value = value),
  onParticipants: (list) => {
    people.value = list;

    // وضعیتِ دکمه‌ها از خودِ اتاق خوانده می‌شود نه از آنچه خواسته بودیم؛ اگر
    // مرورگر اجازه‌ی دوربین را رد کند، دکمه نباید روشن بماند.
    const me = list.find((who) => who.local);
    if (me) {
      micOn.value = me.micOn;
      camOn.value = me.camOn;
    }
  },
  onVideo: (identity, track) => {
    const next = new Map(videos.value);
    if (track) next.set(identity, track);
    else next.delete(identity);

    videos.value = next;
  },
});

const joined = computed(() => state.value === 'on');
const busy = computed(() => state.value === 'joining');

/** نامِ نمایشی از روی فهرست بازیکن‌ها؛ شناسه همان ‎userId‎ است. */
function nameOf(participant: VoiceParticipant): string {
  const person = props.people.find((p) => p.userId === participant.identity);
  return person?.displayName || participant.name || t('game.unknownPlayer');
}

function colorOf(participant: VoiceParticipant): string {
  return props.people.find((p) => p.userId === participant.identity)?.avatarColor ?? 'var(--hx-accent)';
}

async function join(): Promise<void> {
  const ticket = await props.ticket();
  if (!ticket) {
    state.value = 'failed';
    return;
  }

  await session.join(ticket);
}

/**
 * عنصرِ ویدیو که ساخته شد، جریانش را به آن می‌چسبانیم.
 *
 * با ‎:ref‎ انجام می‌شود نه در ‎onMounted‎، چون تصویرها می‌آیند و می‌روند و هر بار
 * یک عنصرِ تازه ساخته می‌شود.
 */
function bind(el: Element | null, identity: string): void {
  const track = videos.value.get(identity);
  if (el instanceof HTMLVideoElement && track) track.attach(el);
}

// خروج از صفحه یعنی خروج از اتاق؛ وگرنه میکروفون باز می‌ماند.
onBeforeUnmount(() => void session.leave());

watch(micOn, (on) => void (joined.value && session.setMic(on)));
watch(camOn, (on) => void (joined.value && session.setCamera(on)));
</script>

<template>
  <div class="hx-voice">
    <p v-if="state === 'failed'" class="hx-muted hx-small">{{ t('game.voiceFailed') }}</p>

    <div class="hx-voice__controls">
      <button
        v-if="!joined"
        type="button"
        class="hx-btn hx-btn--sm hx-btn--primary"
        :disabled="busy"
        @click="join()"
      >
        {{ busy ? t('game.voiceJoining') : t('game.voiceJoin') }}
      </button>

      <template v-else>
        <button
          type="button"
          class="hx-btn hx-btn--sm"
          :class="{ 'hx-btn--outline': !micOn }"
          :aria-pressed="micOn"
          @click="micOn = !micOn"
        >
          {{ micOn ? t('game.voiceMicOn') : t('game.voiceMicOff') }}
        </button>

        <button
          type="button"
          class="hx-btn hx-btn--sm"
          :class="{ 'hx-btn--outline': !camOn }"
          :aria-pressed="camOn"
          @click="camOn = !camOn"
        >
          {{ camOn ? t('game.voiceCamOn') : t('game.voiceCamOff') }}
        </button>

        <button type="button" class="hx-btn hx-btn--sm hx-btn--ghost" @click="session.leave()">
          {{ t('game.voiceLeave') }}
        </button>
      </template>
    </div>

    <ul v-if="joined" class="hx-voice__people">
      <li
        v-for="who in people"
        :key="who.identity"
        class="hx-voice__person"
        :class="{ 'is-speaking': who.speaking, 'is-muted': !who.micOn }"
        :style="{ '--hx-avatar-color': colorOf(who) }"
      >
        <video
          v-if="videos.has(who.identity)"
          class="hx-voice__video"
          autoplay
          playsinline
          muted
          :ref="(el) => bind(el as Element | null, who.identity)"
        ></video>

        <span class="hx-voice__name">{{ nameOf(who) }}</span>
        <span class="hx-voice__mic" :aria-label="who.micOn ? t('game.voiceMicOn') : t('game.voiceMicOff')">
          {{ who.micOn ? '🔊' : '🔇' }}
        </span>
      </li>
    </ul>

    <p v-else-if="state === 'off'" class="hx-muted hx-small">{{ t('game.voiceHint') }}</p>
  </div>
</template>
