<template>
  <el-container class="admin-layout">
    <el-aside :width="isCollapse ? '64px' : '220px'" class="layout-aside">
      <div class="aside-logo">
        <span v-if="!isCollapse">WildCourt 运营后台</span>
        <span v-else>WC</span>
      </div>
      <el-menu
        :default-active="activeMenu"
        :collapse="isCollapse"
        :collapse-transition="false"
        router
        background-color="#1f2d3d"
        text-color="#c0c4cc"
        active-text-color="#409eff"
      >
        <el-menu-item v-if="authStore.hasPermission('dashboard:view')" index="/dashboard">
          <el-icon><DataAnalysis /></el-icon>
          <template #title>数据仪表盘</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('server:view')" index="/server">
          <el-icon><Monitor /></el-icon>
          <template #title>服务器监控</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('players:view')" index="/players">
          <el-icon><UserFilled /></el-icon>
          <template #title>玩家管理</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('reviews:view')" index="/reviews">
          <el-icon><ChatDotRound /></el-icon>
          <template #title>运营审核</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('announcements:view')" index="/announcements">
          <el-icon><Bell /></el-icon>
          <template #title>公告管理</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('announcements:view')" index="/official-letters">
          <el-icon><Message /></el-icon>
          <template #title>官方发信</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('database:view')" index="/database">
          <el-icon><Coin /></el-icon>
          <template #title>数据库管理</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('deploy:view')" index="/deploy">
          <el-icon><Promotion /></el-icon>
          <template #title>服务端部署</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('audit:view')" index="/audit-logs">
          <el-icon><Document /></el-icon>
          <template #title>操作审计</template>
        </el-menu-item>
        <el-menu-item v-if="authStore.hasPermission('admins:view')" index="/admins">
          <el-icon><Setting /></el-icon>
          <template #title>管理员账号</template>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="layout-header">
        <div class="header-left">
          <el-icon class="collapse-btn" @click="isCollapse = !isCollapse">
            <Expand v-if="isCollapse" />
            <Fold v-else />
          </el-icon>
          <el-breadcrumb separator="/">
            <el-breadcrumb-item :to="{ path: '/dashboard' }">首页</el-breadcrumb-item>
            <el-breadcrumb-item>{{ currentTitle }}</el-breadcrumb-item>
          </el-breadcrumb>
        </div>
        <el-dropdown trigger="click" @command="onCommand">
          <div class="user-info">
            <el-avatar :size="30" class="user-avatar">
              {{ (authStore.profile?.displayName || authStore.profile?.username || 'A')[0].toUpperCase() }}
            </el-avatar>
            <span class="user-name">{{ authStore.profile?.displayName || authStore.profile?.username || '管理员' }}</span>
            <el-tag v-if="authStore.profile?.roleName" size="small" class="role-tag">{{ authStore.profile.roleName }}</el-tag>
            <el-icon><ArrowDown /></el-icon>
          </div>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="password">
                <el-icon><Key /></el-icon>修改密码
              </el-dropdown-item>
              <el-dropdown-item command="2fa">
                <el-icon><Lock /></el-icon>双因素认证
              </el-dropdown-item>
              <el-dropdown-item divided command="logout">
                <el-icon><SwitchButton /></el-icon>退出登录
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>

      <el-main class="layout-main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>

  <el-dialog v-model="pwdVisible" title="修改密码" width="420px" @closed="resetPwdForm">
    <el-form ref="pwdFormRef" :model="pwdForm" :rules="pwdRules" label-width="80px">
      <el-form-item label="旧密码" prop="oldPassword">
        <el-input v-model="pwdForm.oldPassword" type="password" show-password placeholder="请输入旧密码" />
      </el-form-item>
      <el-form-item label="新密码" prop="newPassword">
        <el-input v-model="pwdForm.newPassword" type="password" show-password placeholder="至少 6 位" />
      </el-form-item>
      <el-form-item label="确认密码" prop="confirmPassword">
        <el-input v-model="pwdForm.confirmPassword" type="password" show-password placeholder="再次输入新密码" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="pwdVisible = false">取消</el-button>
      <el-button type="primary" :loading="pwdLoading" @click="submitPassword">确定</el-button>
    </template>
  </el-dialog>

  <el-dialog v-model="tfVisible" :title="tfMode === 'setup' ? '开启双因素认证' : '关闭双因素认证'" width="440px" :close-on-click-modal="false" @closed="resetTf">
    <template v-if="tfMode === 'setup'">
      <el-alert type="info" :closable="false" show-icon class="tf-tip">
        使用身份验证器 App（Google Authenticator、Microsoft Authenticator、Authy 等）扫码，或手动输入密钥完成绑定
      </el-alert>
      <div class="tf-qr-wrap">
        <img v-if="tfOtpUri && !qrError" :src="qrImageUrl" alt="双因素二维码" class="tf-qr" @error="qrError = true" />
        <el-empty v-else description="二维码不可用，请手动输入下方密钥" :image-size="72" />
      </div>
      <div class="tf-secret-row">
        <el-input v-model="tfSecret" readonly class="tf-secret">
          <template #append>
            <el-button :disabled="!tfSecret" @click="copySecret">复制</el-button>
          </template>
        </el-input>
      </div>
      <el-form ref="tfFormRef" :model="tfForm" :rules="tfRules" label-width="80px">
        <el-form-item label="动态码" prop="code">
          <el-input v-model="tfForm.code" placeholder="输入 App 中显示的 6 位动态码" maxlength="6" inputmode="numeric" />
        </el-form-item>
      </el-form>
    </template>
    <template v-else>
      <el-alert type="warning" :closable="false" show-icon class="tf-tip">
        关闭后该账号登录将不再需要动态验证码，请确认已排除误操作风险
      </el-alert>
      <el-form ref="tfFormRef" :model="tfForm" :rules="tfRules" label-width="80px">
        <el-form-item label="动态码" prop="code">
          <el-input v-model="tfForm.code" placeholder="输入 App 中显示的 6 位动态码" maxlength="6" inputmode="numeric" />
        </el-form-item>
      </el-form>
    </template>
    <template #footer>
      <el-button @click="tfVisible = false">取消</el-button>
      <el-button type="primary" :loading="tfLoading" @click="submitTf">
        {{ tfMode === 'setup' ? '确认启用' : '确认关闭' }}
      </el-button>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import { authApi } from '@/api'
import { sha256Hex } from '@/utils/sha256'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const isCollapse = ref(false)
const activeMenu = computed(() => route.path)
const currentTitle = computed(() => (route.meta.title as string) || '首页')

onMounted(() => {
  if (!authStore.profile) {
    authStore.fetchProfile().catch(() => {
      // 401 由拦截器处理
    })
  }
})

async function onCommand(command: string) {
  if (command === 'logout') {
    authStore.logout()
    router.push('/login')
  } else if (command === 'password') {
    pwdVisible.value = true
  } else if (command === '2fa') {
    openTwoFactor()
  }
}

// ---- 修改密码 ----
const pwdVisible = ref(false)
const pwdLoading = ref(false)
const pwdFormRef = ref<FormInstance>()
const pwdForm = reactive({ oldPassword: '', newPassword: '', confirmPassword: '' })

const pwdRules: FormRules = {
  oldPassword: [{ required: true, message: '请输入旧密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    {
      validator: (_r, v, cb) => (v === pwdForm.newPassword ? cb() : cb(new Error('两次输入不一致'))),
      trigger: 'blur',
    },
  ],
}

function resetPwdForm() {
  pwdForm.oldPassword = ''
  pwdForm.newPassword = ''
  pwdForm.confirmPassword = ''
}

async function submitPassword() {
  if (!pwdFormRef.value) return
  const valid = await pwdFormRef.value.validate().catch(() => false)
  if (!valid) return

  pwdLoading.value = true
  try {
    await authStore.changePassword(await sha256Hex(pwdForm.oldPassword), await sha256Hex(pwdForm.newPassword))
    ElMessage.success('密码修改成功，请重新登录')
    pwdVisible.value = false
    authStore.logout()
    router.push('/login')
  } catch {
    // 已由拦截器提示
  } finally {
    pwdLoading.value = false
  }
}

// ---- 双因素认证 ----
const tfVisible = ref(false)
const tfLoading = ref(false)
const tfMode = ref<'setup' | 'disable'>('setup')
const tfSecret = ref('')
const tfOtpUri = ref('')
const qrError = ref(false)
const tfFormRef = ref<FormInstance>()
const tfForm = reactive({ code: '' })

const tfRules: FormRules = {
  code: [
    { required: true, message: '请输入动态验证码', trigger: 'blur' },
    { pattern: /^\d{6}$/, message: '请输入 6 位数字动态码', trigger: 'blur' },
  ],
}

const qrImageUrl = computed(() =>
  tfOtpUri.value ? `https://api.qrserver.com/v1/create-qr-code/?size=220x220&margin=8&data=${encodeURIComponent(tfOtpUri.value)}` : '',
)

async function openTwoFactor() {
  tfMode.value = authStore.profile?.twoFactorEnabled ? 'disable' : 'setup'
  tfVisible.value = true
  tfLoading.value = true
  try {
    if (tfMode.value === 'setup') {
      const res = await authApi.setup2fa()
      tfSecret.value = res.secret
      tfOtpUri.value = res.otpUri
    }
  } catch {
    tfVisible.value = false
  } finally {
    tfLoading.value = false
  }
}

async function copySecret() {
  try {
    await navigator.clipboard.writeText(tfSecret.value)
    ElMessage.success('密钥已复制')
  } catch {
    ElMessage.warning('复制失败，请手动选择复制')
  }
}

function resetTf() {
  tfSecret.value = ''
  tfOtpUri.value = ''
  tfForm.code = ''
  qrError.value = false
}

async function submitTf() {
  if (!tfFormRef.value) return
  const valid = await tfFormRef.value.validate().catch(() => false)
  if (!valid) return

  tfLoading.value = true
  try {
    if (tfMode.value === 'setup') {
      await authApi.enable2fa(tfSecret.value, tfForm.code.trim())
      ElMessage.success('双因素认证已启用')
    } else {
      await authApi.disable2fa(tfForm.code.trim())
      ElMessage.success('双因素认证已关闭')
    }
    tfVisible.value = false
    await authStore.fetchProfile()
  } catch {
    // 已由拦截器提示
  } finally {
    tfLoading.value = false
  }
}
</script>

<style scoped>
.admin-layout {
  height: 100%;
}

.layout-aside {
  background: #1f2d3d;
  transition: width 0.2s;
  overflow-x: hidden;
}

.aside-logo {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 16px;
  font-weight: 600;
  background: rgba(0, 0, 0, 0.15);
  white-space: nowrap;
}

.layout-aside :deep(.el-menu) {
  border-right: none;
}

.layout-header {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e4e7ed;
  padding: 0 16px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 14px;
}

.collapse-btn {
  font-size: 18px;
  cursor: pointer;
  color: #606266;
}

.user-info {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  outline: none;
}

.user-avatar {
  background: #2f54a0;
  color: #fff;
}

.user-name {
  font-size: 14px;
  color: #303133;
}

.layout-main {
  background: #f0f2f5;
  padding: 0;
  overflow-y: auto;
}

.role-tag {
  margin-left: 2px;
}

.tf-tip {
  margin-bottom: 16px;
}

.tf-qr-wrap {
  display: flex;
  justify-content: center;
  margin-bottom: 12px;
}

.tf-qr {
  width: 220px;
  height: 220px;
  border: 1px solid #dcdfe6;
  border-radius: 6px;
}

.tf-secret-row {
  margin-bottom: 16px;
}

.tf-secret :deep(.el-input__inner) {
  font-family: 'Consolas', 'Monaco', monospace;
  letter-spacing: 1px;
}
</style>
