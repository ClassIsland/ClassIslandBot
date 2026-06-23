import { createRouter, createWebHistory } from 'vue-router'
import rootRoutes from '@/router/rootRoutes'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('@/views/index.vue'),
      children: rootRoutes,
    },
  ],
})

export default router
