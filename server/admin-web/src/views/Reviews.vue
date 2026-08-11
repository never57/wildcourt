<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>信件审核队列</span>
        <el-button type="primary" :loading="loading" @click="fetchQueue">刷新</el-button>
      </div>
    </template>

    <el-empty v-if="!loading && items.length === 0" description="暂无待审核信件" />

    <el-table v-else :data="items" v-loading="loading" stripe>
      <el-table-column prop="letterId" label="信件ID" width="90" />
      <el-table-column label="发件人" width="140">
        <template #default="{ row }">
          {{ row.fromNickname }} <span class="muted">(#{{ row.fromUser }})</span>
        </template>
      </el-table-column>
      <el-table-column label="收件人" width="140">
        <template #default="{ row }">
          {{ row.toNickname }} <span class="muted">(#{{ row.toUser }})</span>
        </template>
      </el-table-column>
      <el-table-column label="内容" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          <el-link type="primary" @click="viewDetail(row)">{{ row.content }}</el-link>
        </template>
      </el-table-column>
      <el-table-column prop="itemId" label="附件" width="110">
        <template #default="{ row }">{{ row.itemId || '-' }}</template>
      </el-table-column>
      <el-table-column label="时间" width="160">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column v-if="canEdit" label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button link type="success" size="small" @click="review(row, true)">通过</el-button>
          <el-button link type="danger" size="small" @click="review(row, false)">拒绝</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 信件详情 -->
    <el-dialog v-model="detailVisible" :title="`信件详情 #${current?.letterId ?? ''}`" width="480px">
      <el-descriptions :column="2" border>
        <el-descriptions-item label="发件人">{{ current?.fromNickname }}</el-descriptions-item>
        <el-descriptions-item label="收件人">{{ current?.toNickname }}</el-descriptions-item>
        <el-descriptions-item label="附件">{{ current?.itemId || '无' }}</el-descriptions-item>
        <el-descriptions-item label="发送时间">{{ current ? formatDate(current.createdAt) : '' }}</el-descriptions-item>
      </el-descriptions>
      <div class="letter-content">{{ current?.content }}</div>
      <template v-if="canEdit" #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
        <el-button type="success" @click="review(current!, true)">通过</el-button>
        <el-button type="danger" @click="review(current!, false)">拒绝</el-button>
      </template>
      <template v-else #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reviewApi, type ReviewQueueItem } from '@/api'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const canEdit = computed(() => authStore.hasPermission('reviews:edit'))

const items = ref<ReviewQueueItem[]>([])
const loading = ref(false)
const detailVisible = ref(false)
const current = ref<ReviewQueueItem | null>(null)

async function fetchQueue() {
  loading.value = true
  try {
    items.value = await reviewApi.queue()
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

function viewDetail(row: ReviewQueueItem) {
  current.value = row
  detailVisible.value = true
}

async function review(row: ReviewQueueItem, approve: boolean) {
  const action = approve ? '通过' : '拒绝'
  try {
    await ElMessageBox.confirm(`确认${action}信件 #${row.letterId}？${approve ? '' : '拒绝后发件人无法重发'}`, `${action}确认`, {
      type: approve ? 'success' : 'warning',
      confirmButtonText: action,
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  try {
    await reviewApi.review(row.letterId, approve)
    ElMessage.success(`已${action}`)
    detailVisible.value = false
    fetchQueue()
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

onMounted(fetchQueue)
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

.muted {
  color: #909399;
  font-size: 12px;
}

.letter-content {
  margin-top: 16px;
  padding: 12px;
  background: #f5f7fa;
  border-radius: 6px;
  white-space: pre-wrap;
  word-break: break-all;
  line-height: 1.7;
}
</style>
