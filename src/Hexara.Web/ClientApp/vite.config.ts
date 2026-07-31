import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import { resolve } from 'node:path';
import { serviceWorker } from './vite-plugin-service-worker';

const outDir = resolve(__dirname, '../wwwroot/dist');

// خروجی build مستقیم داخل wwwroot/dist می‌رود تا ASP.NET بتواند نام فایل‌های
// هش‌دار را از manifest.json بخواند (ViteManifest.cs).
export default defineConfig({
  plugins: [
    vue(),
    serviceWorker({ outDir, template: resolve(__dirname, 'sw-template.js') }),
  ],
  base: '/dist/',
  resolve: {
    alias: {
      // فایل‌های ترجمه بین سرور (Razor) و کلاینت مشترک‌اند.
      '@locales': resolve(__dirname, '../Locales'),
      '@': resolve(__dirname, 'src'),
    },
  },
  build: {
    manifest: true,
    outDir,
    emptyOutDir: true,
    target: 'es2022',
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'src/main.ts'),
      },
      output: {
        // Three.js حجیم است؛ جدا نگه داشتنش یعنی صفحاتی که برد ندارند آن را
        // دانلود نمی‌کنند و کش مرورگر بین نسخه‌ها حفظ می‌شود.
        manualChunks(id) {
          if (id.includes('node_modules/three')) return 'three';
          return undefined;
        },
      },
    },
  },
  server: {
    port: 5173,
    strictPort: true,
    cors: true,
  },
});
