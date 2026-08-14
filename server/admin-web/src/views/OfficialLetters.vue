<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>官方发信</span>
      </div>
    </template>

    <el-alert type="info" :closable="false" show-icon class="tip">
      向玩家发送官方信件（直接送达，不经审核）。支持单发、多选批量发信、按条件群发。附件可携带多种：庭贝 / 邮券 / 道具 / 非遗。
    </el-alert>

    <el-tabs v-model="mode">
      <!-- 单发 / 多选批量 -->
      <el-tab-pane label="定向发信（单发/多选）" name="direct">
        <el-form :model="form" :rules="rules" ref="formRef" label-width="90px" class="letter-form">
          <el-form-item label="收件玩家" prop="toUserIds">
            <div class="player-picker">
              <el-input
                v-model="playerKeyword"
                placeholder="搜索账号 / 昵称 / 玩家ID"
                clearable
                class="player-search"
                @keyup.enter="searchPlayers"
                @clear="searchPlayers"
              >
                <template #prefix><el-icon><Search /></el-icon></template>
              </el-input>
              <el-button type="primary" :loading="searching" @click="searchPlayers">搜索</el-button>
            </div>
            <el-select
              v-model="form.toUserIds"
              placeholder="选择收件玩家（可多选）"
              filterable
              multiple
              collapse-tags
              collapse-tags-tooltip
              class="player-select"
              no-data-text="请输入关键字搜索玩家"
              :loading="searching"
            >
              <el-option
                v-for="p in playerOptions"
                :key="p.userId"
                :label="`${p.nickname} (${p.username} #${p.userId})`"
                :value="p.userId"
              />
            </el-select>
          </el-form-item>

          <el-form-item label="信件标题" prop="title">
            <el-input v-model="form.title" maxlength="100" show-word-limit placeholder="信件标题（可留空）" />
          </el-form-item>

          <el-form-item label="信件内容" prop="content">
            <el-input v-model="form.content" type="textarea" :rows="5" maxlength="200" show-word-limit placeholder="信件正文" />
          </el-form-item>

          <el-form-item label="附件">
            <AttachmentEditor v-model="form.attachments" />
          </el-form-item>

          <el-form-item>
            <el-button type="primary" :loading="sending" @click="submit">发送</el-button>
            <el-button @click="reset">重置</el-button>
          </el-form-item>
        </el-form>
      </el-tab-pane>

      <!-- 条件群发 -->
      <el-tab-pane label="条件群发" name="broadcast">
        <el-alert type="warning" :closable="false" show-icon class="tip">
          按筛选条件匹配玩家后群发，请谨慎操作。不填筛选条件将发给全部玩家（上限 10000）。
        </el-alert>

        <el-form :model="broadcastForm" :rules="broadcastRules" ref="broadcastFormRef" label-width="90px" class="letter-form">
          <el-form-item label="筛选条件">
            <div class="filter-row">
              <span class="filter-label">等级</span>
              <el-input-number v-model="broadcastForm.minLevel" :min="1" :max="99" placeholder="最低" style="width: 130px" />
              <span class="filter-sep">~</span>
              <el-input-number v-model="broadcastForm.maxLevel" :min="1" :max="99" placeholder="最高" style="width: 130px" />
            </div>
            <div class="filter-row">
              <span class="filter-label">封禁状态</span>
              <el-select v-model="broadcastForm.isBanned" placeholder="不限" clearable style="width: 200px">
                <el-option label="正常" :value="false" />
                <el-option label="已封禁" :value="true" />
              </el-select>
            </div>
          </el-form-item>

          <el-form-item label="信件标题" prop="title">
            <el-input v-model="broadcastForm.title" maxlength="100" show-word-limit placeholder="信件标题（可留空）" />
          </el-form-item>

          <el-form-item label="信件内容" prop="content">
            <el-input v-model="broadcastForm.content" type="textarea" :rows="5" maxlength="200" show-word-limit placeholder="信件正文" />
          </el-form-item>

          <el-form-item label="附件">
            <AttachmentEditor v-model="broadcastForm.attachments" />
          </el-form-item>

          <el-form-item>
            <el-button type="danger" :loading="sending" @click="submitBroadcast">群发</el-button>
            <el-button @click="resetBroadcast">重置</el-button>
          </el-form-item>
        </el-form>
      </el-tab-pane>
    </el-tabs>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import AttachmentEditor from '@/components/AttachmentEditor.vue'
import { letterApi, playerApi, type PlayerItem, type BatchOperationResult, type LetterAttachment } from '@/api'

const mode = ref<'direct' | 'broadcast'>('direct')
const formRef = ref<FormInstance>()
const broadcastFormRef = ref<FormInstance>()

// ---- 定向发信表单 ----
const form = reactive({
  toUserIds: [] as number[],
  title: '',
  content: '',
  attachments: [] as LetterAttachment[],
})

const rules: FormRules = {
  toUserIds: [{ required: true, message: '请选择至少一个收件玩家', trigger: 'change' }],
  content: [{ required: true, message: '请输入信件内容', trigger: 'blur' }],
}

// ---- 条件群发表单 ----
const broadcastForm = reactive({
  minLevel: undefined as number | undefined,
  maxLevel: undefined as number | undefined,
  isBanned: undefined as boolean | undefined,
  title: '',
  content: '',
  attachments: [] as LetterAttachment[],
})

const broadcastRules: FormRules = {
  content: [{ required: true, message: '请输入信件内容', trigger: 'blur' }],
}

const playerKeyword = ref('')
const playerOptions = ref<PlayerItem[]>([])
const searching = ref(false)
const sending = ref(false)

async function searchPlayers() {
  searching.value = true
  try {
    const res = await playerApi.list({ keyword: playerKeyword.value || undefined, page: 1, pageSize: 50 })
    playerOptions.value = res.items
  } catch {
    // 拦截器提示
  } finally {
    searching.value = false
  }
}

async function submit() {
  if (!formRef.value) return
  const valid = await formRef.value.validate().catch(() => false)
  if (!valid) return
  sending.value = true
  try {
    const result: BatchOperationResult = await letterApi.sendBatch({
      toUserIds: form.toUserIds,
      title: form.title || undefined,
      content: form.content,
      attachments: form.attachments,
    })
    ElMessage.success(`已发送 ${result.succeeded} 封${result.failed ? `，失败 ${result.failed} 封` : ''}`)
    if (result.failed) {
      ElMessage.warning(`失败明细：${result.failures.map(f => `#${f.userId}(${f.reason})`).join('、')}`)
    }
    reset()
  } catch {
    // 拦截器提示
  } finally {
    sending.value = false
  }
}

async function submitBroadcast() {
  if (!broadcastFormRef.value) return
  const valid = await broadcastFormRef.value.validate().catch(() => false)
  if (!valid) return
  try {
    await ElMessageBox.confirm(
      '群发将向所有符合条件的玩家发送信件，确认执行？',
      '群发确认',
      { type: 'warning', confirmButtonText: '群发', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  sending.value = true
  try {
    const result: BatchOperationResult = await letterApi.broadcast({
      title: broadcastForm.title || undefined,
      content: broadcastForm.content,
      attachments: broadcastForm.attachments,
      filter: {
        minLevel: broadcastForm.minLevel,
        maxLevel: broadcastForm.maxLevel,
        isBanned: broadcastForm.isBanned,
      },
    })
    ElMessage.success(`群发成功，共 ${result.succeeded} 人`)
    resetBroadcast()
  } catch {
    // 拦截器提示
  } finally {
    sending.value = false
  }
}

function reset() {
  form.toUserIds = []
  form.title = ''
  form.content = ''
  form.attachments = []
  playerKeyword.value = ''
  playerOptions.value = []
  formRef.value?.clearValidate()
}

function resetBroadcast() {
  broadcastForm.minLevel = undefined
  broadcastForm.maxLevel = undefined
  broadcastForm.isBanned = undefined
  broadcastForm.title = ''
  broadcastForm.content = ''
  broadcastForm.attachments = []
  broadcastFormRef.value?.clearValidate()
}
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

.tip {
  margin-bottom: 20px;
}

.letter-form {
  max-width: 720px;
}

.player-picker {
  display: flex;
  gap: 8px;
  width: 100%;
  margin-bottom: 8px;
}

.player-search {
  flex: 1;
}

.player-select {
  width: 100%;
}

.filter-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.filter-label {
  width: 60px;
  color: #606266;
  flex-shrink: 0;
}

.filter-sep {
  color: #909399;
}
</style>
