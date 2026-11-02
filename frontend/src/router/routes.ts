import type { RouteRecordRaw } from 'vue-router'

export const ROUTE_NAMES = {
  login: 'login',
  chatList: 'chat-list',
  chat: 'chat',
  profile: 'profile',
} as const

export const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: ROUTE_NAMES.login,
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    name: ROUTE_NAMES.chatList,
    component: () => import('@/views/ChatListView.vue'),
  },
  {
    path: '/chat/:id',
    name: ROUTE_NAMES.chat,
    component: () => import('@/views/ChatView.vue'),
    props: true,
  },
  {
    path: '/profile',
    name: ROUTE_NAMES.profile,
    component: () => import('@/views/ProfileView.vue'),
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: { name: ROUTE_NAMES.chatList },
  },
]
