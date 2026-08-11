<template>
  <div class="page">
    <el-row :gutter="16">
      <!-- 左：部署配置与操作 -->
      <el-col :xs="24" :md="6">
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

          <div v-if="canEdit" class="upload-area">
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

          <div v-if="canEdit" class="build-area">
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

      <!-- 中：文件管理（浏览/上传/替换/删除，免打包） -->
      <el-col :xs="24" :md="12">
        <el-card shadow="never" class="file-card">
          <template #header>
            <div class="card-header">
              <span>文件管理</span>
              <div class="header-actions">
                <el-button size="small" @click="loadFiles">刷新</el-button>
                <template v-if="canEdit">
                  <el-button size="small" @click="handleMkdir">新建目录</el-button>
                  <el-button size="small" type="primary" @click="openUpload">
                    上传文件
                    <input ref="fileInputRef" type="file" class="hidden-input" @change="onFileSelected" />
                  </el-button>
                </template>
              </div>
            </div>
          </template>

          <div class="file-breadcrumb">
            <el-breadcrumb separator="/">
              <el-breadcrumb-item>
                <a href="#" @click.prevent="goRoot">{{ rootName }}</a>
              </el-breadcrumb-item>
              <el-breadcrumb-item v-for="(seg, i) in breadcrumbs" :key="i">
                <a href="#" @click.prevent="goBreadcrumb(i)">{{ seg }}</a>
              </el-breadcrumb-item>
            </el-breadcrumb>
            <span class="file-hint">点击目录进入；点击文件在线预览/编辑；上传同名文件即覆盖（替换）</span>
          </div>

          <el-table
            :data="entries"
            size="small"
            height="calc(100vh - 340px)"
            :row-style="{ cursor: 'pointer' }"
            @row-dblclick="onRowDblClick"
          >
            <el-table-column label="名称" min-width="240">
              <template #default="{ row }">
                <span class="file-name" @click="onNameClick(row)">
                  <el-icon v-if="row.type === 'dir'" color="#e6a23c"><Folder /></el-icon>
                  <el-icon v-else color="#909399"><Document /></el-icon>
                  <span class="file-name-text">{{ row.name }}</span>
                </span>
              </template>
            </el-table-column>
            <el-table-column label="大小" width="90">
              <template #default="{ row }">{{ row.type === 'dir' ? '-' : formatSize(row.size) }}</template>
            </el-table-column>
            <el-table-column label="修改时间" width="130" prop="modifiedAt" />
            <el-table-column v-if="canEdit" label="操作" width="80" align="center">
              <template #default="{ row }">
                <el-button link type="danger" size="small" @click="handleDelete(row)">删除</el-button>
              </template>
            </el-table-column>
            <template #empty>
              <el-empty description="空目录" :image-size="60" />
            </template>
          </el-table>
        </el-card>
      </el-col>

      <!-- 右：部署日志 -->
      <el-col :xs="24" :md="6">
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

    <!-- 文件内容预览 / 在线编辑 -->
    <el-dialog
      v-model="editorVisible"
      :title="editorTitle"
      width="76%"
      top="4vh"
      destroy-on-close
      :before-close="onEditorBeforeClose"
    >
      <div v-loading="editorLoading" class="editor-body">
        <template v-if="editorPath">
          <div class="editor-meta">
            <el-tag size="small" type="info">{{ formatSize(editorSize) }}</el-tag>
            <el-tag size="small">{{ editorEncoding }}</el-tag>
            <span class="file-hint">Ctrl+S 保存 · Tab 缩进 · 保存后需重新构建部署生效</span>
          </div>
          <textarea
            v-model="editorContent"
            class="code-editor"
            spellcheck="false"
            @keydown.tab.prevent="insertTab"
            @keydown.ctrl.s.prevent="saveEditor"
            @keydown.meta.s.prevent="saveEditor"
          ></textarea>
        </template>
      </div>
      <template #footer>
        <el-button @click="editorVisible = false">取消</el-button>
        <el-button v-if="canEdit" type="primary" :loading="editorSaving" :disabled="!editorDirty" @click="saveEditor">
          保存
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { UploadFilled, Folder, Document } from '@element-plus/icons-vue'
import { deployApi, type DeployStatus, type FileEntry } from '@/api'
import { useAuthStore } from '@/stores/auth'

const authStore = useAuthStore()
const canEdit = computed(() => authStore.hasPermission('deploy:edit'))

const status = ref<DeployStatus | null>(null)
const logs = ref<string[]>([])
const noCache = ref(false)
const uploadResult = ref('')
const autoScroll = ref(true)
const logRef = ref<HTMLDivElement>()

const entries = ref<FileEntry[]>([])
const currentPath = ref('')
const fileInputRef = ref<HTMLInputElement>()

const rootName = computed(() => {
  const root = status.value?.hostCodeRoot || 'server'
  return root.split(/[\\/]/).filter(Boolean).pop() || 'server'
})
const breadcrumbs = computed(() => (currentPath.value ? currentPath.value.split('/') : []))

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

// ==================== 文件管理 ====================

async function loadFiles() {
  try {
    entries.value = await deployApi.listFiles(currentPath.value)
  } catch {
    // 拦截器提示
  }
}

function goRoot() {
  currentPath.value = ''
  loadFiles()
}

function goDir(entry: FileEntry) {
  if (entry.type !== 'dir') return
  currentPath.value = entry.path
  loadFiles()
}

function goBreadcrumb(index: number) {
  currentPath.value = currentPath.value.split('/').slice(0, index + 1).join('/')
  loadFiles()
}

function onNameClick(row: FileEntry) {
  if (row.type === 'dir') goDir(row)
  else openEditor(row)
}

function onRowDblClick(row: FileEntry) {
  if (row.type === 'dir') goDir(row)
  else openEditor(row)
}

// ==================== 文件内容预览 / 在线编辑 ====================

const editorVisible = ref(false)
const editorLoading = ref(false)
const editorSaving = ref(false)
const editorPath = ref('')
const editorContent = ref('')
const editorOriginal = ref('')
const editorEncoding = ref('utf-8')
const editorSize = ref(0)

const editorDirty = computed(() => editorContent.value !== editorOriginal.value)
const editorTitle = computed(() => (editorPath.value ? `编辑 ${editorPath.value}` : '文件编辑'))

async function openEditor(row: FileEntry) {
  if (row.type !== 'file') return
  editorPath.value = row.path
  editorContent.value = ''
  editorOriginal.value = ''
  editorVisible.value = true
  editorLoading.value = true
  try {
    const res = await deployApi.getFileContent(row.path)
    editorContent.value = res.content
    editorOriginal.value = res.content
    editorEncoding.value = res.encoding
    editorSize.value = res.size
  } catch {
    editorVisible.value = false
  } finally {
    editorLoading.value = false
  }
}

function onEditorBeforeClose(done: () => void) {
  if (editorDirty.value) {
    ElMessageBox.confirm('有未保存的修改，确定关闭？', '提示', { type: 'warning' })
      .then(() => done())
      .catch(() => {})
  } else {
    done()
  }
}

function insertTab(e: KeyboardEvent) {
  const el = e.target as HTMLTextAreaElement
  const start = el.selectionStart
  const end = el.selectionEnd
  editorContent.value = editorContent.value.slice(0, start) + '    ' + editorContent.value.slice(end)
  nextTick(() => {
    el.selectionStart = el.selectionEnd = start + 4
  })
}

async function saveEditor() {
  if (!editorPath.value || editorSaving.value) return
  editorSaving.value = true
  try {
    const res = await deployApi.saveFileContent(editorPath.value, editorContent.value, editorEncoding.value)
    ElMessage.success(res.message)
    editorOriginal.value = editorContent.value
    editorSize.value = res.size
    await loadFiles()
  } catch {
    // 拦截器提示
  } finally {
    editorSaving.value = false
  }
}

function openUpload() {
  fileInputRef.value?.click()
}

async function onFileSelected(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return
  try {
    const res = await deployApi.uploadFile(currentPath.value, file)
    ElMessage.success(res.message)
    await loadFiles()
  } catch {
    // 拦截器提示
  } finally {
    input.value = ''
  }
}

async function handleMkdir() {
  let name = ''
  try {
    const { value } = await ElMessageBox.prompt('输入新目录名称', '新建目录', {
      confirmButtonText: '创建',
      cancelButtonText: '取消',
      inputPattern: /^[\w.\- ]+$/,
      inputErrorMessage: '目录名仅支持字母/数字/下划线/点/横线/空格',
    })
    name = value.trim()
  } catch {
    return
  }
  if (!name) return
  try {
    const path = currentPath.value ? `${currentPath.value}/${name}` : name
    const res = await deployApi.mkdir(path)
    ElMessage.success(res.message)
    await loadFiles()
  } catch {
    // 拦截器提示
  }
}

async function handleDelete(row: FileEntry) {
  const isDir = row.type === 'dir'
  try {
    await ElMessageBox.confirm(
      `确定删除${isDir ? '目录' : '文件'}「${row.name}」？${isDir ? '目录将递归删除，不可恢复！' : ''}`,
      '删除确认',
      { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    const res = await deployApi.deleteFile(row.path)
    ElMessage.success(res.message)
    await loadFiles()
  } catch {
    // 拦截器提示
  }
}

function formatSize(size: number): string {
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  if (size < 1024 * 1024 * 1024) return `${(size / (1024 * 1024)).toFixed(1)} MB`
  return `${(size / (1024 * 1024 * 1024)).toFixed(2)} GB`
}

// ==================== 通用 ====================

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
  await loadFiles()
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
  gap: 8px;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
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

.file-breadcrumb {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
  flex-wrap: wrap;
}

.file-hint {
  font-size: 12px;
  color: #909399;
}

.hidden-input {
  display: none;
}

.file-name {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.file-name-text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
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

.editor-body {
  min-height: 55vh;
}

.editor-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
  flex-wrap: wrap;
}

.code-editor {
  width: 100%;
  height: 55vh;
  padding: 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: #1e1e1e;
  color: #d4d4d4;
  font-family: 'Cascadia Code', Consolas, monospace;
  font-size: 13px;
  line-height: 1.7;
  resize: vertical;
  box-sizing: border-box;
  outline: none;
}

.code-editor:focus {
  border-color: var(--el-color-primary);
}
</style>
