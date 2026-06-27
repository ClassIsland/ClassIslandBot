<script setup lang="ts">
import { SwitchButton } from '@element-plus/icons-vue'
import rootRoutes from '@/router/rootRoutes'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const authStore = useAuthStore()

function logout() {
  void authStore.logout()
}
</script>

<template>
  <div>
    <el-container class="min-h-screen">
      <el-header class="px-0" style="padding: 0">
        <el-menu
          class="el-menu-demo"
          mode="horizontal"
          :ellipsis="false"
        >
          <div class="flex items-center gap-2 mx-6">
            <img src="../assets/Logo.png" width="32" height="32"
                 alt="Logo"/>
            <h1 class="font-medium text-xl">ClassIsland Bot</h1>
          </div>
          <div class="mx-6 flex flex-1 items-center justify-end gap-3">
            <span
              v-if="authStore.user.login"
              class="text-sm text-[var(--el-text-color-regular)]"
            >
              {{ authStore.user.login }}
            </span>
            <el-button
              text
              :icon="SwitchButton"
              @click="logout"
            >
              退出
            </el-button>
          </div>
        </el-menu>
      </el-header>
      <el-container>
        <el-aside width="225px">
          <el-menu
            style="min-height: 100%"
            router
            :defaultActive="router.currentRoute.value.path"
          >
            <el-menu-item v-for="i in rootRoutes" :key="i.name" :index="`/${i.path}`">
              <el-icon v-if="i.meta?.icon">
                <component :is="i.meta.icon" />
              </el-icon>
              <span>{{ i.meta?.title }}</span>
            </el-menu-item>
          </el-menu>
        </el-aside>
        <el-container>
          <el-main>
            <router-view/>
          </el-main>
        </el-container>
      </el-container>
    </el-container>
  </div>
</template>

<style scoped lang="scss">

</style>
