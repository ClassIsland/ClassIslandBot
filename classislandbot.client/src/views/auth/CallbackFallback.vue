<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

function backToLogin() {
  void router.replace({
    name: 'login',
    query: {
      error: 'callback-not-handled',
    },
  })
}
</script>

<template>
  <div class="flex items-center justify-center justify-items-center h-full">
    <div class="flex w-full max-w-80 flex-col gap-3 items-center">
      <h2 class="text-2xl font-bold">登录回调未完成</h2>
      <el-alert
        title="登录回调没有被后端处理，请检查开发服务器代理或回调地址配置。"
        type="error"
        show-icon
        :closable="false"
      />
      <el-button
        type="primary"
        class="w-full"
        @click="backToLogin"
      >
        返回登录
      </el-button>
    </div>
  </div>
</template>
