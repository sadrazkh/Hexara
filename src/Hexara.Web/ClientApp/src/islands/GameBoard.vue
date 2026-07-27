<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import gsap from 'gsap';
import { BoardScene, type BoardData, type Highlights, type Pick } from '@/three/board';

const props = defineProps<{ board: BoardData; highlights: Highlights }>();
const emit = defineEmits<{ pick: [Pick] }>();

const host = ref<HTMLDivElement | null>(null);
const failed = ref(false);
const hovering = ref(false);

let scene: THREE.Scene | null = null;
let camera: THREE.PerspectiveCamera | null = null;
let renderer: THREE.WebGLRenderer | null = null;
let controls: OrbitControls | null = null;
let board: BoardScene | null = null;
let observer: ResizeObserver | null = null;
let frame = 0;

const raycaster = new THREE.Raycaster();
const pointer = new THREE.Vector2();
const clock = new THREE.Clock();

/** نقطه‌ای که اشاره‌گر پایین آمد — تا چرخاندن دوربین با انتخاب اشتباه نشود. */
let pressedAt: { x: number; y: number } | null = null;

const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

/**
 * کیفیت بر اساس توان دستگاه پایین می‌آید: روی موبایل نه سایه‌ای هست نه
 * ضدلبه‌دندانه‌ای، و چگالی پیکسل هم سقف کمتری دارد.
 */
function quality() {
  const coarse = window.matchMedia('(pointer: coarse)').matches;
  const small = Math.min(window.innerWidth, window.innerHeight) < 720;
  const weak = coarse || small || (navigator.hardwareConcurrency ?? 4) <= 4;

  return {
    shadows: !weak,
    antialias: !weak,
    pixelRatio: Math.min(window.devicePixelRatio, weak ? 1.5 : 2),
  };
}

function build(container: HTMLDivElement): void {
  const settings = quality();
  const width = container.clientWidth || 1;
  const height = container.clientHeight || 1;

  renderer = new THREE.WebGLRenderer({
    antialias: settings.antialias,
    alpha: true,
    powerPreference: 'high-performance',
  });
  renderer.setPixelRatio(settings.pixelRatio);
  renderer.setSize(width, height, false);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  renderer.shadowMap.enabled = settings.shadows;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  container.appendChild(renderer.domElement);

  scene = new THREE.Scene();

  const pmrem = new THREE.PMREMGenerator(renderer);
  const environment = pmrem.fromScene(new RoomEnvironment(), 0.04);
  scene.environment = environment.texture;

  board = new BoardScene();
  scene.add(board.root);
  board.update(props.board, props.highlights);

  const reach = board.extent(props.board.tiles);

  camera = new THREE.PerspectiveCamera(42, width / height, 0.1, 200);
  camera.position.set(0, reach * 1.5, reach * 1.35);

  controls = new OrbitControls(camera, renderer.domElement);
  controls.target.set(0, 0, 0);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.minDistance = reach * 0.7;
  controls.maxDistance = reach * 3;
  // نگذاریم دوربین زیر برد برود یا دقیقاً از بالا بیفتد؛ هر دو گیج‌کننده‌اند.
  controls.minPolarAngle = 0.15;
  controls.maxPolarAngle = Math.PI / 2.35;
  controls.update();

  const key = new THREE.DirectionalLight(0xffffff, 2.2);
  key.position.set(reach * 0.7, reach * 1.6, reach * 0.9);
  key.castShadow = settings.shadows;
  if (settings.shadows) {
    key.shadow.mapSize.set(1024, 1024);
    key.shadow.camera.near = 1;
    key.shadow.camera.far = reach * 5;
    const span = reach * 1.6;
    key.shadow.camera.left = -span;
    key.shadow.camera.right = span;
    key.shadow.camera.top = span;
    key.shadow.camera.bottom = -span;
  }
  scene.add(key);
  scene.add(new THREE.AmbientLight(0x8fb3ff, 0.5));

  const rim = new THREE.DirectionalLight(0x5ee7c6, 0.7);
  rim.position.set(-reach, reach * 0.8, -reach);
  scene.add(rim);

  observer = new ResizeObserver(() => resize(container));
  observer.observe(container);

  render();
}

function resize(container: HTMLDivElement): void {
  if (!camera || !renderer) return;

  const width = container.clientWidth || 1;
  const height = container.clientHeight || 1;

  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  renderer.setSize(width, height, false);
}

function render(): void {
  frame = requestAnimationFrame(render);
  if (!renderer || !scene || !camera) return;

  controls?.update();
  if (!reduceMotion) {
    board?.animateMarkers(clock.getElapsedTime());
  }

  renderer.render(scene, camera);
}

/** قطعه‌های تازه از هیچ بزرگ می‌شوند تا ساخت‌وساز حس داشته باشد. */
function animateSpawned(): void {
  if (!board) return;

  const spawned = board.takeSpawned();
  if (reduceMotion) return;

  for (const piece of spawned) {
    piece.scale.setScalar(0.01);
    gsap.to(piece.scale, { x: 1, y: 1, z: 1, duration: 0.42, ease: 'back.out(2.2)' });
  }
}

function pickAt(event: PointerEvent): void {
  if (!renderer || !camera || !board) return;

  const rect = renderer.domElement.getBoundingClientRect();
  pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

  raycaster.setFromCamera(pointer, camera);
  const hit = raycaster.intersectObjects(board.pickables, false)[0];
  const pick = hit?.object.userData.pick as Pick | undefined;

  if (pick) {
    emit('pick', pick);
  }
}

function updateHover(event: PointerEvent): void {
  if (!renderer || !camera || !board) return;

  const rect = renderer.domElement.getBoundingClientRect();
  pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
  pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

  raycaster.setFromCamera(pointer, camera);
  const hit = raycaster.intersectObjects(board.pickables, false)[0];
  hovering.value = Boolean(hit?.object.userData.pick);
}

function onPointerDown(event: PointerEvent): void {
  pressedAt = { x: event.clientX, y: event.clientY };
}

/** کشیدن برای چرخاندن دوربین نباید به‌عنوان کلیک حساب شود. */
function onPointerUp(event: PointerEvent): void {
  if (!pressedAt) return;

  const moved = Math.hypot(event.clientX - pressedAt.x, event.clientY - pressedAt.y);
  pressedAt = null;

  if (moved < 6) {
    pickAt(event);
  }
}

watch(
  () => [props.board, props.highlights],
  () => {
    board?.update(props.board, props.highlights);
    animateSpawned();
  },
  { deep: true },
);

onMounted(() => {
  const container = host.value;
  if (!container) return;

  try {
    build(container);
  } catch (error) {
    console.warn('[hexara] WebGL board unavailable', error);
    failed.value = true;
  }
});

onBeforeUnmount(() => {
  cancelAnimationFrame(frame);
  observer?.disconnect();
  controls?.dispose();
  board?.dispose();
  renderer?.dispose();
  renderer?.domElement.remove();
});
</script>

<template>
  <div
    ref="host"
    class="hx-board"
    :class="{ 'hx-board--pick': hovering }"
    @pointerdown="onPointerDown"
    @pointerup="onPointerUp"
    @pointermove="updateHover"
    @pointerleave="hovering = false"
  >
    <p v-if="failed" class="hx-board__fallback">
      <slot name="fallback" />
    </p>
  </div>
</template>

<style scoped>
.hx-board {
  position: relative;
  width: 100%;
  aspect-ratio: 4 / 3;
  min-height: 320px;
  border: 1px solid var(--hx-border);
  border-radius: var(--hx-r-lg);
  overflow: hidden;
  background: radial-gradient(circle at 50% 30%, rgb(28 79 124 / 35%), transparent 70%);
  touch-action: none;
}

.hx-board--pick {
  cursor: pointer;
}

.hx-board :deep(canvas) {
  display: block;
  width: 100% !important;
  height: 100% !important;
}

.hx-board__fallback {
  position: absolute;
  inset: 0;
  display: grid;
  place-items: center;
  padding: var(--hx-sp-4);
  text-align: center;
  color: var(--hx-text-faint);
}
</style>
