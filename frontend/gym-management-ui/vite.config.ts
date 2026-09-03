import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

// import.meta.dirname rather than __dirname: Vite's native config loader does not provide
// __dirname, and that loader becomes the default in a future major. Fixing it now costs
// nothing and avoids the config silently failing to resolve these aliases later.
const here = import.meta.dirname

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(here, './src'),
      '@assets': path.resolve(here, './src/assets'),
      '@components': path.resolve(here, './src/components'),
      '@constants': path.resolve(here, './src/constants'),
      '@features': path.resolve(here, './src/features'),
      '@hooks': path.resolve(here, './src/hooks'),
      '@lib': path.resolve(here, './src/lib'),
      '@pages': path.resolve(here, './src/pages'),
      '@routes': path.resolve(here, './src/routes'),
      '@services': path.resolve(here, './src/services'),
      '@store': path.resolve(here, './src/store'),
      '@app-types': path.resolve(here, './src/types'),
      '@utils': path.resolve(here, './src/utils'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',
        changeOrigin: true,
      },
    },
  },
})
