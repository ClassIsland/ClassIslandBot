<script setup lang="ts">
import { computed, onMounted, reactive, ref, type Ref } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import Apis from '@/api'
import type {
  DiscussionAssociationRequest,
  DiscussionAssociationResponse,
  GitHubDiscussionOption,
  GitHubIssueOption,
  GitHubRepositoryOption,
} from '@/api/globals'
import {
  Delete,
  Edit,
  Plus,
  Refresh,
  Search,
} from '@element-plus/icons-vue'

interface DiscussionAssociationForm {
  repoId: string
  discussionId: string
  issueId: string
  refCommentId: string
  isTracking: boolean
}

const associations = ref<DiscussionAssociationResponse[]>([])
const repositoryOptions = ref<GitHubRepositoryOption[]>([])
const filterIssueOptions = ref<GitHubIssueOption[]>([])
const filterDiscussionOptions = ref<GitHubDiscussionOption[]>([])
const formIssueOptions = ref<GitHubIssueOption[]>([])
const formDiscussionOptions = ref<GitHubDiscussionOption[]>([])
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()

const pagination = reactive({
  pageIndex: 1,
  pageSize: 20,
  itemCount: 0,
  totalPages: 0,
})

const filters = reactive({
  repoId: '',
  discussionId: '',
  issueId: '',
  isTracking: 'all',
})

const metadataLoading = reactive({
  repositories: false,
  filterIssues: false,
  filterDiscussions: false,
  formIssues: false,
  formDiscussions: false,
})

const form = reactive<DiscussionAssociationForm>({
  repoId: '',
  discussionId: '',
  issueId: '',
  refCommentId: '',
  isTracking: true,
})

const rules: FormRules<DiscussionAssociationForm> = {
  repoId: [{ required: true, message: '请输入 Repo ID', trigger: 'blur' }],
  discussionId: [{ required: true, message: '请输入 Discussion ID', trigger: 'blur' }],
  issueId: [{ required: true, message: '请输入 Issue ID', trigger: 'blur' }],
}

const dialogTitle = computed(() => editingId.value == null ? '新建关联' : '编辑关联')

async function loadRepositories(keyword = '') {
  metadataLoading.repositories = true
  try {
    const params: { keyword?: string } = {}
    if (keyword.trim()) {
      params.keyword = keyword.trim()
    }

    repositoryOptions.value = await Apis.GitHubMetadata.getRepositories({ params })
  } catch (error) {
    ElMessage.error(`加载仓库失败：${error instanceof Error ? error.message : String(error)}`)
  } finally {
    metadataLoading.repositories = false
  }
}

async function loadIssueOptions(
  repoId: string,
  keyword: string,
  target: Ref<GitHubIssueOption[]>,
  loadingKey: 'filterIssues' | 'formIssues',
) {
  if (!repoId) {
    target.value = []
    return
  }

  metadataLoading[loadingKey] = true
  try {
    const params: { keyword?: string; take: number } = { take: 50 }
    if (keyword.trim()) {
      params.keyword = keyword.trim()
    }

    target.value = await Apis.GitHubMetadata.getIssues({
      pathParams: { repoId },
      params,
    })
  } catch (error) {
    ElMessage.error(`加载 Issue 失败：${error instanceof Error ? error.message : String(error)}`)
  } finally {
    metadataLoading[loadingKey] = false
  }
}

async function loadDiscussionOptions(
  keyword: string,
  target: Ref<GitHubDiscussionOption[]>,
  loadingKey: 'filterDiscussions' | 'formDiscussions',
) {
  metadataLoading[loadingKey] = true
  try {
    const params: { keyword?: string; take: number } = { take: 50 }
    if (keyword.trim()) {
      params.keyword = keyword.trim()
    }

    target.value = await Apis.GitHubMetadata.getDiscussions({ params })
  } catch (error) {
    ElMessage.error(`加载 Discussion 失败：${error instanceof Error ? error.message : String(error)}`)
  } finally {
    metadataLoading[loadingKey] = false
  }
}

function loadFilterIssues(keyword = '') {
  void loadIssueOptions(filters.repoId.trim(), keyword, filterIssueOptions, 'filterIssues')
}

function loadFilterDiscussions(keyword = '') {
  void loadDiscussionOptions(keyword, filterDiscussionOptions, 'filterDiscussions')
}

function loadFormIssues(keyword = '') {
  void loadIssueOptions(form.repoId.trim(), keyword, formIssueOptions, 'formIssues')
}

function loadFormDiscussions(keyword = '') {
  void loadDiscussionOptions(keyword, formDiscussionOptions, 'formDiscussions')
}

function handleRepositoryVisibleChange(visible: boolean) {
  if (visible) {
    void loadRepositories()
  }
}

function handleFilterIssueVisibleChange(visible: boolean) {
  if (visible) {
    loadFilterIssues()
  }
}

function handleFilterDiscussionVisibleChange(visible: boolean) {
  if (visible) {
    loadFilterDiscussions()
  }
}

function handleFormIssueVisibleChange(visible: boolean) {
  if (visible) {
    loadFormIssues()
  }
}

function handleFormDiscussionVisibleChange(visible: boolean) {
  if (visible) {
    loadFormDiscussions()
  }
}

function handleFilterRepoChange() {
  filters.issueId = ''
  filterIssueOptions.value = []
  loadFilterIssues()
}

function handleFormRepoChange() {
  form.issueId = ''
  formIssueOptions.value = []
  loadFormIssues()
}

function formatRepoLabel(option: GitHubRepositoryOption) {
  return `${option.fullName ?? option.name ?? ''} (${option.id ?? ''})`
}

function formatIssueLabel(option: GitHubIssueOption) {
  return `#${option.number ?? '-'} ${option.title ?? ''}`
}

function formatDiscussionLabel(option: GitHubDiscussionOption) {
  return `#${option.number ?? '-'} ${option.title ?? ''}`
}

function buildListParams() {
  const params: {
    pageIndex: number
    pageSize: number
    repoId?: string
    discussionId?: string
    issueId?: string
    isTracking?: boolean
  } = {
    pageIndex: pagination.pageIndex,
    pageSize: pagination.pageSize,
  }

  if (filters.repoId.trim()) {
    params.repoId = filters.repoId.trim()
  }
  if (filters.discussionId.trim()) {
    params.discussionId = filters.discussionId.trim()
  }
  if (filters.issueId.trim()) {
    params.issueId = filters.issueId.trim()
  }
  if (filters.isTracking !== 'all') {
    params.isTracking = filters.isTracking === 'true'
  }

  return params
}

async function loadAssociations() {
  loading.value = true
  try {
    const data = await Apis.DiscussionAssociations.list({ params: buildListParams() }).send(true)
    associations.value = data.items ?? []
    pagination.pageIndex = data.pageIndex ?? pagination.pageIndex
    pagination.pageSize = data.pageSize ?? pagination.pageSize
    pagination.itemCount = data.itemCount ?? 0
    pagination.totalPages = data.totalPages ?? 0
  } catch (error) {
    ElMessage.error(`加载失败：${error instanceof Error ? error.message : String(error)}`)
  } finally {
    loading.value = false
  }
}

function resetForm() {
  editingId.value = null
  form.repoId = ''
  form.discussionId = ''
  form.issueId = ''
  form.refCommentId = ''
  form.isTracking = true
  formRef.value?.clearValidate()
}

function openCreateDialog() {
  resetForm()
  dialogVisible.value = true
  void loadRepositories()
  loadFormDiscussions()
}

async function migrateExistedAssociations() {
  try {
    loading.value = true;
    await ElMessageBox.confirm("你确定要迁移现有的 Discussion 关联状态吗？这可能需要一定的时间。");
    await Apis.DiscussionAssociations.MigrateDiscussions();
  } catch (e) {
    // ignored
  } finally {
    loading.value = false;
  }
}

function openEditDialog(row: DiscussionAssociationResponse) {
  if (row.id == null) {
    ElMessage.error('关联 ID 缺失，无法编辑')
    return
  }

  editingId.value = row.id
  form.repoId = row.repoId ?? ''
  form.discussionId = row.discussionId ?? ''
  form.issueId = row.issueId ?? ''
  form.refCommentId = row.refCommentId ?? ''
  form.isTracking = row.isTracking ?? true
  formRef.value?.clearValidate()
  dialogVisible.value = true
  void loadRepositories(row.repoId ?? '')
  loadFormIssues(row.issueId ?? '')
  loadFormDiscussions(row.discussionId ?? '')
}

function buildPayload(): DiscussionAssociationRequest {
  return {
    repoId: form.repoId.trim(),
    discussionId: form.discussionId.trim(),
    issueId: form.issueId.trim(),
    refCommentId: form.refCommentId.trim() || null,
    isTracking: form.isTracking,
  }
}

async function submitForm() {
  const valid = await formRef.value?.validate()
  if (!valid) {
    return
  }

  let shouldRefresh = false
  saving.value = true
  try {
    const payload = buildPayload()
    const id = editingId.value
    if (id == null) {
      await Apis.DiscussionAssociations.create({ data: payload })
    } else {
      await Apis.DiscussionAssociations.update({
        pathParams: { id },
        data: payload,
      })
    }

    ElMessage.success(id == null ? '已创建关联' : '已更新关联')
    shouldRefresh = true
    dialogVisible.value = false
  } catch (error) {
    ElMessage.error(`保存失败：${error instanceof Error ? error.message : String(error)}`)
  } finally {
    saving.value = false
  }

  if (shouldRefresh) {
    await loadAssociations()
  }
}

async function deleteAssociation(row: DiscussionAssociationResponse) {
  if (row.id == null) {
    ElMessage.error('关联 ID 缺失，无法删除')
    return
  }

  try {
    await ElMessageBox.confirm(
      `确定删除关联 #${row.id} 吗？`,
      '删除关联',
      {
        confirmButtonText: '删除',
        cancelButtonText: '取消',
        type: 'warning',
      },
    )
  } catch {
    return
  }

  loading.value = true
  try {
    await Apis.DiscussionAssociations.delete({ pathParams: { id: row.id } })
    ElMessage.success('已删除关联')
    if (associations.value.length === 1 && pagination.pageIndex > 1) {
      pagination.pageIndex -= 1
    }
    await loadAssociations()
  } catch (error) {
    ElMessage.error(`删除失败：${error instanceof Error ? error.message : String(error)}`)
  } finally {
    loading.value = false
  }
}

function applyFilters() {
  pagination.pageIndex = 1
  void loadAssociations()
}

function resetFilters() {
  filters.repoId = ''
  filters.discussionId = ''
  filters.issueId = ''
  filters.isTracking = 'all'
  applyFilters()
}

function handlePageChange(page: number) {
  pagination.pageIndex = page
  void loadAssociations()
}

function handlePageSizeChange(pageSize: number) {
  pagination.pageSize = pageSize
  pagination.pageIndex = 1
  void loadAssociations()
}

onMounted(() => {
  void loadAssociations()
  void loadRepositories()
})
</script>

<template>
  <section class="flex flex-col gap-[18px]">
    <div class="flex items-start justify-between gap-4 max-[720px]:flex-col max-[720px]:items-stretch">
      <div>
        <h2 class="m-0 text-2xl font-[650] text-[var(--el-text-color-primary)]">Discussion 关联</h2>
        <p class="mb-0 mt-1.5 text-[var(--el-text-color-secondary)]">管理 Issue 与 GitHub Discussion 的关联记录。</p>
      </div>
      <div>
        <el-button :icon="Refresh" @click="migrateExistedAssociations" :loading="loading">
          迁移现有关联
        </el-button>
        <el-button type="primary" :icon="Plus" @click="openCreateDialog" :loading="loading">
          新建关联
        </el-button>
      </div>
    </div>

    <el-form
      class="rounded-lg border border-[var(--el-border-color-lighter)] bg-[var(--el-bg-color)] p-4 max-[720px]:[&_.el-form-item]:!mr-0 max-[720px]:[&_.el-form-item]:!block"
      :model="filters"
      inline
    >
      <el-form-item label="Repo ID">
        <el-select
          v-model="filters.repoId"
          class="w-[300px]"
          clearable
          filterable
          remote
          allow-create
          default-first-option
          placeholder="按仓库节点筛选"
          :remote-method="loadRepositories"
          :loading="metadataLoading.repositories"
          @change="handleFilterRepoChange"
          @visible-change="handleRepositoryVisibleChange"
        >
          <el-option
            v-for="option in repositoryOptions"
            :key="option.id"
            :label="formatRepoLabel(option)"
            :value="option.id"
          >
            <div class="overflow-hidden text-ellipsis whitespace-nowrap text-[var(--el-text-color-primary)]">
              {{ option.fullName ?? option.name ?? '' }}
            </div>
            <div class="overflow-hidden text-ellipsis whitespace-nowrap text-xs leading-[1.2] text-[var(--el-text-color-secondary)]">
              {{ option.id ?? '' }}
            </div>
          </el-option>
        </el-select>
      </el-form-item>
      <el-form-item label="Discussion ID">
        <el-select
          v-model="filters.discussionId"
          class="w-[260px]"
          clearable
          filterable
          remote
          allow-create
          default-first-option
          placeholder="按节点 ID 或 #编号筛选"
          :remote-method="loadFilterDiscussions"
          :loading="metadataLoading.filterDiscussions"
          @visible-change="handleFilterDiscussionVisibleChange"
        >
          <el-option
            v-for="option in filterDiscussionOptions"
            :key="option.id"
            :label="formatDiscussionLabel(option)"
            :value="option.id"
          >
            <div class="overflow-hidden text-ellipsis whitespace-nowrap text-[var(--el-text-color-primary)]">
              {{ formatDiscussionLabel(option) }}
            </div>
            <div class="overflow-hidden text-ellipsis whitespace-nowrap text-xs leading-[1.2] text-[var(--el-text-color-secondary)]">
              {{ option.state ? `${option.state} · ` : '' }}{{ option.id ?? '' }}
            </div>
          </el-option>
        </el-select>
      </el-form-item>
      <el-form-item label="Issue ID">
        <el-select
          v-model="filters.issueId"
          class="w-[260px]"
          clearable
          filterable
          remote
          allow-create
          default-first-option
          placeholder="按节点 ID 或 #编号筛选"
          :disabled="!filters.repoId"
          :remote-method="loadFilterIssues"
          :loading="metadataLoading.filterIssues"
          @visible-change="handleFilterIssueVisibleChange"
        >
          <el-option
            v-for="option in filterIssueOptions"
            :key="option.id"
            :label="formatIssueLabel(option)"
            :value="option.id"
          >
            <div class="overflow-hidden text-ellipsis whitespace-nowrap text-[var(--el-text-color-primary)]">
              {{ formatIssueLabel(option) }}
            </div>
            <div class="overflow-hidden text-ellipsis whitespace-nowrap text-xs leading-[1.2] text-[var(--el-text-color-secondary)]">
              {{ option.state ?? '' }} · {{ option.id ?? '' }}
            </div>
          </el-option>
        </el-select>
      </el-form-item>
      <el-form-item label="状态">
        <el-select v-model="filters.isTracking" class="w-32">
          <el-option label="全部" value="all" />
          <el-option label="追踪中" value="true" />
          <el-option label="已停止" value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" :icon="Search" @click="applyFilters">
          查询
        </el-button>
        <el-button :icon="Refresh" @click="resetFilters">
          重置
        </el-button>
      </el-form-item>
    </el-form>

    <el-table
      v-loading="loading"
      :data="associations"
      row-key="id"
      border
      class="w-full"
      empty-text="暂无关联记录"
    >
      <el-table-column prop="id" label="ID" width="88" />
      <el-table-column prop="repoId" label="Repo ID" min-width="180" show-overflow-tooltip />
      <el-table-column prop="discussionId" label="Discussion ID" min-width="220" show-overflow-tooltip />
      <el-table-column prop="issueId" label="Issue ID" min-width="220" show-overflow-tooltip />
      <el-table-column prop="refCommentId" label="Ref Comment ID" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          <span class="text-[var(--el-text-color-secondary)]">{{ row.refCommentId || '未设置' }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="isTracking" label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isTracking ? 'success' : 'info'" effect="plain">
            {{ row.isTracking ? '追踪中' : '已停止' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button :icon="Edit" text type="primary" @click="openEditDialog(row)">
            编辑
          </el-button>
          <el-button :icon="Delete" text type="danger" @click="deleteAssociation(row)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <div class="flex justify-end">
      <el-pagination
        background
        layout="total, sizes, prev, pager, next"
        :total="pagination.itemCount"
        :current-page="pagination.pageIndex"
        :page-size="pagination.pageSize"
        :page-sizes="[10, 20, 50, 100]"
        @current-change="handlePageChange"
        @size-change="handlePageSizeChange"
      />
    </div>

    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="620px" @closed="resetForm">
      <el-form
        ref="formRef"
        :model="form"
        :rules="rules"
        label-position="top"
        class="[&_.el-form-item:last-child]:!mb-0"
      >
        <el-form-item label="Repo ID" prop="repoId">
          <el-select
            v-model="form.repoId"
            class="w-full"
            clearable
            filterable
            remote
            allow-create
            default-first-option
            placeholder="选择或输入仓库节点 ID"
            :remote-method="loadRepositories"
            :loading="metadataLoading.repositories"
            @change="handleFormRepoChange"
            @visible-change="handleRepositoryVisibleChange"
          >
            <el-option
              v-for="option in repositoryOptions"
              :key="option.id"
              :label="formatRepoLabel(option)"
              :value="option.id"
            >
              <div class="overflow-hidden text-ellipsis whitespace-nowrap text-[var(--el-text-color-primary)]">
                {{ option.fullName ?? option.name ?? '' }}
              </div>
              <div class="overflow-hidden text-ellipsis whitespace-nowrap text-xs leading-[1.2] text-[var(--el-text-color-secondary)]">
                {{ option.id ?? '' }}
              </div>
            </el-option>
          </el-select>
        </el-form-item>
        <el-form-item label="Discussion ID" prop="discussionId">
          <el-select
            v-model="form.discussionId"
            class="w-full"
            clearable
            filterable
            remote
            allow-create
            default-first-option
            placeholder="选择或输入节点 ID / #编号"
            :remote-method="loadFormDiscussions"
            :loading="metadataLoading.formDiscussions"
            @visible-change="handleFormDiscussionVisibleChange"
          >
            <el-option
              v-for="option in formDiscussionOptions"
              :key="option.id"
              :label="formatDiscussionLabel(option)"
              :value="option.id"
            >
              <div class="overflow-hidden text-ellipsis whitespace-nowrap text-[var(--el-text-color-primary)]">
                {{ formatDiscussionLabel(option) }}
              </div>
              <div class="overflow-hidden text-ellipsis whitespace-nowrap text-xs leading-[1.2] text-[var(--el-text-color-secondary)]">
                {{ option.state ? `${option.state} · ` : '' }}{{ option.id ?? '' }}
              </div>
            </el-option>
          </el-select>
        </el-form-item>
        <el-form-item label="Issue ID" prop="issueId">
          <el-select
            v-model="form.issueId"
            class="w-full"
            clearable
            filterable
            remote
            allow-create
            default-first-option
            placeholder="选择或输入节点 ID / #编号"
            :disabled="!form.repoId"
            :remote-method="loadFormIssues"
            :loading="metadataLoading.formIssues"
            @visible-change="handleFormIssueVisibleChange"
          >
            <el-option
              v-for="option in formIssueOptions"
              :key="option.id"
              :label="formatIssueLabel(option)"
              :value="option.id"
            >
              <div class="overflow-hidden text-ellipsis whitespace-nowrap text-[var(--el-text-color-primary)]">
                {{ formatIssueLabel(option) }}
              </div>
              <div class="overflow-hidden text-ellipsis whitespace-nowrap text-xs leading-[1.2] text-[var(--el-text-color-secondary)]">
                {{ option.state ?? '' }} · {{ option.id ?? '' }}
              </div>
            </el-option>
          </el-select>
        </el-form-item>
        <el-form-item label="Ref Comment ID" prop="refCommentId">
          <el-input v-model="form.refCommentId" clearable placeholder="可留空" />
        </el-form-item>
        <el-form-item label="追踪状态" prop="isTracking">
          <el-switch
            v-model="form.isTracking"
            active-text="追踪中"
            inactive-text="已停止"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">
          取消
        </el-button>
        <el-button type="primary" :loading="saving" @click="submitForm">
          保存
        </el-button>
      </template>
    </el-dialog>
  </section>
</template>
