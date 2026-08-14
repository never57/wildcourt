<template>
  <el-card class="page" shadow="never">
    <!-- 搜索栏 -->
    <div class="toolbar">
      <el-input
        v-model="keyword"
        placeholder="搜索账号 / 昵称 / 玩家ID"
        clearable
        style="width: 260px"
        @keyup.enter="search"
        @clear="search"
      >
        <template #prefix><el-icon><Search /></el-icon></template>
      </el-input>
      <el-button type="primary" :loading="loading" @click="search">搜索</el-button>
      <el-button @click="resetSearch">重置</el-button>
      <div class="batch-actions" v-if="selectedRows.length">
        <span class="batch-count">已选 {{ selectedRows.length }} 人</span>
        <el-button
          v-if="authStore.hasPermission('players:ban')"
          type="danger"
          size="small"
          @click="batchBan"
        >批量封禁</el-button>
        <el-button
          v-if="authStore.hasPermission('players:ban')"
          type="success"
          size="small"
          @click="batchUnban"
        >批量解封</el-button>
        <el-button
          v-if="authStore.hasPermission('players:edit')"
          type="warning"
          size="small"
          @click="openBatchEdit"
        >批量改数据</el-button>
        <el-button size="small" @click="clearSelection">取消选择</el-button>
      </div>
    </div>

    <el-table :data="players" v-loading="loading" stripe @selection-change="onSelectionChange">
      <el-table-column type="selection" width="50" />
      <el-table-column prop="userId" label="ID" width="80" />
      <el-table-column prop="username" label="账号" min-width="120" show-overflow-tooltip />
      <el-table-column prop="nickname" label="昵称" min-width="120" show-overflow-tooltip />
      <el-table-column prop="level" label="等级" width="70" />
      <el-table-column prop="shell" label="贝壳" width="90" />
      <el-table-column prop="coupon" label="代金券" width="90" />
      <el-table-column prop="exp" label="经验" width="90" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag v-if="row.isBanned" type="danger" size="small">已封禁</el-tag>
          <el-tag v-else type="success" size="small">正常</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="注册时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="lastLogin" label="最后登录" width="170">
        <template #default="{ row }">{{ row.lastLogin ? formatDate(row.lastLogin) : '-' }}</template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="openDetail(row)">详情</el-button>
          <el-button v-if="authStore.hasPermission('players:edit')" link type="warning" size="small" @click="openEdit(row)">改数据</el-button>
          <template v-if="authStore.hasPermission('players:ban')">
            <el-button v-if="!row.isBanned" link type="danger" size="small" @click="ban(row)">封禁</el-button>
            <el-button v-else link type="success" size="small" @click="unban(row)">解封</el-button>
          </template>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      class="pagination"
      background
      layout="total, sizes, prev, pager, next"
      :total="total"
      v-model:current-page="page"
      v-model:page-size="pageSize"
      :page-sizes="[10, 20, 50, 100]"
      @current-change="fetchList"
      @size-change="fetchList"
    />

    <!-- 玩家详情抽屉 -->
    <el-drawer v-model="detailVisible" :title="`玩家详情：${detail?.player.nickname || detail?.player.username || ''}`" size="46%">
      <template v-if="detail">
        <el-descriptions title="基本信息" :column="2" border>
          <el-descriptions-item label="ID">{{ detail.player.userId }}</el-descriptions-item>
          <el-descriptions-item label="账号">{{ detail.player.username }}</el-descriptions-item>
          <el-descriptions-item label="昵称">{{ detail.player.nickname }}</el-descriptions-item>
          <el-descriptions-item label="头像">
            <el-avatar v-if="detail.player.avatarUrl" :src="detail.player.avatarUrl" :size="40" />
            <span v-else>-</span>
          </el-descriptions-item>
          <el-descriptions-item label="等级">{{ detail.player.level }}</el-descriptions-item>
          <el-descriptions-item label="贝壳">{{ detail.player.shell }}</el-descriptions-item>
          <el-descriptions-item label="代金券">{{ detail.player.coupon }}</el-descriptions-item>
          <el-descriptions-item label="经验">{{ detail.player.exp }}</el-descriptions-item>
          <el-descriptions-item label="好友数">{{ detail.friendCount }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="detail.player.isBanned ? 'danger' : 'success'" size="small">
              {{ detail.player.isBanned ? '已封禁' : '正常' }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="注册时间">{{ formatDate(detail.player.createdAt) }}</el-descriptions-item>
        </el-descriptions>

        <el-divider content-position="left">宠物（{{ detail.pets.length }}）</el-divider>
        <el-table :data="detail.pets" size="small" v-if="detail.pets.length">
          <el-table-column prop="petId" label="宠物ID" width="90" />
          <el-table-column prop="type" label="类型" width="110" />
          <el-table-column prop="level" label="等级" width="70" />
          <el-table-column prop="exp" label="经验" width="90" />
          <el-table-column prop="status" label="状态" />
        </el-table>
        <el-empty v-else description="暂无宠物" :image-size="60" />

        <el-divider content-position="left">背包（{{ detail.inventory.length }}）</el-divider>
        <el-table :data="detail.inventory" size="small" v-if="detail.inventory.length">
          <el-table-column prop="itemId" label="物品ID" />
          <el-table-column prop="count" label="数量" width="100" />
        </el-table>
        <el-empty v-else description="背包为空" :image-size="60" />
      </template>
    </el-drawer>

    <!-- 修改数据弹窗 -->
    <el-dialog v-model="editVisible" :title="`修改玩家数据：${editing?.nickname || editing?.username || ''}`" width="480px">
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="90px">
        <el-form-item label="昵称" prop="nickname">
          <el-input v-model="editForm.nickname" maxlength="32" placeholder="玩家昵称" />
        </el-form-item>
        <el-form-item label="头像地址">
          <el-input v-model="editForm.avatarUrl" placeholder="https://…（留空表示清除头像）" clearable />
        </el-form-item>
        <el-divider content-position="left">数值（覆盖设置）</el-divider>
        <el-form-item label="贝壳">
          <el-input-number v-model="editForm.shell" :min="0" :max="99999999" style="width: 100%" />
        </el-form-item>
        <el-form-item label="代金券">
          <el-input-number v-model="editForm.coupon" :min="0" :max="99999999" style="width: 100%" />
        </el-form-item>
        <el-form-item label="等级">
          <el-input-number v-model="editForm.level" :min="1" :max="99" style="width: 100%" />
        </el-form-item>
        <el-form-item label="经验">
          <el-input-number v-model="editForm.exp" :min="0" :max="9999999" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" :loading="editLoading" @click="submitEdit">保存</el-button>
      </template>
    </el-dialog>

    <!-- 批量改数据弹窗 -->
    <el-dialog v-model="batchEditVisible" :title="`批量修改玩家数据（已选 ${selectedRows.length} 人）`" width="480px">
      <el-alert type="warning" :closable="false" show-icon class="batch-tip">
        数值为覆盖设置；留空表示不修改该项。
      </el-alert>
      <el-form label-width="90px">
        <el-form-item label="贝壳">
          <el-input-number v-model="batchEditForm.shell" :min="0" :max="99999999" style="width: 100%" placeholder="留空不修改" />
        </el-form-item>
        <el-form-item label="代金券">
          <el-input-number v-model="batchEditForm.coupon" :min="0" :max="99999999" style="width: 100%" placeholder="留空不修改" />
        </el-form-item>
        <el-form-item label="等级">
          <el-input-number v-model="batchEditForm.level" :min="1" :max="99" style="width: 100%" placeholder="留空不修改" />
        </el-form-item>
        <el-form-item label="经验">
          <el-input-number v-model="batchEditForm.exp" :min="0" :max="9999999" style="width: 100%" placeholder="留空不修改" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="batchEditVisible = false">取消</el-button>
        <el-button type="primary" :loading="batchEditLoading" @click="submitBatchEdit">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import { playerApi, type PlayerDetail, type PlayerItem, type BatchOperationResult } from '@/api'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()

const keyword = ref('')
const players = ref<PlayerItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(20)
const loading = ref(false)

async function fetchList() {
  loading.value = true
  try {
    const res = await playerApi.list({ keyword: keyword.value || undefined, page: page.value, pageSize: pageSize.value })
    players.value = res.items
    total.value = res.total
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

function search() {
  page.value = 1
  fetchList()
}

function resetSearch() {
  keyword.value = ''
  page.value = 1
  fetchList()
}

// ---- 详情 ----
const detailVisible = ref(false)
const detail = ref<PlayerDetail | null>(null)

async function openDetail(row: PlayerItem) {
  try {
    detail.value = await playerApi.detail(row.userId)
    detailVisible.value = true
  } catch {
    // 拦截器提示
  }
}

// ---- 改数据 ----
const editVisible = ref(false)
const editLoading = ref(false)
const editing = ref<PlayerItem | null>(null)
const editFormRef = ref<FormInstance>()
const editRules: FormRules = {
  nickname: [{ required: true, message: '昵称不能为空', trigger: 'blur' }],
}
const editForm = reactive({ nickname: '', avatarUrl: '', shell: 0, coupon: 0, level: 1, exp: 0 })

function openEdit(row: PlayerItem) {
  editing.value = row
  editForm.nickname = row.nickname
  editForm.avatarUrl = row.avatarUrl || ''
  editForm.shell = row.shell
  editForm.coupon = row.coupon
  editForm.level = row.level
  editForm.exp = row.exp
  editVisible.value = true
}

async function submitEdit() {
  if (!editing.value) return
  const valid = await editFormRef.value?.validate().catch(() => false)
  if (!valid) return
  editLoading.value = true
  try {
    await playerApi.updateProfile(editing.value.userId, {
      nickname: editForm.nickname,
      avatarUrl: editForm.avatarUrl,
    })
    await playerApi.updateCurrency(editing.value.userId, {
      shell: editForm.shell,
      coupon: editForm.coupon,
      level: editForm.level,
      exp: editForm.exp,
    })
    ElMessage.success('修改成功')
    editVisible.value = false
    fetchList()
  } catch {
    // 拦截器提示
  } finally {
    editLoading.value = false
  }
}

// ---- 封禁/解封 ----
async function ban(row: PlayerItem) {
  try {
    await ElMessageBox.confirm(
      `封禁后玩家「${row.nickname}」将无法登录且被踢下线，确认封禁？`,
      '封禁确认',
      { type: 'warning', confirmButtonText: '封禁', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    await playerApi.ban(row.userId)
    ElMessage.success('已封禁')
    fetchList()
  } catch {
    // 拦截器提示
  }
}

async function unban(row: PlayerItem) {
  try {
    await playerApi.unban(row.userId)
    ElMessage.success('已解封')
    fetchList()
  } catch {
    // 拦截器提示
  }
}

// ---- 批量操作 ----
const selectedRows = ref<PlayerItem[]>([])

function onSelectionChange(rows: PlayerItem[]) {
  selectedRows.value = rows
}

function clearSelection() {
  selectedRows.value = []
}

function reportBatchResult(action: string, result: BatchOperationResult) {
  if (result.failed > 0) {
    ElMessage.warning(
      `${action}完成：成功 ${result.succeeded}，失败 ${result.failed}。` +
      result.failures.slice(0, 5).map(f => `#${f.userId}(${f.reason})`).join('、') +
      (result.failures.length > 5 ? ` 等 ${result.failures.length} 项` : ''),
    )
  } else {
    ElMessage.success(`${action}完成：成功 ${result.succeeded} 人`)
  }
}

async function batchBan() {
  const userIds = selectedRows.value.map(r => r.userId)
  try {
    await ElMessageBox.confirm(
      `将批量封禁 ${userIds.length} 名玩家并踢下线，确认？`,
      '批量封禁确认',
      { type: 'warning', confirmButtonText: '封禁', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    const result = await playerApi.batchBan(userIds)
    reportBatchResult('批量封禁', result)
    fetchList()
  } catch {
    // 拦截器提示
  }
}

async function batchUnban() {
  const userIds = selectedRows.value.map(r => r.userId)
  try {
    await ElMessageBox.confirm(
      `将批量解封 ${userIds.length} 名玩家，确认？`,
      '批量解封确认',
      { type: 'warning', confirmButtonText: '解封', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    const result = await playerApi.batchUnban(userIds)
    reportBatchResult('批量解封', result)
    fetchList()
  } catch {
    // 拦截器提示
  }
}

// ---- 批量改数据 ----
const batchEditVisible = ref(false)
const batchEditLoading = ref(false)
const batchEditForm = reactive<{ shell?: number; coupon?: number; level?: number; exp?: number }>({})

function openBatchEdit() {
  batchEditForm.shell = undefined
  batchEditForm.coupon = undefined
  batchEditForm.level = undefined
  batchEditForm.exp = undefined
  batchEditVisible.value = true
}

async function submitBatchEdit() {
  const userIds = selectedRows.value.map(r => r.userId)
  if (Object.values(batchEditForm).every(v => v === undefined)) {
    ElMessage.warning('请至少填写一个要修改的字段')
    return
  }
  batchEditLoading.value = true
  try {
    const result = await playerApi.batchUpdateCurrency({
      userIds,
      shell: batchEditForm.shell,
      coupon: batchEditForm.coupon,
      level: batchEditForm.level,
      exp: batchEditForm.exp,
    })
    reportBatchResult('批量改数据', result)
    batchEditVisible.value = false
    fetchList()
  } catch {
    // 拦截器提示
  } finally {
    batchEditLoading.value = false
  }
}

function formatDate(value?: string): string {
  if (!value) return '-'
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

.toolbar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.batch-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-left: auto;
}

.batch-count {
  color: #606266;
  font-size: 13px;
}

.batch-tip {
  margin-bottom: 16px;
}

.pagination {
  margin-top: 16px;
  justify-content: flex-end;
}
</style>
