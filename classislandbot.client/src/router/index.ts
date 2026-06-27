import { createRouter, createWebHistory } from 'vue-router'
import rootRoutes from '@/router/rootRoutes'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'root',
      component: () => import('@/views/index.vue'),
      redirect: '/home',
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
        },
        {
          path: 'github/callback',
          name: 'githubCallbackFallback',
          component: () => import("@/views/auth/CallbackFallback.vue"),
        }
      ]
    }
  ],
})

function getSafeReturnUrl(value: unknown) {
  const returnUrl = Array.isArray(value) ? value[0] : value
  if (typeof returnUrl !== 'string') {
    return '/'
  }

  return returnUrl.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : '/'
}

router.beforeEach(async (to) => {
  const authStore = useAuthStore()
  await authStore.loadCurrentUser()

  if (to.path.startsWith('/auth')) {
    if (authStore.isAuthenticated && to.name === 'login') {
      return getSafeReturnUrl(to.query.returnUrl)
    }

    return true
  }

  if (!authStore.isAuthenticated) {
    return {
      name: 'login',
      query: {
        returnUrl: to.fullPath,
      },
    }
  }

  return true
})

export default router
