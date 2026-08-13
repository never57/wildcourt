<template>
  <el-card class="page" shadow="never">
    <template #header>
      <div class="card-header">
        <span>官方发信</span>
      </div>
    </template>

    <el-alert type="info" :closable="false" show-icon class="tip">
      向指定玩家发送一封官方信件（直接送达，不经审核）。附件可选：庭贝 / 邮券 / 道具 / 非遗。
    </el-alert>

    <el-form :model="form" :rules="rules" ref="formRef" label-width="90px" class="letter-form">
      <el-form-item label="收件玩家" prop="toUserId">
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
          v-model="form.toUserId"
          placeholder="选择收件玩家"
          filterable
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

      <el-form-item label="信件内容" prop="content">
        <el-input v-model="form.content" type="textarea" :rows="5" maxlength="200" show-word-limit placeholder="信件正文" />
      </el-form-item>

      <el-form-item label="附件类型">
        <el-radio-group v-model="form.rewardType">
          <el-radio value="">无附件</el-radio>
          <el-radio value="shell">庭贝</el-radio>
          <el-radio value="coupon">邮券</el-radio>
          <el-radio value="item">道具 / 非遗</el-radio>
        </el-radio-group>
      </el-form-item>

      <el-form-item v-if="form.rewardType" label="附件数量" prop="rewardCount">
        <el-input-number v-model="form.rewardCount" :min="1" :max="99999999" style="width: 200px" />
      </el-form-item>

      <el-form-item v-if="form.rewardType === 'item'" label="道具ID" prop="itemId">
        <el-input v-model="form.itemId" placeholder="道具/非遗 ID，如 decoration_xxx 或 crop_xxx" maxlength="64" />
      </el-form-item>

      <el-form-item>
        <el-button type="primary" :loading="sending" @click="submit">发送</el-button>
        <el-button @click="reset">重置</el-button>
      </el-form-item>
    </el-form>
  </el-card>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import { letterApi, playerApi, type PlayerItem } from '@/api'

const formRef = ref<FormInstance>()

const form = reactive({
  toUserId: undefined as number | undefined,
  content: '',
  rewardType: '' as '' | 'shell' | 'coupon' | 'item',
  rewardCount: 1,
  itemId: '',
})

const rules: FormRules = {
  toUserId: [{ required: true, message: '请选择收件玩家', trigger: 'change' }],
  content: [{ required: true, message: '请输入信件内容', trigger: 'blur' }],
  rewardCount: [{ required: true, message: '请输入附件数量', trigger: 'blur' }],
  itemId: [{ required: true, message: '请输入道具/非遗 ID', trigger: 'blur' }],
}

const playerKeyword = ref('')
const playerOptions = ref<PlayerItem[]>([])
const searching = ref(false)
const sending = ref(false)

async function searchPlayers() {
  searching.value = true
  try {
    const res = await playerApi.list({ keyword: playerKeyword.value || undefined, page: 1, pageSize: 20 })
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
    await letterApi.sendOfficial({
      toUserId: form.toUserId!,
      content: form.content,
      rewardType: form.rewardType || undefined,
      rewardCount: form.rewardType ? form.rewardCount : 0,
      itemId: form.rewardType === 'item' ? form.itemId : undefined,
    })
    ElMessage.success('官方信件已发送')
    reset()
  } catch {
    // 拦截器提示
  } finally {
    sending.value = false
  }
}

function reset() {
  form.toUserId = undefined
  form.content = ''
  form.rewardType = ''
  form.rewardCount = 1
  form.itemId = ''
  playerKeyword.value = ''
  playerOptions.value = []
  formRef.value?.clearValidate()
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
</style>
