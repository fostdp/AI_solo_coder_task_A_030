import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import vue from '@vitejs/plugin-vue';
import viteCompression from 'vite-plugin-compression';
import path from 'path';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const enableCDN = env.VITE_ENABLE_CDN === 'true';
  const cdnBase = env.VITE_CDN_BASE || '/';

  return {
    plugins: [
      react(),
      vue(),
      viteCompression({
        verbose: true,
        disable: false,
        threshold: 10240,
        algorithm: 'gzip',
        ext: '.gz',
        deleteOriginFile: false,
      }),
      viteCompression({
        verbose: true,
        disable: false,
        threshold: 10240,
        algorithm: 'brotliCompress',
        ext: '.br',
        deleteOriginFile: false,
      }),
    ],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src'),
      },
    },
    server: {
      port: 3000,
      proxy: {
        '/api': {
          target: 'http://localhost:5000',
          changeOrigin: true,
        },
        '/hubs': {
          target: 'http://localhost:5000',
          changeOrigin: true,
          ws: true,
        },
      },
    },
    build: {
      outDir: 'dist',
      sourcemap: mode !== 'production',
      minify: 'esbuild',
      target: 'es2015',
      cssCodeSplit: true,
      rollupOptions: {
        output: {
          manualChunks: {
            vendor: ['react', 'react-dom', 'react-router-dom'],
            echarts: ['echarts', 'echarts-for-react'],
            signalr: ['@microsoft/signalr'],
            axios: ['axios'],
          },
          chunkFileNames: enableCDN
            ? 'assets/js/[name]-[hash].js'
            : 'assets/js/[name]-[hash].js',
          entryFileNames: 'assets/js/[name]-[hash].js',
          assetFileNames: 'assets/[ext]/[name]-[hash].[ext]',
        },
      },
      chunkSizeWarningLimit: 1500,
    },
    base: enableCDN ? cdnBase : '/',
  };
});
