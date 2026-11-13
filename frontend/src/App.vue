<script setup lang="ts">
import { computed, ref } from 'vue'

type Row = Record<string, unknown>
type View = { metadata: Row, tables: Record<string, Row[]>, warnings: string[], hiddenSectors: number }
type RecordSummary = { id: number, type: string, kind: string }
type RecordDetail = RecordSummary & { fields?: Row, shape?: number[], value?: unknown, items?: unknown[], itemCount?: number }

const id = ref('')
const view = ref<View>()
const hasInfo = ref(false)
const spoilers = ref(false)
const error = ref('')
const busy = ref(false)
const table = ref('')
const query = ref('')
const page = ref(0)
const schema = ref(false)
const source = ref('main')
const schemaQuery = ref('')
const records = ref<RecordSummary[]>([])
const total = ref(0)
const root = ref(0)
const schemaPage = ref(0)
const detail = ref<RecordDetail>()
const itemPage = ref(0)
const base = computed(() => '/api/' + id.value)
const visibleRows = computed(() => (view.value?.tables[table.value] ?? []).filter(row =>
  !query.value || Object.values(row).some(value => show(value).toLowerCase().includes(query.value.toLowerCase()))))
const columns = computed(() => [...new Set((view.value?.tables[table.value] ?? []).flatMap(Object.keys))])
const exportQuery = computed(() => '?spoilers=' + spoilers.value)

function show(value: unknown) {
  if (value === null || value === undefined) return 'Unknown'
  return typeof value === 'object' ? JSON.stringify(value) : String(value)
}
function link(value: unknown): number | undefined {
  if (value && typeof value === 'object' && 'ref' in value && typeof value.ref === 'number') return value.ref
}
async function api<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, options)
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new Error(problem.error ?? 'Request failed (' + response.status + ').')
  }
  return response.json()
}
async function run(action: () => Promise<void>) {
  busy.value = true
  error.value = ''
  try { await action() }
  catch (failure) { error.value = String(failure instanceof Error ? failure.message : failure) }
  finally { busy.value = false }
}
async function upload(event: SubmitEvent) {
  const data = new FormData(event.currentTarget as HTMLFormElement)
  if (!(data.get('info') as File | null)?.size) data.delete('info')
  await run(async () => {
    const response = await api<{ id: string, view: View, hasInfo: boolean }>('/api/import', {
      method: 'POST', body: data, headers: { 'X-Star-Valor': 'import' },
    })
    id.value = response.id
    view.value = response.view
    hasInfo.value = response.hasInfo
    spoilers.value = false
    schema.value = false
    detail.value = undefined
    source.value = 'main'
    table.value = Object.keys(response.view.tables)[0] ?? ''
    query.value = ''
    page.value = 0
  })
}
async function toggle(event: Event) {
  const checkbox = event.target as HTMLInputElement
  const requested = checkbox.checked
  checkbox.checked = spoilers.value
  await run(async () => {
    const next = await api<View>(base.value + '/view?spoilers=' + requested)
    spoilers.value = requested
    checkbox.checked = requested
    view.value = next
    if (!(table.value in next.tables)) table.value = Object.keys(next.tables)[0] ?? ''
    page.value = 0
    if (!requested) { schema.value = false; detail.value = undefined; records.value = [] }
  })
}
async function searchSchema(nextPage = 0) {
  await run(async () => {
    const data = await api<{ total: number, records: RecordSummary[], root: number }>(
      base.value + '/schema?spoilers=true&source=' + source.value + '&offset=' + nextPage * 100 + '&q=' + encodeURIComponent(schemaQuery.value))
    total.value = data.total
    records.value = data.records
    root.value = data.root
    schemaPage.value = nextPage
    detail.value = undefined
    schema.value = true
  })
}
async function openRecord(number: number, nextPage = 0) {
  await run(async () => {
    detail.value = await api<RecordDetail>(base.value + '/schema?spoilers=true&source=' + source.value +
      '&record=' + number + '&itemOffset=' + nextPage * 100)
    itemPage.value = nextPage
  })
}
</script>

<template>
  <main class="mx-auto max-w-7xl space-y-6 px-5 py-8">
    <header>
      <p class="text-sm font-medium text-cyan-300">Star Valor Galactic Intelligence</p>
      <h1 class="text-3xl font-semibold">Save Explorer</h1>
    </header>
    <form class="flex flex-wrap items-end gap-4 rounded-lg border border-slate-700 p-4" @submit.prevent="upload">
      <label class="grid gap-1 text-sm">Main save <input required name="save" type="file" accept=".dat"></label>
      <label class="grid gap-1 text-sm">Companion Info (optional) <input name="info" type="file" accept=".dat"></label>
      <label class="grid gap-1 text-sm">Mechanics profile <select name="mechanics"><option>Unknown</option><option>Vanilla</option><option>Galaxy Reforged</option></select></label>
      <button :disabled="busy" type="submit">Import save</button>
    </form>
    <p v-if="busy" role="status" class="text-cyan-300">Loading…</p>
    <p v-if="error" role="alert" class="text-red-300">{{ error }}</p>
    <template v-if="view">
      <section aria-label="Player metadata" class="grid gap-3 sm:grid-cols-3 lg:grid-cols-4">
        <div v-for="(value, key) in view.metadata" :key="key" class="rounded border border-slate-800 p-3">
          <span class="text-sm text-slate-400">{{ ({ shipModelID: 'Ship model ID', savedVersion: 'Game version', baseBuilding: 'Base Building DLC', marketLayout: 'Market layout', info: 'Companion Info' } as Record<string,string>)[key] ?? key }}</span>
          <details v-if="key === 'info'"><summary>File metadata</summary><pre>{{ JSON.stringify(value, null, 2) }}</pre></details>
          <div v-else class="font-medium">{{ show(value) }}</div>
        </div>
      </section>
      <p v-for="warning in view.warnings" :key="warning" class="text-amber-300">{{ warning }}</p>
      <div class="flex flex-wrap items-center gap-3">
        <label class="flex items-center gap-2"><input type="checkbox" :checked="spoilers" :disabled="busy" @change="toggle"> Show spoilers and raw records</label>
        <span class="text-sm text-slate-400">{{ view.hiddenSectors }} sectors with discovery unconfirmed or false</span>
        <button :disabled="busy" @click="schema = false">Tables</button>
        <button v-if="spoilers" :disabled="busy" @click="searchSchema()">Schema</button>
        <a class="action" :href="base + '/export.json' + exportQuery">Export JSON</a>
        <a v-if="spoilers" class="action" :href="base + '/export.json' + exportQuery + '&raw=true'">Export raw JSON</a>
      </div>
      <section v-if="!schema && table" class="space-y-3">
        <div class="flex flex-wrap items-center gap-3">
          <select v-model="table" aria-label="Table" @change="page = 0; query = ''"><option v-for="(_, name) in view.tables" :key="name">{{ name }}</option></select>
          <input v-model="query" aria-label="Filter table" placeholder="Filter table" @input="page = 0">
          <a class="action" :href="base + '/export.csv' + exportQuery + '&table=' + encodeURIComponent(table)">Export table CSV</a>
        </div>
        <div class="overflow-x-auto rounded border border-slate-800">
          <table class="w-full whitespace-nowrap text-sm">
            <thead class="bg-slate-900"><tr><th v-for="key in columns" :key="key">{{ key }}</th></tr></thead>
            <tbody><tr v-for="(row, index) in visibleRows.slice(page * 100, (page + 1) * 100)" :key="index">
              <td v-for="key in columns" :key="key">{{ key in row ? show(row[key]) : '' }}</td>
            </tr></tbody>
          </table>
        </div>
        <div class="flex items-center gap-3">
          <button :disabled="page === 0" @click="page--">Previous</button>
          <button :disabled="(page + 1) * 100 >= visibleRows.length" @click="page++">Next</button>
          <span>Page {{ page + 1 }} · {{ visibleRows.length }} rows</span>
        </div>
      </section>
      <section v-if="schema" class="space-y-3">
        <form class="flex flex-wrap gap-3" @submit.prevent="searchSchema()">
          <select v-model="source" aria-label="Record source" :disabled="busy" @change="searchSchema()"><option value="main">Main</option><option v-if="hasInfo" value="info">Companion Info</option></select>
          <input v-model="schemaQuery" aria-label="Search schema" placeholder="Type, member or record ID">
          <button :disabled="busy">Search</button>
          <button type="button" :disabled="busy" @click="openRecord(root)">Open root</button>
        </form>
        <div v-if="detail" class="space-y-3 rounded border border-slate-700 p-4">
          <h2 class="break-all font-semibold">#{{ detail.id }} {{ detail.type }}</h2>
          <p v-if="detail.shape">Shape {{ detail.shape.join(' × ') }}</p>
          <p v-if="detail.kind === 'primitive'">{{ show(detail.value) }}</p>
          <dl class="grid grid-cols-[minmax(5rem,1fr)_minmax(0,2fr)] items-center gap-1 text-sm">
            <template v-for="(value, key) in detail.fields ?? detail.items ?? {}" :key="key">
              <dt class="break-all text-slate-400">{{ typeof key === 'number' ? key + itemPage * 100 : key }}</dt>
              <dd><button v-if="link(value)" :disabled="busy" @click="openRecord(link(value)!)">Record #{{ link(value) }}</button><span v-else class="break-all">{{ show(value) }}</span></dd>
            </template>
          </dl>
          <div v-if="detail.itemCount" class="flex items-center gap-3">
            <button :disabled="busy || itemPage === 0" @click="openRecord(detail.id, itemPage - 1)">Previous items</button>
            <button :disabled="busy || (itemPage + 1) * 100 >= detail.itemCount" @click="openRecord(detail.id, itemPage + 1)">Next items</button>
            <span>{{ detail.itemCount }} items</span>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button :disabled="busy || schemaPage === 0" @click="searchSchema(schemaPage - 1)">Previous</button>
          <button :disabled="busy || (schemaPage + 1) * 100 >= total" @click="searchSchema(schemaPage + 1)">Next</button>
          <span>{{ total }} matching records · Page {{ schemaPage + 1 }}</span>
        </div>
        <ul class="space-y-1"><li v-for="record in records" :key="record.id">
          <button class="break-all text-left text-sm" :disabled="busy" @click="openRecord(record.id)">#{{ record.id }} {{ record.type }}</button>
        </li></ul>
      </section>
    </template>
  </main>
</template>
