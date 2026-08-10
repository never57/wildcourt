<template>
  <div class="page">
    <el-row :gutter="16">
      <!-- 左：部署配置与操作 -->
      <el-col :xs="24" :md="10">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span>服务端部署</span>
              <el-tag :type="status?.building ? 'warning' : 'success'" size="small">
                {{ status?.building ? '构建中...' : '空闲' }}
              </el-tag>
            </div>
          </template>

          <el-descriptions :column="1" border>
            <el-descriptions-item label="代码目录">{{ status?.hostCodeRoot || '-' }}</el-descriptions-item>
            <el-descriptions-item label="Compose 文件">{{ status?.composeFile || '-' }}</el-descriptions-item>
            <el-descriptions-item label="上次部署时间">
              {{ status?.lastDeployAt ? formatDate(status.lastDeployAt) : '从未部署' }}
            </el-descriptions-item>
            <el-descriptions-item label="上次结果">
              <span :class="isResultError(status?.lastDeployResult) ? 'result-error' : ''">
                {{ status?.lastDeployResult || '-' }}
              </span>
            </el-descriptions-item>
          </el-descriptions>

          <el-divider />

          <div class="upload-area">
            <div class="upload-title">1. 上传服务端代码包（zip）</div>
            <el-upload
              drag
              :show-file-list="false"
              accept=".zip,application/zip,application/x-zip-compressed"
              :before-upload="handleUpload"
            >
              <el-icon size="40" color="#909399"><UploadFilled /></el-icon>
              <div class="el-upload__text">拖拽 zip 到这里，或 <em>点击选择</em></div>
              <template #tip>
                <div class="el-upload__tip">
                  压缩包需为 server 目录结构（含 Dockerfile / docker-compose.yml），
                  解压后覆盖宿主机 {{ status?.hostCodeRoot || '/opt/wildcourt/server' }}
                </div>
              </template>
            </el-upload>
            <div v-if="uploadResult" class="upload-result">
              <el-tag type="success" size="small">{{ uploadResult }}</el-tag>
            </div>
          </div>

          <el-divider />

          <div class="build-area">
            <div class="upload-title">2. 构建并部署（docker compose up -d --build）</div>
            <el-checkbox v-model="noCache">强制无缓存重建（--no-cache）</el-checkbox>
            <div style="margin-top: 12px; display: flex; gap: 12px">
              <el-button type="primary" :disabled="!status?.enabled || status?.building" @click="startBuild">
                开始构建部署
              </el-button>
              <el-button @click="refreshStatus">刷新状态</el-button>
            </div>
            <el-alert
              v-if="!status?.enabled"
              class="mt12"
              type="warning"
              :closable="false"
              show-icon
              title="部署功能未启用：请确认后端已挂载 /opt/wildcourt/server 且配置 Admin:Deploy 节点"
            />
          </div>
        </el-card>
      </el-col>

      <!-- 右：部署日志 -->
      <el-col :xs="24" :md="14">
        <el-card shadow="never">
          <template #header>
            <div class="card-header">
              <span>部署日志</span>
              <el-button size="small" :disabled="!status?.building" @click="autoScroll = !autoScroll">
                {{ autoScroll ? '自动滚动：开' : '自动滚动：关' }}
              </el-button>
            </div>
          </template>
          <div ref="logRef" class="log-box">
            <div v-if="logs.length === 0" class="log-empty">暂无日志，开始构建后实时输出</div>
            <div v-for="(line, i) in logs" :key="i" class="log-line">{{ line }}</div>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { UploadFilled } from '@element-plus/icons-vue'
import { deployApi, type DeployStatus } from '@/api'

const status = ref<DeployStatus | null>(null)
const logs = ref<string[]>([])
const noCache = ref(false)
const uploadResult = ref('')
const autoScroll = ref(true)
const logRef = ref<HTMLDivElement>()

let timer: number | undefined

async function refreshStatus() {
  try {
    status.value = await deployApi.status()
  } catch {
    // 拦截器提示
  }
}

async function fetchLogs() {
  try {
    logs.value = await deployApi.logs(300)
    if (autoScroll.value) scrollToBottom()
  } catch {
    // 拦截器提示
  }
}

function scrollToBottom() {
  nextTick(() => {
    const el = logRef.value
    if (el) el.scrollTop = el.scrollHeight
  })
}

async function handleUpload(file: File): Promise<boolean> {
  if (!file.name.toLowerCase().endsWith('.zip')) {
    ElMessage.error('仅支持 .zip 压缩包')
    return false
  }
  try {
    const res = await deployApi.upload(file)
    uploadResult.value = res.message
    ElMessage.success(res.message)
  } catch {
    // 拦截器提示
  }
  return false
}

async function startBuild() {
  try {
    const res = await deployApi.build(noCache.value)
    if (res.started) {
      ElMessage.success(res.message)
      uploadResult.value = ''
    } else {
      ElMessage.info(res.message)
    }
    await refreshStatus()
    await fetchLogs()
  } catch {
    // 拦截器提示
  }
}

function isResultError(s?: string): boolean {
  return !!s && s.includes('失败')
}

function formatDate(value: string): string {
  // 后端时间均为 UTC 且无时区标记（如 2026-08-10T18:13:31），补 Z 让浏览器按 UTC 解析
  const d = new Date(value.endsWith('Z') || /[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z')
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
}

onMounted(async () => {
  await refreshStatus()
  await fetchLogs()
  timer = window.setInterval(async () => {
    await refreshStatus()
    if (status.value?.building) await fetchLogs()
  }, 3000)
})

onBeforeUnmount(() => {
  if (timer) window.clearInterval(timer)
})
</script>

<style scoped>
.page {
  padding: 16px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.upload-title {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
  margin-bottom: 12px;
}

.upload-result {
  margin-top: 12px;
}

.mt12 {
  margin-top: 12px;
}

.result-error {
  color: #f56c6c;
}

.log-box {
  height: calc(100vh - 220px);
  overflow: auto;
  background: #1e1e1e;
  border-radius: 6px;
  padding: 12px;
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  line-height: 1.7;
  color: #d4d4d4;
}

.log-line {
  white-space: pre-wrap;
  word-break: break-all;
}

.log-empty {
  color: #808080;
  text-align: center;
  padding-top: 40px;
}
</style>
