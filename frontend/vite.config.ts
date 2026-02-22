import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // 개발 서버에서 API 프록시 (EC2로 직접 연결)
      '/webhook': {
        target: 'http://13.125.245.167',
        changeOrigin: true,
      },
      '/api': {
        target: 'http://13.125.245.167',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: false,
  },
})
