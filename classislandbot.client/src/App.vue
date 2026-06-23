<script setup lang="ts">
import { elementPlusConfig } from '@/plugins/element-plus'
</script>

<template>
  <el-config-provider v-bind="elementPlusConfig">
    <router-view/>
  </el-config-provider>
</template>

<style scoped lang="scss">
:deep(a) {
  color: inherit;
  text-decoration: none;
}
</style>
