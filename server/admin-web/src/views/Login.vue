<template>
  <div class="login-page">
    <el-card class="login-card" shadow="always">
      <div class="login-header">
        <div class="login-logo">WC</div>
        <h1>{{ totpStep ? '双因素认证' : 'WildCourt 运营管理系统' }}</h1>
        <p>{{ totpStep ? '请输入身份验证器中的动态验证码' : '游戏服务器管理后台' }}</p>
      </div>

      <!-- 第一步：账号密码 + 验证码 -->
      <el-form v-if="!totpStep" ref="formRef" :model="form" :rules="rules" size="large" @keyup.enter="onSubmit">
        <el-form-item prop="username">
          <el-input v-model="form.username" placeholder="管理员账号" :prefix-icon="User" clearable />
        </el-form-item>
        <el-form-item prop="password">
          <el-input
            v-model="form.password"
            type="password"
            placeholder="密码"
            :prefix-icon="Lock"
            show-password
          />
        </el-form-item>
        <el-form-item prop="captchaCode">
          <div class="captcha-row">
            <el-input v-model="form.captchaCode" placeholder="验证码" :prefix-icon="Key" maxlength="4" />
            <img
              v-if="captchaImage"
              :src="captchaImage"
              class="captcha-img"
              alt="验证码"
              title="点击刷新"
              @click="loadCaptcha"
            />
          </div>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" class="login-btn" :loading="loading" @click="onSubmit">
            登 录
          </el-button>
        </el-form-item>
      </el-form>

      <!-- 第二步：动态验证码 -->
      <el-form v-else ref="totpFormRef" :model="totpForm" :rules="totpRules" size="large" @keyup.enter="onTotpSubmit">
        <el-alert type="info" :closable="false" show-icon class="totp-tip">
          账号已启用双因素认证，请输入身份验证器（如 Google Authenticator）中的 6 位动态码
        </el-alert>
        <el-form-item prop="totpCode">
          <el-input
            v-model="totpForm.totpCode"
            placeholder="6 位动态验证码"
            :prefix-icon="Key"
            maxlength="6"
            inputmode="numeric"
            class="totp-input"
          />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" class="login-btn" :loading="loading" @click="onTotpSubmit">
            验 证
          </el-button>
          <el-button text class="back-btn" :disabled="loading" @click="backToLogin">返回上一步</el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Key, Lock, User } from '@element-plus/icons-vue'
import { useAuthStore } from '@/stores/auth'
import { authApi } from '@/api'
import { sha256Hex } from '@/utils/sha256'

const router = useRouter()
const authStore = useAuthStore()

const formRef = ref<FormInstance>()
const totpFormRef = ref<FormInstance>()
const loading = ref(false)
const captchaId = ref('')
const captchaImage = ref('')
const totpStep = ref(false)
const totpTicket = ref('')
let passwordHash = ''

const form = reactive({ username: '', password: '', captchaCode: '' })
const totpForm = reactive({ totpCode: '' })

const rules: FormRules = {
  username: [{ required: true, message: '请输入管理员账号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  captchaCode: [{ required: true, message: '请输入验证码', trigger: 'blur' }],
}

const totpRules: FormRules = {
  totpCode: [
    { required: true, message: '请输入动态验证码', trigger: 'blur' },
    { pattern: /^\d{6}$/, message: '请输入 6 位数字动态码', trigger: 'blur' },
  ],
}

async function loadCaptcha() {
  try {
    const res = await authApi.captcha()
    captchaId.value = res.captchaId
    captchaImage.value = res.image
    form.captchaCode = ''
  } catch {
    // 错误提示已由拦截器处理
  }
}

async function onSubmit() {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    // 与玩家登录约定一致：客户端 SHA256 一次哈希后传输
    passwordHash = await sha256Hex(form.password)
    const res = await authStore.login({
      username: form.username.trim(),
      password: passwordHash,
      captchaId: captchaId.value,
      captchaCode: form.captchaCode.trim(),
    })
    if (res.needTotp) {
      totpTicket.value = res.totpTicket || ''
      totpStep.value = true
      // 验证码已消费，刷新一个新的备用
      loadCaptcha()
      return
    }
    ElMessage.success('登录成功')
    router.push('/dashboard')
  } catch {
    loadCaptcha()
  } finally {
    loading.value = false
  }
}

async function onTotpSubmit() {
  if (!totpFormRef.value) return
  const valid = await totpFormRef.value.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await authStore.login({
      username: form.username.trim(),
      password: passwordHash,
      totpCode: totpForm.totpCode.trim(),
      totpTicket: totpTicket.value,
    })
    ElMessage.success('登录成功')
    router.push('/dashboard')
  } catch {
    totpForm.totpCode = ''
  } finally {
    loading.value = false
  }
}

function backToLogin() {
  totpStep.value = false
  totpTicket.value = ''
  totpForm.totpCode = ''
  loadCaptcha()
}

onMounted(loadCaptcha)
</script>

<style scoped>
.login-page {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #1f2d3d 0%, #2f54a0 60%, #3b82f6 100%);
}

.login-card {
  width: 400px;
  border-radius: 12px;
  padding: 8px 12px;
}

.login-header {
  text-align: center;
  margin-bottom: 24px;
}

.login-logo {
  width: 56px;
  height: 56px;
  line-height: 56px;
  margin: 0 auto 12px;
  border-radius: 14px;
  background: linear-gradient(135deg, #2f54a0, #3b82f6);
  color: #fff;
  font-size: 24px;
  font-weight: 700;
}

.login-header h1 {
  font-size: 18px;
  color: #303133;
}

.login-header p {
  margin-top: 6px;
  font-size: 13px;
  color: #909399;
}

.login-btn {
  width: 100%;
}

.captcha-row {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
}

.captcha-row .el-input {
  flex: 1;
}

.captcha-img {
  width: 120px;
  height: 40px;
  border-radius: 4px;
  border: 1px solid #dcdfe6;
  cursor: pointer;
  flex-shrink: 0;
}

.totp-tip {
  margin-bottom: 18px;
}

.totp-input {
  margin-top: 18px;
}

.back-btn {
  width: 100%;
  margin-top: 4px;
}
</style>
