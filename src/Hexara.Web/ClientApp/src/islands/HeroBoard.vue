<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue';
import * as THREE from 'three';
import { RoomEnvironment } from 'three/examples/jsm/environments/RoomEnvironment.js';
import { axialToWorld, hexDisc } from '@/three/hex';

const props = withDefaults(defineProps<{ radius?: number }>(), { radius: 2 });

const host = ref<HTMLDivElement | null>(null);
const failed = ref(false);
const renderer = shallowRef<THREE.WebGLRenderer | null>(null);

let frame = 0;
let observer: ResizeObserver | null = null;
let disposables: { dispose(): void }[] = [];

// رنگ زمین‌ها از همان توکن‌های CSS گرفته می‌شود تا برد و رابط یک‌دست بمانند.
const TERRAIN_COLORS = [
  0xc05a3e, // آجر
  0x2f7d4f, // چوب
  0x8fc95a, // پشم
  0xe0b23c, // گندم
  0x7d8aa3, // سنگ
  0xcbb187, // بیابان
];

/** توزیع ثابت (بدون تصادف) تا پیش‌نمایش هیرو در هر بار بارگذاری یکسان باشد. */
function terrainFor(index: number): number {
  return TERRAIN_COLORS[(index * 5 + 2) % TERRAIN_COLORS.length];
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
  scene.add(new THREE.AmbientLight(0x8fb3ff, 0.55));

  const rim = new THREE.DirectionalLight(0x5ee7c6, 0.8);
  rim.position.set(-6, 4, -5);
  scene.add(rim);

  const board = new THREE.Group();
  scene.add(board);

  const size = 1;
  const cells = hexDisc(props.radius);

  const tileGeometry = new THREE.CylinderGeometry(size * 0.94, size * 0.94, 0.36, 6);
  disposables.push(tileGeometry);

  const materials = new Map<number, THREE.MeshStandardMaterial>();
  for (const color of TERRAIN_COLORS) {
    const material = new THREE.MeshStandardMaterial({ color, roughness: 0.72, metalness: 0.06 });
    materials.set(color, material);
    disposables.push(material);
  }

  cells.forEach((cell, index) => {
    const { x, z } = axialToWorld(cell.q, cell.r, size);
    const mesh = new THREE.Mesh(tileGeometry, materials.get(terrainFor(index))!);
    mesh.position.set(x, 0, z);
    mesh.userData.phase = index * 0.35;
    board.add(mesh);
  });

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
      for (const child of board.children) {
        if (child === sea) continue;
        child.position.y = Math.sin(elapsed * 1.1 + (child.userData.phase as number)) * 0.045;
      }
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
