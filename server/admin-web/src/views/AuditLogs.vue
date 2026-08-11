<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>操作审计日志</span>
        <el-button :loading="loading" @click="fetchList">刷新</el-button>
      </div>
    </template>

    <!-- 筛选 -->
    <el-form :inline="true" class="filter-form">
      <el-form-item label="操作名">
        <el-select
          v-model="query.action"
          placeholder="全部操作"
          clearable
          filterable
          style="width: 220px"
          @change="handleSearch"
        >
          <el-option v-for="a in actionOptions" :key="a" :label="a" :value="a" />
        </el-select>
      </el-form-item>
      <el-form-item label="管理员">
        <el-input
          v-model="query.keyword"
          placeholder="管理员名 / 路径"
          clearable
          style="width: 200px"
          @keyup.enter="handleSearch"
          @clear="handleSearch"
        />
      </el-form-item>
      <el-form-item label="状态码">
        <el-select v-model="query.statusCode" placeholder="全部" clearable style="width: 120px" @change="handleSearch">
          <el-option label="成功 (2xx)" :value="200" />
          <el-option label="失败 (4xx/5xx)" :value="500" />
        </el-select>
      </el-form-item>
      <el-form-item label="时间">
        <el-date-picker
          v-model="dateRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          value-format="YYYY-MM-DDTHH:mm:ss"
          style="width: 360px"
          @change="handleSearch"
        />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="handleSearch">查询</el-button>
        <el-button @click="resetSearch">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column prop="logId" label="ID" width="90" />
      <el-table-column prop="adminName" label="管理员" width="140" />
      <el-table-column label="操作名" min-width="200">
        <template #default="{ row }">
          <el-tag size="small" type="info" effect="plain">{{ row.action }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="path" label="请求路径" min-width="220" show-overflow-tooltip />
      <el-table-column prop="method" label="方法" width="80" />
      <el-table-column label="状态码" width="100">
        <template #default="{ row }">
          <el-tag size="small" :type="row.statusCode >= 400 ? 'danger' : 'success'">
            {{ row.statusCode }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="durationMs" label="耗时" width="100">
        <template #default="{ row }">{{ row.durationMs }} ms</template>
      </el-table-column>
      <el-table-column prop="ipAddress" label="IP" width="140" show-overflow-tooltip />
      <el-table-column label="时间" width="170">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="详情" width="80" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="openDetail(row)">查看</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="pager">
      <el-pagination
        v-model:current-page="query.page"
        v-model:page-size="query.pageSize"
        :total="total"
        :page-sizes="[10, 20, 50, 100]"
        layout="total, sizes, prev, pager, next, jumper"
        @current-change="fetchList"
        @size-change="handleSearch"
      />
    </div>

    <el-dialog v-model="detailVisible" title="操作详情" width="620px">
      <el-descriptions :column="1" border>
        <el-descriptions-item label="操作名">{{ detail.action }}</el-descriptions-item>
        <el-descriptions-item label="管理员">{{ detail.adminName }} (ID: {{ detail.adminId ?? '-' }})</el-descriptions-item>
        <el-descriptions-item label="请求">
          {{ detail.method }} {{ detail.path }}
        </el-descriptions-item>
        <el-descriptions-item label="状态码 / 耗时">
          {{ detail.statusCode }} / {{ detail.durationMs }} ms
        </el-descriptions-item>
        <el-descriptions-item label="IP">{{ detail.ipAddress ?? '-' }}</el-descriptions-item>
        <el-descriptions-item label="时间">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
        <el-descriptions-item label="参数">
          <pre class="detail-pre">{{ detail.details ?? '（无）' }}</pre>
        </el-descriptions-item>
      </el-descriptions>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { auditApi, type AdminOperationLog } from '@/api'

const loading = ref(false)
const items = ref<AdminOperationLog[]>([])
const total = ref(0)
const actionOptions = ref<string[]>([])
const dateRange = ref<[string, string] | null>(null)

const query = reactive({
  page: 1,
  pageSize: 20,
  action: undefined as string | undefined,
  keyword: '',
  statusCode: undefined as number | undefined,
})

const detailVisible = ref(false)
const detail = ref<AdminOperationLog>({} as AdminOperationLog)

async function fetchList() {
  loading.value = true
  try {
    const params: Record<string, unknown> = {
      page: query.page,
      pageSize: query.pageSize,
    }
    if (query.action) params.action = query.action
    if (query.keyword.trim()) params.keyword = query.keyword.trim()
    if (query.statusCode !== undefined) params.statusCode = query.statusCode
    if (dateRange.value) {
      // 库中存的是 UTC 时间，筛选参数需转成 UTC ISO 格式传给后端
      params.startTime = new Date(dateRange.value[0]).toISOString()
      params.endTime = new Date(dateRange.value[1]).toISOString()
    }
    const data = await auditApi.list(params)
    items.value = data.items
    total.value = Number(data.total)
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

async function fetchActions() {
  try {
    actionOptions.value = await auditApi.actions()
  } catch {
    // 忽略
  }
}

function handleSearch() {
  query.page = 1
  fetchList()
}

function resetSearch() {
  query.action = undefined
  query.keyword = ''
  query.statusCode = undefined
  dateRange.value = null
  handleSearch()
}

function openDetail(row: AdminOperationLog) {
  detail.value = row
  detailVisible.value = true
}

function formatDate(value: string): string {
  const d = new Date(value.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z')
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

onMounted(() => {
  fetchList()
  fetchActions()
})
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

.filter-form {
  margin-bottom: 8px;
}

.pager {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

.detail-pre {
  margin: 0;
  padding: 8px;
  background: #f5f7fa;
  border-radius: 4px;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 260px;
  overflow-y: auto;
}
</style>
