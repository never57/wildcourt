<template>
  <div class="page">
    <el-row :gutter="16">
      <el-col :xs="24" :sm="12" :md="6">
        <el-card shadow="hover">
          <div class="mini-stat">
            <div class="mini-label">内存</div>
            <el-progress :percentage="memPercent" :status="memPercent > 90 ? 'exception' : undefined" />
            <div class="mini-desc">{{ formatBytes(overview?.memUsed) }} / {{ formatBytes(overview?.memTotal) }}</div>
          </div>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="12" :md="6">
        <el-card shadow="hover">
          <div class="mini-stat">
            <div class="mini-label">磁盘（{{ overview?.diskMountPoint || '/' }}）</div>
            <el-progress :percentage="diskPercent" :status="diskPercent > 90 ? 'exception' : undefined" />
            <div class="mini-desc">{{ formatBytes(overview?.diskUsed) }} / {{ formatBytes(overview?.diskTotal) }}</div>
          </div>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="12" :md="6">
        <el-card shadow="hover">
          <div class="mini-stat">
            <div class="mini-label">系统负载</div>
            <div class="load-line">
              <span>1min <b>{{ overview?.loadAvg1.toFixed(2) ?? '-' }}</b></span>
              <span>5min <b>{{ overview?.loadAvg5.toFixed(2) ?? '-' }}</b></span>
              <span>15min <b>{{ overview?.loadAvg15.toFixed(2) ?? '-' }}</b></span>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :xs="24" :sm="12" :md="6">
        <el-card shadow="hover">
          <div class="mini-stat">
            <div class="mini-label">宿主机</div>
            <div class="host-info">
              <div class="host-name">{{ overview?.hostName || '-' }}</div>
              <div class="host-ver">Docker {{ overview?.dockerVersion || '-' }}</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-card class="mt16" shadow="hover">
      <template #header>
        <div class="card-header">
          <span>容器列表</span>
          <div>
            <el-switch v-model="autoRefresh" active-text="自动刷新(30s)" style="margin-right: 12px" />
            <el-button type="primary" :loading="loading" @click="fetchAll">刷新</el-button>
          </div>
        </div>
      </template>

      <el-table :data="containers" v-loading="loading" stripe>
        <el-table-column label="容器名" min-width="160">
          <template #default="{ row }">
            <span class="container-name">{{ row.name }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="image" label="镜像" min-width="180" show-overflow-tooltip />
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="stateType(row.state)" size="small">{{ row.state }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="CPU" width="90">
          <template #default="{ row }">
            <span v-if="row.cpuPercent != null">{{ row.cpuPercent.toFixed(2) }}%</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="内存" width="140">
          <template #default="{ row }">
            <span v-if="row.memUsage != null">{{ formatBytes(row.memUsage) }} / {{ formatBytes(row.memLimit) }}</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column prop="runningFor" label="运行时长" width="140" show-overflow-tooltip />
        <el-table-column prop="ports" label="端口映射" min-width="140" show-overflow-tooltip />
        <el-table-column label="操作" width="140" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openLogs(row)">日志</el-button>
            <el-popconfirm title="确认重启该容器？" @confirm="restart(row)">
              <template #reference>
                <el-button link type="warning" size="small">重启</el-button>
              </template>
            </el-popconfirm>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 容器日志抽屉 -->
    <el-drawer v-model="logVisible" :title="`容器日志：${logContainer}`" size="60%">
      <div class="log-toolbar">
        <el-select v-model="logTail" style="width: 120px">
          <el-option :value="100" label="100 行" />
          <el-option :value="300" label="300 行" />
          <el-option :value="1000" label="1000 行" />
        </el-select>
        <el-button type="primary" :loading="logLoading" @click="fetchLogs">刷新</el-button>
      </div>
      <pre class="log-box">{{ logContent || '暂无日志' }}</pre>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { serverApi, type ContainerInfo, type ServerOverview } from '@/api'

const overview = ref<ServerOverview | null>(null)
const containers = ref<ContainerInfo[]>([])
const loading = ref(false)
const autoRefresh = ref(false)

const memPercent = computed(() => Math.round((overview.value?.memUsagePercent ?? 0) * 100))
const diskPercent = computed(() => Math.round((overview.value?.diskUsagePercent ?? 0) * 100))

async function fetchAll() {
  loading.value = true
  try {
    const res = await serverApi.overview()
    overview.value = res
    containers.value = res.containers
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

function stateType(state: string): 'success' | 'info' | 'warning' | 'danger' {
  switch (state) {
    case 'running': return 'success'
    case 'exited': return 'info'
    case 'restarting': return 'warning'
    default: return 'danger'
  }
}

function formatBytes(bytes?: number | null): string {
  if (!bytes || bytes <= 0) return '-'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let v = bytes
  let i = 0
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024
    i++
  }
  return `${v.toFixed(1)} ${units[i]}`
}

// ---- 日志 ----
const logVisible = ref(false)
const logLoading = ref(false)
const logContainer = ref('')
const logTail = ref(300)
const logContent = ref('')

async function openLogs(row: ContainerInfo) {
  logContainer.value = row.name
  logVisible.value = true
  await fetchLogs()
}

async function fetchLogs() {
  logLoading.value = true
  try {
    const res = await serverApi.logs(logContainer.value, logTail.value)
    logContent.value = res.log
  } catch {
    // 拦截器提示
  } finally {
    logLoading.value = false
  }
}

async function restart(row: ContainerInfo) {
  try {
    await serverApi.restart(row.name)
    ElMessage.success('重启命令已发送')
    setTimeout(fetchAll, 2000)
  } catch {
    // 拦截器提示
  }
}

let timer: number | undefined
onMounted(() => {
  fetchAll()
  timer = window.setInterval(() => {
    if (autoRefresh.value) fetchAll()
  }, 30000)
})

onBeforeUnmount(() => {
  if (timer) window.clearInterval(timer)
})
</script>

<style scoped>
.page {
  padding: 16px;
}

.mt16 {
  margin-top: 16px;
}

.mini-label {
  font-size: 13px;
  color: #909399;
  margin-bottom: 8px;
}

.mini-desc {
  margin-top: 6px;
  font-size: 12px;
  color: #909399;
}

.load-line {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  color: #909399;
}

.load-line b {
  color: #303133;
}

.host-name {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.host-ver {
  margin-top: 4px;
  font-size: 12px;
  color: #909399;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.container-name {
  font-weight: 500;
}

.log-toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 12px;
}

.log-box {
  height: calc(100vh - 180px);
  overflow: auto;
  background: #1e1e1e;
  color: #d4d4d4;
  border-radius: 6px;
  padding: 12px;
  font-size: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
