import { createRouter, createWebHistory } from 'vue-router'
import rootRoutes from '@/router/rootRoutes'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('@/views/index.vue'),
      redirect: '/discussions',
      children: rootRoutes,
    },
    {
      path: '/auth',
      name: 'authRoot',
      component: () => import("@/views/auth/index.vue"),
      children: [
        {
          path: 'login',
          name: 'login',
          component: () => import("@/views/auth/Login.vue"),
        }
      ]
    }
  ],
})

export default router
