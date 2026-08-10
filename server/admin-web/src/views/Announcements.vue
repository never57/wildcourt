<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>公告管理</span>
        <el-button type="primary" @click="openCreate">新建公告</el-button>
      </div>
    </template>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column prop="announcementId" label="ID" width="70" />
      <el-table-column prop="title" label="标题" min-width="180" show-overflow-tooltip />
      <el-table-column prop="content" label="内容" min-width="240" show-overflow-tooltip />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)" size="small">{{ statusLabel(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="creatorName" label="创建人" width="100" />
      <el-table-column label="创建时间" width="160">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button v-if="row.status !== 'published'" link type="success" size="small" @click="publish(row)">发布</el-button>
          <el-button link type="primary" size="small" @click="openEdit(row)">编辑</el-button>
          <el-button link type="danger" size="small" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editing ? '编辑公告' : '新建公告'" width="600px">
      <el-form :model="form" :rules="rules" ref="formRef" label-width="70px">
        <el-form-item label="标题" prop="title">
          <el-input v-model="form.title" maxlength="100" show-word-limit placeholder="公告标题" />
        </el-form-item>
        <el-form-item label="内容" prop="content">
          <el-input v-model="form.content" type="textarea" :rows="6" placeholder="公告正文" />
        </el-form-item>
        <el-form-item label="状态" prop="status">
          <el-radio-group v-model="form.status">
            <el-radio value="draft">草稿</el-radio>
            <el-radio value="published">发布</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submit">保存</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { announcementApi, type Announcement } from '@/api'

const items = ref<Announcement[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)
const editing = ref<Announcement | null>(null)
const formRef = ref<FormInstance>()

const form = reactive({ title: '', content: '', status: 'draft' })
const rules: FormRules = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  content: [{ required: true, message: '请输入内容', trigger: 'blur' }],
}

async function fetchList() {
  loading.value = true
  try {
    items.value = await announcementApi.list()
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editing.value = null
  form.title = ''
  form.content = ''
  form.status = 'draft'
  dialogVisible.value = true
}

function openEdit(row: Announcement) {
  editing.value = row
  form.title = row.title
  form.content = row.content
  form.status = row.status === 'published' ? 'published' : 'draft'
  dialogVisible.value = true
}

async function submit() {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    if (editing.value) {
      await announcementApi.update(editing.value.announcementId, { ...form })
    } else {
      await announcementApi.create({ ...form })
    }
    ElMessage.success('保存成功')
    dialogVisible.value = false
    fetchList()
  } catch {
    // 拦截器提示
  } finally {
    saving.value = false
  }
}

async function publish(row: Announcement) {
  try {
    await announcementApi.publish(row.announcementId)
    ElMessage.success('已发布')
    fetchList()
  } catch {
    // 拦截器提示
  }
}

async function remove(row: Announcement) {
  try {
    await ElMessageBox.confirm(`确认删除公告「${row.title}」？`, '删除确认', { type: 'warning' })
  } catch {
    return
  }
  try {
    await announcementApi.remove(row.announcementId)
    ElMessage.success('已删除')
    fetchList()
  } catch {
    // 拦截器提示
  }
}

function statusLabel(s: string): string {
  return s === 'published' ? '已发布' : s === 'archived' ? '已归档' : '草稿'
}

function statusType(s: string): 'success' | 'info' | 'warning' {
  return s === 'published' ? 'success' : s === 'archived' ? 'info' : 'warning'
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
