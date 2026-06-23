import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite'
import AutoImport from 'unplugin-auto-import/vite'
import Components from 'unplugin-vue-components/vite'
import { ElementPlusResolver } from 'unplugin-vue-components/resolvers'
import plugin from '@vitejs/plugin-vue';
import tailwindcss from '@tailwindcss/vite';
import VueDevTools from 'vite-plugin-vue-devtools';
import IconsResolver from 'unplugin-icons/resolver';
import fs from 'fs';
import path from 'path';
import child_process from 'child_process';
import { env } from 'process';

const baseFolder =
    env.APPDATA !== undefined && env.APPDATA !== ''
        ? `${env.APPDATA}/ASP.NET/https`
        : `${env.HOME}/.aspnet/https`;

const certificateName = "khaslanamarketplace.client";
const certFilePath = path.join(baseFolder, `${certificateName}.pem`);
const keyFilePath = path.join(baseFolder, `${certificateName}.key`);

if (!fs.existsSync(baseFolder)) {
    fs.mkdirSync(baseFolder, { recursive: true });
}

if (!fs.existsSync(certFilePath) || !fs.existsSync(keyFilePath)) {
    if (0 !== child_process.spawnSync('dotnet', [
        'dev-certs',
        'https',
        '--export-path',
        certFilePath,
        '--format',
        'Pem',
        '--no-password',
    ], { stdio: 'inherit', }).status) {
        throw new Error("Could not create certificate.");
    }
}

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
            }
        },
        port: parseInt(env.DEV_SERVER_PORT || '50069'),
        https: {
            key: fs.readFileSync(keyFilePath),
            cert: fs.readFileSync(certFilePath),
        }
    }
})
