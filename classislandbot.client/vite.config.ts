import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import plugin from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';
import VueDevTools from 'vite-plugin-vue-devtools';
import IconsResolver from 'unplugin-icons/resolver';
import { env } from 'process';

const target = env.ASPNETCORE_HTTPS_PORT ? `https://localhost:${env.ASPNETCORE_HTTPS_PORT}` :
    env.ASPNETCORE_URLS ? env.ASPNETCORE_URLS.split(';')[0] : 'https://localhost:7294';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [
      plugin(),
      tailwindcss(),
      VueDevTools({ launchEditor: 'rider' }),
      AutoImport({
        resolvers: [
          ElementPlusResolver({
            importStyle: "sass"
          }),
          IconsResolver({
            prefix: 'Icon',
          }),
        ],
      }),
      Components({
        resolvers: [
          ElementPlusResolver({
            importStyle: "sass"
          }),
          IconsResolver({
            enabledCollections: ['ep'],
          }),
        ],
      }),
    ],
    css: {
      preprocessorOptions: {
        scss: {
          additionalData: `@use "@/assets/base.scss";`,
        },
      },
    },
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '^/api': {
                target,
                secure: false
            },
            '^/auth/github/callback': {
                target,
                secure: false
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '50069')
    }
})
