<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>举报审理</span>
        <div class="header-right">
          <el-radio-group v-model="statusFilter" size="small" @change="fetchList">
            <el-radio-button :label="0">待审理</el-radio-button>
            <el-radio-button :label="1">已处理</el-radio-button>
          </el-radio-group>
          <el-button type="primary" :loading="loading" @click="fetchList">刷新</el-button>
        </div>
      </div>
    </template>

    <el-empty v-if="!loading && items.length === 0" description="暂无举报" />

    <el-table v-else :data="items" v-loading="loading" stripe>
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column label="举报人" width="150">
        <template #default="{ row }">
          {{ row.reporterNickname }} <span class="muted">(#{{ row.reporterUserId }})</span>
        </template>
      </el-table-column>
      <el-table-column label="被举报人" width="150">
        <template #default="{ row }">
          {{ row.reportedNickname }} <span class="muted">(#{{ row.reportedUserId }})</span>
        </template>
      </el-table-column>
      <el-table-column label="举报理由" min-width="200" show-overflow-tooltip>
        <template #default="{ row }">{{ row.reason }}</template>
      </el-table-column>
      <el-table-column label="时间" width="160">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.status === 0 ? 'warning' : 'success'">
            {{ row.status === 0 ? '待审理' : '已处理' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="viewDetail(row)">详情</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-if="total > pageSize"
      class="pager"
      background
      layout="prev, pager, next, total"
      :total="total"
      :page-size="pageSize"
      :current-page="page"
      @current-change="onPageChange"
    />

    <!-- 举报详情 -->
    <el-dialog v-model="detailVisible" :title="`举报详情 #${current?.id ?? ''}`" width="720px" top="6vh">
      <template v-if="current">
        <el-descriptions :column="2" border class="mb">
          <el-descriptions-item label="举报人">{{ current.reporterNickname }} (#{{ current.reporterUserId }})</el-descriptions-item>
          <el-descriptions-item label="被举报人">{{ current.reportedNickname }} (#{{ current.reportedUserId }})</el-descriptions-item>
          <el-descriptions-item label="举报时间">{{ formatDate(current.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="current.status === 0 ? 'warning' : 'success'">{{ current.status === 0 ? '待审理' : '已处理' }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="被举报人聊天封禁" :span="2">
            <span v-if="current.chatBan.banned" class="ban-active">
              封禁中，剩余 {{ current.chatBan.remainDays }} 天（至 {{ formatDate(current.chatBan.banUntil ?? '') }}）
            </span>
            <span v-else class="ban-none">未封禁</span>
          </el-descriptions-item>
          <el-descriptions-item label="举报理由" :span="2">
            {{ current.reason || '（无）' }}
          </el-descriptions-item>
        </el-descriptions>

        <div class="chat-title">两人全部聊天记录</div>
        <div class="chat-list">
          <el-empty v-if="current.chatMessages.length === 0" description="无聊天记录" :image-size="48" />
          <div
            v-for="m in current.chatMessages"
            :key="m.id"
            class="chat-item"
            :class="m.fromUserId === current.reportedUserId ? 'chat-reported' : 'chat-reporter'"
          >
            <span class="chat-who">
              {{ m.fromUserId === current.reportedUserId ? `被举报人(#${m.fromUserId})` : `举报人(#${m.fromUserId})` }}
            </span>
            <span class="chat-content">{{ m.content }}</span>
            <span class="chat-time">{{ formatTime(m.createdAt) }}</span>
          </div>
        </div>
      </template>

      <template v-if="canEdit" #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
        <el-button type="warning" :disabled="!current?.chatBan.banned" @click="unbanChat(current!)">解封聊天</el-button>
        <el-button type="danger" @click="openBanDialog">封禁聊天并处理</el-button>
      </template>
      <template v-else #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 封禁聊天弹窗 -->
    <el-dialog v-model="banVisible" title="封禁被举报人聊天" width="400px" :close-on-click-modal="false">
      <el-form label-width="90px">
        <el-form-item label="封禁天数">
          <el-input-number v-model="banDays" :min="1" :max="365" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="banNote" type="textarea" :rows="2" placeholder="可选" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="banVisible = false">取消</el-button>
        <el-button type="danger" :loading="banLoading" @click="confirmBan">确认封禁并处理</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reportsApi, type AdminReportItem, type AdminReportDetail } from '@/api'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const canEdit = computed(() => authStore.hasPermission('reports:edit'))

const items = ref<AdminReportItem[]>([])
const loading = ref(false)
const total = ref(0)
const page = ref(1)
const pageSize = 20
const statusFilter = ref(0)

const detailVisible = ref(false)
const current = ref<AdminReportDetail | null>(null)

const banVisible = ref(false)
const banDays = ref(7)
const banNote = ref('')
const banLoading = ref(false)

async function fetchList() {
  loading.value = true
  try {
    const res = await reportsApi.list({ status: statusFilter.value, page: page.value, pageSize })
    items.value = res.items
    total.value = res.total
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

function onPageChange(p: number) {
  page.value = p
  fetchList()
}

async function viewDetail(row: AdminReportItem) {
  try {
    const res = await reportsApi.detail(row.id)
    current.value = res
    detailVisible.value = true
  } catch {
    // 拦截器提示
  }
}

async function unbanChat(row: AdminReportDetail) {
  try {
    await ElMessageBox.confirm(`确认解封 ${row.reportedNickname}(#${row.reportedUserId}) 的聊天功能？`, '解封确认', {
      type: 'warning',
      confirmButtonText: '解封',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await reportsApi.chatUnban(row.reportedUserId)
    ElMessage.success('已解封聊天')
    await refreshDetail(row.id)
  } catch {
    // 拦截器提示
  }
}

function openBanDialog() {
  banDays.value = 7
  banNote.value = ''
  banVisible.value = true
}

async function confirmBan() {
  if (!current.value) return
  banLoading.value = true
  try {
    await reportsApi.resolve(current.value.id, { chatBanDays: banDays.value, note: banNote.value || undefined })
    ElMessage.success(`已封禁聊天 ${banDays.value} 天并标记处理`)
    banVisible.value = false
    await refreshDetail(current.value.id)
    fetchList()
  } catch {
    // 拦截器提示
  } finally {
    banLoading.value = false
  }
}

async function refreshDetail(id: number) {
  try {
    current.value = await reportsApi.detail(id)
  } catch {
    // ignore
  }
}

function formatDate(value: string): string {
  if (!value) return '-'
  const d = new Date(value.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z')
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function formatTime(ms: number): string {
  const d = new Date(ms)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
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

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.muted {
  color: #909399;
  font-size: 12px;
}

.mb {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  justify-content: flex-end;
}

.chat-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.chat-list {
  max-height: 320px;
  overflow-y: auto;
  border: 1px solid #ebeef5;
  border-radius: 6px;
  padding: 8px;
  background: #fafafa;
}

.chat-item {
  display: flex;
  align-items: baseline;
  gap: 8px;
  padding: 4px 8px;
  border-radius: 4px;
  margin-bottom: 4px;
  font-size: 13px;
}

.chat-reporter {
  background: #ecf5ff;
}

.chat-reported {
  background: #fef0f0;
}

.chat-who {
  min-width: 110px;
  color: #606266;
  font-size: 12px;
}

.chat-content {
  flex: 1;
  word-break: break-all;
}

.chat-time {
  color: #909399;
  font-size: 12px;
  white-space: nowrap;
}

.ban-active {
  color: #f56c6c;
}

.ban-none {
  color: #67c23a;
}
</style>
