<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue';
import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import { axialToWorld, hexDisc } from '@/three/hex';
import { TERRAIN_TOKEN, tokenColor } from '@/three/board';
import { buildScenery } from '@/three/scenery';
import { THEME_CHANGE, token } from '@/theme';

const props = withDefaults(defineProps<{ radius?: number }>(), { radius: 2 });

const host = ref<HTMLDivElement | null>(null);
const failed = ref(false);
const renderer = shallowRef<THREE.WebGLRenderer | null>(null);

let frame = 0;
let observer: ResizeObserver | null = null;
let themeListener: (() => void) | null = null;
let disposables: { dispose(): void }[] = [];

/**
 * زمین‌ها با نامشان نگه داشته می‌شوند نه با عدد رنگ.
 *
 * قبلاً این‌جا یک فهرست رنگِ سخت‌کد بود که با ‎tokens.css‎ لغزیده بود — سبزِ
 * جنگل روی صفحه‌ی اصلی با سبزِ جنگل سرِ بازی یکی نبود. حالا هر دو از یک
 * منبع می‌آیند و زینتِ زمین هم همان ماژول بازی را می‌سازد.
 */
const TERRAINS = ['Hills', 'Forest', 'Pasture', 'Fields', 'Mountains', 'Desert'];

/** توزیع ثابت (بدون تصادف) تا پیش‌نمایش هیرو در هر بار بارگذاری یکسان باشد. */
function terrainFor(index: number): string {
  return TERRAINS[(index * 5 + 2) % TERRAINS.length]!;
}

function terrainColor(terrain: string): THREE.Color {
  return tokenColor(TERRAIN_TOKEN[terrain] ?? TERRAIN_TOKEN.Desert!, 0xd9c18f);
}

/** رنگ یک توکن نور، با جایگزین در صورت نبودنش. */
function themeLight(name: string, fallback: number): THREE.Color {
  const raw = token(name);
  return raw ? new THREE.Color(raw) : new THREE.Color(fallback);
}

function buildScene(container: HTMLDivElement) {
  const width = container.clientWidth || 1;
  const height = container.clientHeight || 1;

  const gl = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
  gl.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  gl.setSize(width, height, false);
  gl.toneMapping = THREE.ACESFilmicToneMapping;
  gl.toneMappingExposure = 1.05;
  container.appendChild(gl.domElement);
  renderer.value = gl;

  const scene = new THREE.Scene();

  // محیط داخلی سبک three به‌جای فایل HDR — بازتاب‌های نرم بدون دانلود اضافه.
  const pmrem = new THREE.PMREMGenerator(gl);
  const envRt = pmrem.fromScene(new RoomEnvironment(), 0.04);
  scene.environment = envRt.texture;
  disposables.push(envRt, pmrem);

  const camera = new THREE.PerspectiveCamera(38, width / height, 0.1, 100);
  camera.position.set(0, 9.2, 8.4);
  camera.lookAt(0, 0, 0);

  const key = new THREE.DirectionalLight(0xffffff, 2.1);
  key.position.set(5, 10, 6);
  scene.add(key);

  // نورهای پُرکننده و لبه از توکن‌های تم می‌آیند، مثل برد بازی. قبلاً رنگ ثابتِ
  // نشانِ قدیمی (فیروزه‌ای) بود و کنارِ همان نشان روی صفحه‌ی اصلی می‌نشست.
  const ambient = new THREE.AmbientLight(themeLight('--hx-light-ambient', 0x8a6a3f), 0.55);
  scene.add(ambient);

  const rim = new THREE.DirectionalLight(themeLight('--hx-light-rim', 0xe0a63a), 0.8);
  rim.position.set(-6, 4, -5);
  scene.add(rim);

  const onThemeChange = () => {
    ambient.color.copy(themeLight('--hx-light-ambient', 0x8a6a3f));
    rim.color.copy(themeLight('--hx-light-rim', 0xe0a63a));
    refreshTerrain();
  };

  // تعویض تم: زمین‌ها جای خودشان رنگ می‌گیرند، ولی زینت رنگ را در نمونه‌ها
  // دارد و باید از نو ساخته شود.
  let refreshTerrain = () => {};

  document.addEventListener(THEME_CHANGE, onThemeChange);
  themeListener = onThemeChange;

  const board = new THREE.Group();
  scene.add(board);

  const size = 1;
  const cells = hexDisc(props.radius);

  const tileGeometry = new THREE.CylinderGeometry(size * 0.94, size * 0.94, 0.36, 6);
  disposables.push(tileGeometry);

  const materials = new Map<string, THREE.MeshStandardMaterial>();
  for (const terrain of TERRAINS) {
    const material = new THREE.MeshStandardMaterial({
      color: terrainColor(terrain),
      roughness: 0.72,
      metalness: 0.06,
    });
    materials.set(terrain, material);
    disposables.push(material);
  }

  const tiles = cells.map((cell, index) => ({
    q: cell.q,
    r: cell.r,
    terrain: terrainFor(index),
  }));

  for (const tile of tiles) {
    const { x, z } = axialToWorld(tile.q, tile.r, size);
    const mesh = new THREE.Mesh(tileGeometry, materials.get(tile.terrain)!);
    mesh.position.set(x, 0, z);
    board.add(mesh);
  }

  // زینتِ زمین، از همان ماژولی که برد بازی استفاده می‌کند. بدون سایه، چون این
  // صحنه نور سایه‌انداز ندارد و صفحه‌ی اصلی باید سبک بماند.
  let scenery = buildScenery(tiles, size, terrainColor, tokenColor, false);
  board.add(scenery.group);

  refreshTerrain = () => {
    for (const [terrain, material] of materials) {
      material.color.copy(terrainColor(terrain));
    }

    board.remove(scenery.group);
    for (const item of scenery.disposables) item.dispose();

    scenery = buildScenery(tiles, size, terrainColor, tokenColor, false);
    board.add(scenery.group);
  };

  disposables.push({ dispose: () => scenery.disposables.forEach((item) => item.dispose()) });

  // حلقه‌ی آب دور برد — یک استوانه‌ی کم‌ارتفاع نیمه‌شفاف.
  const seaRadius = (props.radius + 1.15) * size * Math.SQRT2 * 1.28;
  const seaGeometry = new THREE.CylinderGeometry(seaRadius, seaRadius, 0.16, 64);
  const seaMaterial = new THREE.MeshStandardMaterial({
    color: 0x1c4f7c,
    roughness: 0.25,
    metalness: 0.35,
    transparent: true,
    opacity: 0.85,
  });
  const sea = new THREE.Mesh(seaGeometry, seaMaterial);
  sea.position.y = -0.16;
  board.add(sea);
  disposables.push(seaGeometry, seaMaterial);

  board.rotation.x = 0.04;

  const clock = new THREE.Clock();
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  const render = () => {
    frame = requestAnimationFrame(render);
    const elapsed = clock.getElapsedTime();

    if (!reduceMotion) {
      board.rotation.y = elapsed * 0.16;

      // نفسِ آرام روی کلِ جزیره، نه روی هر خانه.
      //
      // قبلاً هر خانه جدا بالا و پایین می‌رفت. حالا که درخت و قله روی خانه‌ها
      // نشسته‌اند، آن حرکت زینت را از زمینش جدا می‌کرد — چون زینت یک شبکه‌ی
      // نمونه‌دار برای کل برد است، نه فرزندِ هر خانه.
      board.position.y = Math.sin(elapsed * 0.9) * 0.06;
    }

    gl.render(scene, camera);
  };
  render();

  observer = new ResizeObserver(() => {
    const w = container.clientWidth || 1;
    const h = container.clientHeight || 1;
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    gl.setSize(w, h, false);
  });
  observer.observe(container);
}

onMounted(() => {
  const container = host.value;
  if (!container) return;

  try {
    buildScene(container);
  } catch (error) {
    console.warn('[hexara] WebGL preview unavailable', error);
    failed.value = true;
  }
});

onBeforeUnmount(() => {
  cancelAnimationFrame(frame);
  observer?.disconnect();

  if (themeListener) {
    document.removeEventListener(THEME_CHANGE, themeListener);
    themeListener = null;
  }

  for (const item of disposables) item.dispose();
  disposables = [];
  renderer.value?.dispose();
  renderer.value?.domElement.remove();
});
</script>

<template>
  <div ref="host" class="hx-hero-board" :class="{ 'hx-hero-board--failed': failed }">
    <!-- اگر WebGL در دسترس نباشد، یک نشان ساده به‌جای صحنه نمایش داده می‌شود. -->
    <svg v-if="failed" class="hx-hero-board__fallback" viewBox="0 0 100 100" aria-hidden="true">
      <path d="M50 8 88 29v42L50 92 12 71V29z" fill="none" stroke="currentColor" stroke-width="2" />
      <path d="M50 30 70 41v22L50 74 30 63V41z" fill="currentColor" opacity=".25" />
    </svg>
  </div>
</template>

<style scoped>
.hx-hero-board {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  color: var(--hx-accent-2);
}

.hx-hero-board :deep(canvas) {
  width: 100% !important;
  height: 100% !important;
  display: block;
  filter: drop-shadow(0 30px 60px rgb(0 0 0 / 45%));
}

.hx-hero-board__fallback {
  width: 60%;
  opacity: 0.5;
}
</style>
