<template>
  <div class="attachment-editor">
    <div v-for="(att, idx) in modelValue" :key="idx" class="attachment-row">
      <el-select v-model="att.type" placeholder="附件类型" style="width: 140px">
        <el-option label="庭贝" value="shell" />
        <el-option label="邮券" value="coupon" />
        <el-option label="道具 / 非遗" value="item" />
      </el-select>

      <el-input
        v-if="att.type === 'item'"
        v-model="att.itemId"
        placeholder="道具/非遗 ID"
        maxlength="64"
        style="width: 200px"
      />

      <el-input-number v-model="att.count" :min="1" :max="99999999" placeholder="数量" style="width: 160px" />

      <el-button type="danger" text @click="remove(idx)">移除</el-button>
    </div>

    <el-button type="primary" plain @click="add">+ 添加附件</el-button>
    <div class="attachment-hint">类型不可重复：庭贝 / 邮券 各一个，道具可按不同 ID 多个。</div>
  </div>
</template>

<script setup lang="ts">
import type { LetterAttachment } from '@/api'

const props = defineProps<{
  modelValue: LetterAttachment[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: LetterAttachment[]): void
}>()

function add() {
  emit('update:modelValue', [...props.modelValue, { type: 'shell', count: 1 }])
}

function remove(idx: number) {
  const next = props.modelValue.filter((_, i) => i !== idx)
  emit('update:modelValue', next)
}
</script>

<style scoped>
.attachment-editor {
  width: 100%;
}

.attachment-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}

.attachment-hint {
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}
</style>
