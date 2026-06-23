import { House } from '@element-plus/icons-vue'
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: 'home',
    name: 'home',
    component: () => import('@/views/HomeView.vue'),
    meta: {
      title: '首页',
      icon: House,
    },
  },
  {
    path: 'home2',
    name: 'home2',
    component: () => import('@/views/HomeView.vue'),
    meta: {
      title: '首页',
      icon: House,
    },
  },
]

export default routes
