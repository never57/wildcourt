<template>
  <div class="page">
    <el-alert
      title="高危操作：此处可执行任意 SQL 或直接编辑业务数据，请务必先在测试环境验证，确认后再操作！"
      type="error"
      show-icon
      :closable="false"
    />

    <el-card class="mt16" shadow="never">
      <el-tabs v-model="activeTab">
        <!-- ==================== 可视化 ==================== -->
        <el-tab-pane label="可视化" name="visual">
          <div class="visual-layout">
            <div class="db-sidebar">
              <div class="sidebar-header">
                <span>数据库 / 表</span>
                <el-button link type="primary" size="small" @click="reloadDatabases">刷新</el-button>
              </div>
              <el-tree
                :data="treeData"
                :props="treeProps"
                lazy
                :load="loadTreeNode"
                node-key="id"
                highlight-current
                :expand-on-click-node="false"
                @node-click="onNodeClick"
              >
                <template #default="{ data }">
                  <span class="tree-node">
                    <span class="tree-label">{{ data.label }}</span>
                    <el-tag v-if="data.tableName && data.comment" size="small" type="info" effect="plain" class="tree-comment">
                      {{ data.comment }}
                    </el-tag>
                  </span>
                </template>
              </el-tree>
            </div>

            <div class="db-main">
              <template v-if="currentTable">
                <div class="table-toolbar">
                  <div class="table-title">
                    <span class="table-name">{{ currentDb }}.<b>{{ currentTable }}</b></span>
                    <el-tag v-if="tableComment" size="small" type="info" effect="plain">{{ tableComment }}</el-tag>
                    <span class="table-total">共 {{ total }} 行</span>
                  </div>
                  <div>
                    <el-button :disabled="!primaryKey" @click="openInsert">新增行</el-button>
                    <el-button type="primary" :loading="loading" @click="loadData">刷新</el-button>
                  </div>
                </div>

                <el-table
                  :data="rows"
                  border
                  stripe
                  size="small"
                  v-loading="loading"
                  height="calc(100vh - 380px)"
                >
                  <el-table-column v-for="col in columns" :key="col.name" :prop="col.name" min-width="150" show-overflow-tooltip>
                    <template #header>
                      <el-tooltip
                        :content="`${col.columnType}${col.nullable ? ' · 可空' : ''}${col.comment ? ' · ' + col.comment : ''}`"
                        placement="top"
                      >
                        <span class="col-header">
                          {{ col.name }}
                          <el-tag v-if="col.isPrimaryKey" size="small" type="warning">PK</el-tag>
                        </span>
                      </el-tooltip>
                    </template>
                    <template #default="{ row }">
                      <span :class="{ 'cell-null': row[col.name] == null }">
                        {{ formatCell(row[col.name]) }}
                      </span>
                    </template>
                  </el-table-column>
                  <el-table-column label="操作" width="130" fixed="right">
                    <template #default="{ row }">
                      <el-button link type="primary" :disabled="!primaryKey" @click="openEdit(row)">编辑</el-button>
                      <el-button link type="danger" :disabled="!primaryKey" @click="removeRow(row)">删除</el-button>
                    </template>
                  </el-table-column>
                </el-table>

                <div class="pager">
                  <el-pagination
                    v-model:current-page="page"
                    v-model:page-size="pageSize"
                    :total="total"
                    :page-sizes="[20, 50, 100, 200]"
                    layout="total, sizes, prev, pager, next, jumper"
                    @current-change="loadData"
                    @size-change="onPageSizeChange"
                  />
                </div>
              </template>

              <el-empty v-else description="从左侧选择一个表查看和编辑数据" />
            </div>
          </div>
        </el-tab-pane>

        <!-- ==================== SQL 编辑器 ==================== -->
        <el-tab-pane label="SQL 编辑器" name="sql">
          <div>
            <div class="card-header">
              <span>SQL 编辑器</span>
              <div>
                <el-upload
                  :show-file-list="false"
                  accept=".sql"
                  :before-upload="handleFileSelect"
                  style="display: inline-block; margin-right: 8px"
                >
                  <el-button>上传 .sql 文件</el-button>
                </el-upload>
                <el-button type="primary" :disabled="!sql" @click="confirmExecute">执行 SQL</el-button>
              </div>
            </div>

            <el-input
              v-model="sql"
              type="textarea"
              :rows="14"
              placeholder="-- 在此输入 SQL，多条语句用 ; 分隔，例如：
CREATE TABLE IF NOT EXISTS `gift_codes` (
  `id` bigint NOT NULL AUTO_INCREMENT,
  `code` varchar(32) NOT NULL,
  `reward` varchar(64) NOT NULL,
  `used_at` datetime NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;"
            />

            <div v-if="result" class="result-box">
              <el-alert type="success" :closable="false" show-icon>
                <template #title>
                  {{ result.message }}
                </template>
              </el-alert>
              <el-descriptions :column="3" class="result-desc" border>
                <el-descriptions-item label="语句数">{{ result.statementCount }}</el-descriptions-item>
                <el-descriptions-item label="影响行数">{{ result.affectedRows }}</el-descriptions-item>
                <el-descriptions-item label="耗时">{{ result.durationMs }} ms</el-descriptions-item>
              </el-descriptions>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- 编辑 / 新增行弹窗 -->
    <el-dialog
      v-model="editDialog"
      :title="editMode === 'edit' ? '编辑行' : '新增行'"
      width="760px"
      :close-on-click-modal="false"
    >
      <el-form label-width="170px" label-position="left" class="edit-form">
        <el-form-item v-for="col in columns" :key="col.name" :label="col.name">
          <template v-if="isBoolCol(col)">
            <el-switch v-model="editForm[col.name]" />
          </template>
          <template v-else-if="isTextCol(col)">
            <el-input
              v-model="editForm[col.name]"
              type="textarea"
              :rows="3"
              :disabled="editMode === 'edit' && col.isPrimaryKey"
              :placeholder="fieldPlaceholder(col)"
            />
          </template>
          <template v-else>
            <el-input
              v-model="editForm[col.name]"
              :disabled="editMode === 'edit' && col.isPrimaryKey"
              :placeholder="fieldPlaceholder(col)"
            />
          </template>
          <div class="col-hint">{{ col.columnType }} · {{ col.nullable ? '可空' : '必填' }}<template v-if="col.comment"> · {{ col.comment }}</template></div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editDialog = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submitRow">
          {{ editMode === 'edit' ? '保存修改' : '确认新增' }}
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { databaseApi, type ColumnInfo, type DatabaseInfo, type ExecuteSqlResult, type TableInfo } from '@/api'

const activeTab = ref('visual')

// ==================== SQL 编辑器 ====================
const sql = ref('')
const result = ref<ExecuteSqlResult | null>(null)

function handleFileSelect(file: File): boolean {
  if (!file.name.toLowerCase().endsWith('.sql')) {
    ElMessage.error('仅支持 .sql 文件')
    return false
  }
  const reader = new FileReader()
  reader.onload = () => {
    sql.value = String(reader.result || '')
    result.value = null
    ElMessage.success(`已载入 ${file.name}`)
  }
  reader.readAsText(file)
  return false
}

async function confirmExecute() {
  if (!sql.value.trim()) return
  const statementCount = sql.value.split(';').filter((s) => s.trim()).length
  try {
    await ElMessageBox.confirm(
      `即将执行 ${statementCount} 条 SQL 语句。执行不可回滚，确认继续？`,
      '高危操作确认',
      { type: 'error', confirmButtonText: '确认执行', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    result.value = await databaseApi.executeSql(sql.value)
    ElMessage.success('执行完成')
  } catch {
    // 拦截器提示
  }
}

// ==================== 可视化 ====================
interface TreeNode {
  id: string
  label: string
  dbName?: string
  tableName?: string
  comment?: string
}

const treeProps = { label: 'label', children: 'children', isLeaf: 'isLeaf' }
const treeData = ref<TreeNode[]>([])

const currentDb = ref('')
const currentTable = ref('')
const tableComment = ref('')
const columns = ref<ColumnInfo[]>([])
const rows = ref<Record<string, unknown>[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)
const loading = ref(false)

const primaryKey = computed(() => columns.value.find((c) => c.isPrimaryKey)?.name)

const editDialog = ref(false)
const editMode = ref<'edit' | 'insert'>('edit')
const saving = ref(false)
const editForm = ref<Record<string, string | boolean>>({})
const editTarget = ref<Record<string, unknown> | null>(null)

async function reloadDatabases() {
  try {
    const dbs = await databaseApi.listDatabases()
    treeData.value = dbs.map((db: DatabaseInfo) => ({
      id: `db:${db.name}`,
      label: db.name,
      dbName: db.name,
    }))
  } catch {
    // 拦截器提示
  }
}

async function loadTreeNode(node: { level: number; data: TreeNode }, resolve: (data: TreeNode[]) => void) {
  if (node.level === 0) {
    const dbs = await databaseApi.listDatabases()
    resolve(
      dbs.map((db: DatabaseInfo) => ({
        id: `db:${db.name}`,
        label: db.name,
        dbName: db.name,
      })),
    )
    return
  }
  if (node.level === 1) {
    const tables = await databaseApi.listTables(node.data.dbName || '')
    resolve(
      tables.map((t: TableInfo) => ({
        id: `tbl:${node.data.dbName}.${t.name}`,
        label: t.name,
        dbName: node.data.dbName,
        tableName: t.name,
        comment: t.comment,
      })),
    )
    return
  }
  resolve([])
}

function onNodeClick(data: TreeNode) {
  if (!data.tableName) return
  currentDb.value = data.dbName || ''
  currentTable.value = data.tableName
  tableComment.value = data.comment || ''
  page.value = 1
  loadData()
}

async function loadData() {
  if (!currentDb.value || !currentTable.value) return
  loading.value = true
  try {
    const data = await databaseApi.getTableData(currentDb.value, currentTable.value, page.value, pageSize.value)
    columns.value = data.columns
    rows.value = data.rows
    total.value = data.total
  } catch {
    // 拦截器提示
  } finally {
    loading.value = false
  }
}

function onPageSizeChange() {
  page.value = 1
  loadData()
}

function formatCell(value: unknown): string {
  if (value == null) return 'NULL'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

function isBoolCol(col: ColumnInfo): boolean {
  return /^(bit\(1\)|tinyint\(1\))/i.test(col.columnType.trim())
}

function isTextCol(col: ColumnInfo): boolean {
  return /(text|blob|json|longtext|mediumtext|longblob|mediumblob)/i.test(col.columnType)
}

function fieldPlaceholder(col: ColumnInfo): string {
  if (col.isPrimaryKey) return editMode.value === 'edit' ? '主键不可修改' : '留空自动生成'
  return col.nullable ? '留空表示 NULL' : '必填'
}

function openEdit(row: Record<string, unknown>) {
  editMode.value = 'edit'
  editTarget.value = row
  editForm.value = {}
  for (const col of columns.value) {
    const value = row[col.name]
    if (isBoolCol(col)) {
      editForm.value[col.name] = value === true || value === 1 || value === '1'
    } else {
      editForm.value[col.name] = value == null ? '' : String(value)
    }
  }
  editDialog.value = true
}

function openInsert() {
  editMode.value = 'insert'
  editTarget.value = null
  editForm.value = {}
  for (const col of columns.value) {
    editForm.value[col.name] = isBoolCol(col) ? false : ''
  }
  editDialog.value = true
}

async function submitRow() {
  if (editMode.value === 'edit') {
    await submitUpdate()
  } else {
    await submitInsert()
  }
}

async function submitUpdate() {
  const pk = primaryKey.value
  if (!pk || !editTarget.value) return
  const values: Record<string, unknown> = {}
  const target = editTarget.value
  for (const col of columns.value) {
    if (col.isPrimaryKey) continue
    const newValue = editForm.value[col.name]
    const oldRaw = target[col.name]
    const oldNormalized = oldRaw == null ? '' : String(oldRaw)
    const newNormalized = typeof newValue === 'boolean' ? (newValue ? '1' : '0') : String(newValue)
    if (oldNormalized !== newNormalized) {
      values[col.name] = typeof newValue === 'boolean' ? newValue : newValue === '' ? null : newValue
    }
  }
  if (Object.keys(values).length === 0) {
    ElMessage.warning('没有修改任何字段')
    return
  }
  try {
    saving.value = true
    await databaseApi.updateRow({
      database: currentDb.value,
      table: currentTable.value,
      values,
      primaryKey: pk,
      primaryKeyValue: target[pk] ?? null,
    })
    ElMessage.success('已保存')
    editDialog.value = false
    loadData()
  } catch {
    // 拦截器提示
  } finally {
    saving.value = false
  }
}

async function submitInsert() {
  const values: Record<string, unknown> = {}
  for (const col of columns.value) {
    if (col.isPrimaryKey) continue
    const value = editForm.value[col.name]
    if (isBoolCol(col)) {
      values[col.name] = value as boolean
    } else if (value !== '') {
      values[col.name] = value
    }
  }
  if (Object.keys(values).length === 0) {
    ElMessage.warning('请至少填写一个字段')
    return
  }
  try {
    saving.value = true
    await databaseApi.insertRow({
      database: currentDb.value,
      table: currentTable.value,
      values,
    })
    ElMessage.success('已新增')
    editDialog.value = false
    loadData()
  } catch {
    // 拦截器提示
  } finally {
    saving.value = false
  }
}

async function removeRow(row: Record<string, unknown>) {
  const pk = primaryKey.value
  if (!pk) {
    ElMessage.error('该表没有主键，无法定位删除')
    return
  }
  try {
    await ElMessageBox.confirm(
      `确认删除该行（${pk} = ${formatCell(row[pk])}）？删除不可恢复！`,
      '危险操作确认',
      { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  try {
    await databaseApi.deleteRow({
      database: currentDb.value,
      table: currentTable.value,
      primaryKey: pk,
      primaryKeyValue: row[pk] ?? null,
    })
    ElMessage.success('已删除')
    if (rows.value.length === 1 && page.value > 1) page.value -= 1
    loadData()
  } catch {
    // 拦截器提示
  }
}
</script>

<style scoped>
.page {
  padding: 16px;
}

.mt16 {
  margin-top: 16px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.result-box {
  margin-top: 16px;
}

.result-desc {
  margin-top: 12px;
}

.visual-layout {
  display: flex;
  gap: 16px;
  min-height: 420px;
}

.db-sidebar {
  width: 280px;
  flex-shrink: 0;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  padding: 8px;
  overflow: auto;
  max-height: calc(100vh - 320px);
}

.sidebar-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  font-weight: 600;
  padding: 4px 6px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  margin-bottom: 6px;
}

.tree-node {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  max-width: 100%;
}

.tree-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tree-comment {
  max-width: 90px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.db-main {
  flex: 1;
  min-width: 0;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
  gap: 12px;
}

.table-title {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  flex-wrap: wrap;
}

.table-name {
  font-size: 14px;
  white-space: nowrap;
}

.table-total {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.col-header {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.cell-null {
  color: var(--el-text-color-placeholder);
  font-style: italic;
}

.pager {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}

.edit-form .el-form-item {
  margin-bottom: 14px;
}

.col-hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 2px;
  word-break: break-all;
}
</style>
