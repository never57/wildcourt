<template>
  <div class="dashboard">
    <!-- 统计卡片 -->
    <el-row :gutter="16">
      <el-col v-for="card in statCards" :key="card.key" :xs="12" :sm="8" :md="6">
        <el-card class="stat-card" shadow="hover">
          <div class="stat-inner">
            <div class="stat-icon" :style="{ background: card.color }">
              <el-icon :size="26"><component :is="card.icon" /></el-icon>
            </div>
            <div class="stat-meta">
              <div class="stat-value">{{ card.value }}</div>
              <div class="stat-label">{{ card.label }}</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="16" style="margin-top: 16px">
      <!-- 注册趋势 -->
      <el-col :xs="24" :md="16">
        <el-card shadow="hover">
          <template #header>近 7 日注册趋势</template>
          <div ref="trendChartRef" class="chart-box"></div>
        </el-card>
      </el-col>

      <!-- 服务器资源概览 -->
      <el-col :xs="24" :md="8">
        <el-card shadow="hover">
          <template #header>
            服务器资源
            <el-tag v-if="dockerVersion" size="small" style="margin-left: 8px" type="success">Docker {{ dockerVersion }}</el-tag>
          </template>
          <template v-if="overview">
            <div class="res-item">
              <div class="res-label">内存使用率</div>
              <el-progress :percentage="memPercent" :status="memPercent > 90 ? 'exception' : undefined" />
            </div>
            <div class="res-item">
              <div class="res-label">磁盘使用率（/）</div>
              <el-progress :percentage="diskPercent" :status="diskPercent > 90 ? 'exception' : undefined" />
            </div>
            <div class="res-item">
              <div class="res-label">系统负载</div>
              <div class="load-line">
                <span class="load-item">1min <b>{{ overview.loadAvg1.toFixed(2) }}</b></span>
                <span class="load-item">5min <b>{{ overview.loadAvg5.toFixed(2) }}</b></span>
                <span class="load-item">15min <b>{{ overview.loadAvg15.toFixed(2) }}</b></span>
              </div>
            </div>
            <el-divider />
            <div class="res-foot">
              <div>容器数：<b>{{ overview.containers.length }}</b></div>
              <div class="res-actions">
                <el-button size="small" @click="fetchOverview">刷新</el-button>
                <el-button size="small" type="primary" link @click="goMonitor">监控详情 →</el-button>
              </div>
            </div>
          </template>
          <el-skeleton v-else :rows="4" animated />
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import * as echarts from 'echarts'
import {
  DataAnalysis,
  User,
  UserFilled,
  Top,
  Coin,
  CreditCard,
  Monitor,
} from '@element-plus/icons-vue'
import { dashboardApi, serverApi, type ServerOverview } from '@/api'

const router = useRouter()
const overview = ref<ServerOverview | null>(null)
const dockerVersion = ref('')
const trendChartRef = ref<HTMLDivElement>()
let chart: echarts.ECharts | null = null

const statCards = computed(() => [
  { key: 'total', label: '玩家总数', value: summary.value?.totalPlayers ?? '-', icon: User, color: '#409eff' },
  { key: 'today', label: '今日新增', value: summary.value?.todayNewPlayers ?? '-', icon: Top, color: '#67c23a' },
  { key: 'online', label: '实时在线', value: summary.value?.onlinePlayers ?? '-', icon: UserFilled, color: '#e6a23c' },
  { key: 'active', label: '今日活跃', value: summary.value?.todayActivePlayers ?? '-', icon: DataAnalysis, color: '#f56c6c' },
  { key: 'shell', label: '贝壳存量', value: formatNum(summary.value?.totalShell ?? 0), icon: Coin, color: '#909399' },
  { key: 'coupon', label: '代金券存量', value: formatNum(summary.value?.totalCoupon ?? 0), icon: CreditCard, color: '#9c27b0' },
])

const summary = ref<{ totalPlayers: number; todayNewPlayers: number; onlinePlayers: number; todayActivePlayers: number; totalShell: number; totalCoupon: number } | null>(null)

const memPercent = computed(() => Math.round((overview.value?.memUsagePercent ?? 0) * 100))
const diskPercent = computed(() => Math.round((overview.value?.diskUsagePercent ?? 0) * 100))

function formatNum(n: number): string {
  if (n >= 1_0000_0000) return (n / 1_0000_0000).toFixed(2) + '亿'
  if (n >= 1_0000) return (n / 1_0000).toFixed(2) + '万'
  return String(n)
}

async function fetchSummary() {
  try {
    const res = await dashboardApi.summary()
    summary.value = res
    renderChart(res.registrationTrend)
  } catch {
    // 拦截器已提示
  }
}

async function fetchOverview() {
  try {
    const res = await serverApi.overview()
    overview.value = res
    dockerVersion.value = res.dockerVersion
  } catch {
    // 本地无 docker 时返回空数据
  }
}

function renderChart(trend: { date: string; count: number }[]) {
  if (!trendChartRef.value) return
  if (!chart) {
    chart = echarts.init(trendChartRef.value)
  }
  chart.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 40, right: 20, top: 30, bottom: 30 },
    xAxis: { type: 'category', data: trend.map((t) => t.date) },
    yAxis: { type: 'value', minInterval: 1 },
    series: [
      {
        name: '注册人数',
        type: 'line',
        smooth: true,
        symbolSize: 8,
        areaStyle: { opacity: 0.15 },
        itemStyle: { color: '#409eff' },
        data: trend.map((t) => t.count),
      },
    ],
  })
}

function goMonitor() {
  router.push('/server')
}

function resize() {
  chart?.resize()
}

onMounted(() => {
  fetchSummary()
  fetchOverview()
  window.addEventListener('resize', resize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', resize)
  chart?.dispose()
  chart = null
})
</script>

<style scoped>
.dashboard {
  padding: 16px;
}

.stat-inner {
  display: flex;
  align-items: center;
  gap: 14px;
}

.stat-icon {
  width: 52px;
  height: 52px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
}

.stat-value {
  font-size: 22px;
  font-weight: 600;
  color: #303133;
}

.stat-label {
  font-size: 13px;
  color: #909399;
  margin-top: 2px;
}

.chart-box {
  height: 300px;
}

.res-item {
  margin-bottom: 14px;
}

.res-label {
  font-size: 13px;
  color: #606266;
  margin-bottom: 6px;
}

.load-line {
  display: flex;
  justify-content: space-between;
}

.load-item {
  font-size: 13px;
  color: #909399;
}

.load-item b {
  color: #303133;
}

.res-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-size: 13px;
  color: #606266;
}
</style>
