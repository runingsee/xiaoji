import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// 开发: npm run dev → 5173，/api 代理到 .NET (5000)
// 生产: npm run build → dist/，由 .NET 静态托管（复制为 server/wwwroot）
export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5225',
        changeOrigin: true,
      },
    },
  },
  build: {
    outDir: 'dist',
    // 哈希文件名便于 Nginx 长缓存
    assetsDir: 'assets',
  },
})