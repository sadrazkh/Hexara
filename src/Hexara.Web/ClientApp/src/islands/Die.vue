<script setup lang="ts">
import { computed } from 'vue';
import { pipGrid } from '@/game/dice';

/**
 * یک تاس، با خال به‌جای رقم.
 *
 * فقط *داخلِ* تاس را می‌کشد؛ قاب، اندازه و رنگش از کلاسِ میزبان می‌آید تا همین
 * یک کامپوننت هم در نوار بالای صفحه جا شود و هم روی کارتِ بزرگِ تاس. خال‌ها با
 * ‎currentColor‎ کشیده می‌شوند، پس رنگِ هر تاس همان رنگِ متنِ قابش است.
 *
 * چرخشِ حین انداختن روی همین لایه‌ی داخلی می‌نشیند نه روی قاب، چون قاب خودش
 * زاویه دارد و یک انیمیشنِ ‎transform‎ آن زاویه را پاک می‌کرد.
 */
const props = defineProps<{ face: number | null }>();

const cells = computed(() => pipGrid(props.face));
</script>

<template>
  <span class="hx-die__face" aria-hidden="true">
    <span
      v-for="(lit, cell) in cells"
      :key="cell"
      class="hx-die__pip"
      :class="{ 'is-on': lit }"
    ></span>
  </span>
</template>
