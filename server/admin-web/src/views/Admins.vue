<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>管理员账号</span>
        <el-button type="primary" @click="dialogVisible = true">新建管理员</el-button>
      </div>
    </template>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column prop="adminId" label="ID" width="80" />
      <el-table-column prop="username" label="账号" min-width="140" />
      <el-table-column prop="displayName" label="显示名" min-width="140" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="最近登录" width="170">
        <template #default="{ row }">{{ row.lastLoginAt ? formatDate(row.lastLoginAt) : '从未登录' }}</template>
      </el-table-column>
      <el-table-column label="创建时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="240" fixed="right">
        <template #default="{ row }">
          <el-button
            v-if="row.isActive && row.adminId !== currentAdminId"
            link
            type="warning"
            size="small"
            @click="toggleActive(row, false)"
          >
            停用
          </el-button>
          <el-button
            v-else-if="!row.isActive"
            link
            type="success"
            size="small"
            @click="toggleActive(row, true)"
          >
            启用
          </el-button>
          <el-button link type="primary" size="small" @click="openReset(row)">重置密码</el-button>
          <el-button
            v-if="row.adminId !== currentAdminId"
            link
            type="danger"
            size="small"
            @click="remove(row)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" title="新建管理员" width="420px">
      <el-form :model="form" :rules="rules" ref="formRef" label-width="80px">
        <el-form-item label="账号" prop="username">
          <el-input v-model="form.username" placeholder="登录账号" maxlength="32" />
        </el-form-item>
        <el-form-item label="密码" prop="password">
          <el-input v-model="form.password" type="password" show-password placeholder="至少 6 位" />
        </el-form-item>
        <el-form-item label="显示名" prop="displayName">
          <el-input v-model="form.displayName" placeholder="如：运营-张三" maxlength="32" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submit">创建</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="resetVisible" title="重置密码" width="420px">
      <el-form :model="resetForm" :rules="resetRules" ref="resetFormRef" label-width="80px">
        <el-form-item label="账号">
          <span>{{ resetTarget?.username }}</span>
        </el-form-item>
        <el-form-item label="新密码" prop="password">
          <el-input v-model="resetForm.password" type="password" show-password placeholder="至少 6 位" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetVisible = false">取消</el-button>
        <el-button type="primary" :loading="resetting" @click="submitReset">确认重置</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { authApi, type AdminItem } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { sha256Hex } from '@/utils/sha256'

const authStore = useAuthStore()
const currentAdminId = computed(() => authStore.profile?.adminId ?? 0)

const items = ref<AdminItem[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)
const formRef = ref<FormInstance>()

const resetVisible = ref(false)
const resetting = ref(false)
const resetFormRef = ref<FormInstance>()
const resetTarget = ref<AdminItem | null>(null)
const resetForm = reactive({ password: '' })
const resetRules: FormRules = {
  password: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
}

const form = reactive({ username: '', password: '', displayName: '' })
const rules: FormRules = {
  username: [{ required: true, message: '请输入账号', trigger: 'blur' }],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, message: '密码至少 6 位', trigger: 'blur' },
  ],
}

async function fetchList() {
  loading.value = true
  try {
    items.value = await authApi.listAdmins()
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

async function submit() {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    await authApi.createAdmin({
      username: form.username.trim(),
      password: await sha256Hex(form.password),
      displayName: form.displayName.trim(),
    })
    ElMessage.success('创建成功')
    dialogVisible.value = false
    form.username = ''
    form.password = ''
    form.displayName = ''
    fetchList()
  } catch {
    // 拦截器提示
  } finally {
    saving.value = false
  }
}

function openReset(row: AdminItem) {
  resetTarget.value = row
  resetForm.password = ''
  resetVisible.value = true
}

async function submitReset() {
  if (!resetFormRef.value || !resetTarget.value) return
  const valid = await resetFormRef.value.validate().catch(() => false)
  if (!valid) return
  resetting.value = true
  try {
    await authApi.resetPassword(resetTarget.value.adminId, await sha256Hex(resetForm.password))
    ElMessage.success('密码已重置')
    resetVisible.value = false
  } catch {
    // 拦截器提示
  } finally {
    resetting.value = false
  }
}

async function remove(row: AdminItem) {
  try {
    await ElMessageBox.confirm(
      `确认删除管理员「${row.username}」？该操作不可恢复。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '删除' },
    )
  } catch {
    return
  }
  try {
    await authApi.removeAdmin(row.adminId)
    ElMessage.success('已删除')
    fetchList()
  } catch {
    // 拦截器提示
  }
}

async function toggleActive(row: AdminItem, active: boolean) {
  const action = active ? '启用' : '停用'
  try {
    await ElMessageBox.confirm(`确认${action}管理员「${row.username}」？${active ? '' : '停用后其登录状态将立即失效'}`, `${action}确认`, {
      type: 'warning',
    })
  } catch {
    return
  }
  try {
    await authApi.setActive(row.adminId, active)
    ElMessage.success(`已${action}`)
    fetchList()
  } catch {
    // 拦截器提示
  }
}

function formatDate(value: string): string {
  // 后端时间均为 UTC 且无时区标记（如 2026-08-10T18:13:31），补 Z 让浏览器按 UTC 解析
  const d = new Date(value.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z')
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

onMounted(fetchList)
</script>

<style scoped>
.page {
  margin: 16px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
