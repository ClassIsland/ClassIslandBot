import { ChatDotRound, House } from '@element-plus/icons-vue'
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
    path: 'discussions',
    name: 'discussions',
    component: () => import('@/views/DiscussionAssociationsView.vue'),
    meta: {
      title: 'Discussion 关联',
      icon: ChatDotRound,
    },
  },
]

export default routes
