<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue';
import { t } from '@/i18n';
import type { ChatMessage } from '@/game/connection';

/**
 * کمترین چیزی که برای نشان‌دادنِ فرستنده لازم است.
 *
 * عمداً نه ‎PlayerView‎: همین پنل در اتاق انتظار هم کار می‌کند و آن‌جا آدم‌ها
 * صندلیِ اتاق دارند نه صندلیِ بازی.
 */
export interface ChatPerson {
  seat: number;
  displayName: string;
  avatarColor: string;
}

/**
 * چتِ داخل بازی.
 *
 * سه تصمیم که شکلش را ساخته‌اند:
 *
 * ۱. **متن، نه HTML.** پیام‌ها با درج‌گرِ متنیِ Vue کشیده می‌شوند و هیچ‌جا
 *    ‎v-html‎ نیست؛ پس یک بازیکن نمی‌تواند به صفحه‌ی بقیه نشانه‌گذاری تزریق کند.
 *
 * ۲. **نام از روی صندلی.** سرور فقط شماره‌ی صندلی می‌فرستد و نام و رنگ از همان
 *    فهرست بازیکن‌هایی می‌آید که نمای بازی دارد — یعنی جعلِ نام ممکن نیست.
 *
 * ۳. **خرابی‌اش بازی را نمی‌خواباند.** فرستادن هیچ خطایی بالا نمی‌دهد و اگر
 *    نرسد، فقط نرسیده است.
 */
const props = defineProps<{
  messages: ChatMessage[];
  people: ChatPerson[];
  seat: number | null;
  /** وقتی اتصال زنده نیست، نوشتن معنا ندارد. */
  live: boolean;
  maxLength?: number;
}>();

const emit = defineEmits<{ send: [text: string] }>();

const draft = ref('');
const list = ref<HTMLElement | null>(null);

const limit = computed(() => props.maxLength ?? 300);

const canSend = computed(() => props.live && draft.value.trim().length > 0);

const lines = computed(() =>
  props.messages.map((message) => {
    // با *مقدارِ* صندلی پیدا می‌شود نه با جایگاه در آرایه: در اتاق انتظار
    // صندلی‌ها پشت سر هم نیستند و کسی که برود، یک شماره وسط خالی می‌ماند.
    const author = props.people.find((p) => p.seat === message.seat) ?? null;

    return {
      id: message.id,
      text: message.text,
      mine: message.seat === props.seat,
      name: author?.displayName || t('game.unknownPlayer'),
      color: author?.avatarColor ?? 'var(--hx-accent)',
      at: time(message.sentAt),
    };
  }),
);

/** فقط ساعت و دقیقه؛ روزِ پیام در یک دستِ بازی همیشه همان روز است. */
function time(iso: string): string {
  const at = new Date(iso);
  return Number.isNaN(at.getTime())
    ? ''
    : at.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function send(): void {
  if (!canSend.value) return;

  emit('send', draft.value.trim());
  draft.value = '';
}

// پیامِ تازه که آمد، فهرست تا پایین می‌رود — وگرنه گفت‌وگو زیر لبه گم می‌شود.
watch(
  () => props.messages.length,
  async () => {
    await nextTick();
    const box = list.value;
    if (box) box.scrollTop = box.scrollHeight;
  },
);
</script>

<template>
  <div class="hx-chat">
    <ol ref="list" class="hx-chat__list" aria-live="polite">
      <li v-if="lines.length === 0" class="hx-chat__empty hx-muted hx-small">
        {{ t('game.chatEmpty') }}
      </li>

      <li
        v-for="line in lines"
        :key="line.id"
        class="hx-chat__line"
        :class="{ 'is-mine': line.mine }"
      >
        <span class="hx-chat__who" :style="{ '--hx-avatar-color': line.color }">
          {{ line.name }}
        </span>
        <span class="hx-chat__text">{{ line.text }}</span>
        <time class="hx-chat__at">{{ line.at }}</time>
      </li>
    </ol>

    <form class="hx-chat__compose" @submit.prevent="send()">
      <label class="hx-sr-only" for="hx-chat-input">{{ t('game.chatPlaceholder') }}</label>
      <input
        id="hx-chat-input"
        v-model="draft"
        class="hx-input hx-chat__input"
        type="text"
        autocomplete="off"
        :maxlength="limit"
        :disabled="!live"
        :placeholder="live ? t('game.chatPlaceholder') : t('game.chatOffline')"
      />
      <button type="submit" class="hx-btn hx-btn--sm hx-btn--primary" :disabled="!canSend">
        {{ t('game.chatSend') }}
      </button>
    </form>
  </div>
</template>
