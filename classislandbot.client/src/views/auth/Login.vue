<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Connection } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const authStore = useAuthStore()

const errorMessage = computed(() => {
  const error = Array.isArray(route.query.error) ? route.query.error[0] : route.query.error
  if (!error) {
    return ''
  }

  if (error === 'organization-required') {
    return '当前 GitHub 账号不属于 ClassIsland 组织。'
  }

  if (error === 'callback-not-handled') {
    return '登录回调没有被后端处理，请检查开发服务器代理或回调地址配置。'
  }

  return 'GitHub 登录未完成，请重试。'
})

function getReturnUrl() {
  const returnUrl = Array.isArray(route.query.returnUrl) ? route.query.returnUrl[0] : route.query.returnUrl
  return typeof returnUrl === 'string' && returnUrl.startsWith('/') && !returnUrl.startsWith('//')
    ? returnUrl
    : '/'
}

function loginWithGitHub() {
  authStore.login(getReturnUrl())
}
</script>

<template>
  <div class="flex items-center justify-center justify-items-center h-full">
    <div class="flex w-full max-w-80 flex-col gap-3 items-center">
      <h2 class="text-2xl font-bold">登录</h2>
      <p>您需要登录以继续使用本应用。</p>
      <el-alert
        v-if="errorMessage"
        :title="errorMessage"
        type="error"
        show-icon
        :closable="false"
      />
      <el-button
        type="primary"
        size="large"
        class="w-full"
        :loading="authStore.loading"
        @click="loginWithGitHub"
      >
        <el-icon>
          <Connection />
        </el-icon>
        使用 GitHub 继续
      </el-button>
    </div>
  </div>
</template>

<style scoped lang="scss">

</style>
