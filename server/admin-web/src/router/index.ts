import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { TOKEN_KEY } from '@/api/http'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/Login.vue'),
    meta: { title: '登录' },
  },
  {
    path: '/',
    component: () => import('@/layouts/AdminLayout.vue'),
    redirect: '/dashboard',
    children: [
      { path: 'dashboard', name: 'dashboard', component: () => import('@/views/Dashboard.vue'), meta: { title: '数据仪表盘' } },
      { path: 'server', name: 'server', component: () => import('@/views/ServerMonitor.vue'), meta: { title: '服务器监控' } },
      { path: 'players', name: 'players', component: () => import('@/views/Players.vue'), meta: { title: '玩家管理' } },
      { path: 'reviews', name: 'reviews', component: () => import('@/views/Reviews.vue'), meta: { title: '运营审核' } },
      { path: 'announcements', name: 'announcements', component: () => import('@/views/Announcements.vue'), meta: { title: '公告管理' } },
      { path: 'database', name: 'database', component: () => import('@/views/Database.vue'), meta: { title: '数据库管理' } },
      { path: 'deploy', name: 'deploy', component: () => import('@/views/Deploy.vue'), meta: { title: '服务端部署' } },
      { path: 'audit-logs', name: 'audit-logs', component: () => import('@/views/AuditLogs.vue'), meta: { title: '操作审计' } },
      { path: 'admins', name: 'admins', component: () => import('@/views/Admins.vue'), meta: { title: '管理员账号' } },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  document.title = to.meta.title ? `${to.meta.title} · WildCourt 运营后台` : 'WildCourt 运营后台'
  const token = localStorage.getItem(TOKEN_KEY)
  if (!token && to.path !== '/login') {
    return '/login'
  }
  if (token && to.path === '/login') {
    return '/dashboard'
  }
  return true
})

export default router
