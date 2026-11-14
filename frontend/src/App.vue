<script setup lang="ts">
import Command from './Command.vue'
import type { Snapshot } from './intelligence'
import { computed, onMounted, ref, watch } from 'vue'
import { clearRecentSaves, forgetSave, listRecentSaves, rememberSave, type RecentSave } from './recentSaves'

type NameResolution = { rawId: number, category: string, name: string | null, status: string, source: string | null, displayName: string }
type Catalog = {
  sources: { kind: string, label: string, entries: number }[], categories: Record<string, number>, entries: number,
  issues: { code: string, severity: string, source: string, line?: number, section?: number, category?: string, id?: number }[],
  warnings: string[]
}
type Diagnostics = { catalog: Catalog, observed?: { resolved: NameResolution[], referenceNames: NameResolution[], unresolved: NameResolution[] } }
type Row = Record<string, unknown>
type View = { metadata: Row, tables: Record<string, Row[]>, warnings: string[], hiddenSectors: number }
type RecordSummary = { id: number, type: string, kind: string }
type RecordDetail = RecordSummary & { fields?: Row, shape?: number[], value?: unknown, items?: unknown[], itemCount?: number }

const intelligence = ref<Snapshot>()
const baseline = ref<Snapshot>()
const workspace = ref('Command')
const id = ref('')
const view = ref<View>()
const hasInfo = ref(false)
const spoilers = ref(false)
const error = ref('')
const busy = ref(false)
const recent = ref<RecentSave[]>([])
const historyError = ref('')
onMounted(() => updateHistory(() => Promise.resolve()))
const showIds = ref(false)
const diagnosticsOpen = ref(false)
const diagnostics = ref<Diagnostics>()
const diagnosticsError = ref('')
watch([diagnosticsOpen, id, spoilers], async ([open, session, includeSpoilers], _, cleanup) => {
  if (!open) return
  const request = new AbortController()
  cleanup(() => request.abort())
  diagnostics.value = undefined
  diagnosticsError.value = ''
  try {
    const options = { signal: request.signal }
    diagnostics.value = session
      ? await api<Diagnostics>('/api/' + session + '/resolver?spoilers=' + includeSpoilers, options)
      : { catalog: await api<Catalog>('/api/resolver', options) }
  } catch (failure) {
    if (!request.signal.aborted) diagnosticsError.value = String(failure)
  }
})
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
  !query.value || Object.keys(row).filter(isDataField).some(key => [show(row[key]), resolution(row, key)?.displayName]
    .some(value => value?.toLowerCase().includes(query.value.toLowerCase())))))
const columns = computed(() => [...new Set((view.value?.tables[table.value] ?? []).flatMap(Object.keys))].filter(isDataField))
const exportQuery = computed(() => '?spoilers=' + spoilers.value)

function isDataField(key: string) {
  return !key.endsWith('Resolution')
}
function resolution(row: Row, key: string): NameResolution | undefined {
  return row[key + 'Resolution'] as NameResolution | undefined
}
function cell(row: Row, key: string) {
  const result = resolution(row, key)
  if (!result) return show(row[key])
  return showIds.value && result.name ? result.displayName + ' (#' + result.rawId + ')' : result.displayName
}
function hint(row: Row, key: string) {
  const result = resolution(row, key)
  return result ? [result.category + ' #' + result.rawId, result.status, result.source].filter(Boolean).join(' · ') : undefined
}
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
async function updateHistory(change: () => Promise<unknown>) {
  historyError.value = ''
  try {
    await change()
    recent.value = await listRecentSaves()
  } catch {
    historyError.value = 'Recent saves could not be updated. Check your browser storage settings or available space.'
  }
}
async function upload(event: SubmitEvent) {
  const data = new FormData(event.currentTarget as HTMLFormElement)
  const save = data.get('save')
  if (!(save instanceof File)) return
  const companion = data.get('info')
  const info = companion instanceof File && companion.name ? companion : undefined
  const mechanics = String(data.get('mechanics') ?? 'Unknown')
  const keep = data.has('remember')
  const key = JSON.stringify([save.name, save.size, save.lastModified, info?.name, info?.size, info?.lastModified])
  await run(() => importSave({ key, save, info, mechanics, openedAt: Date.now() }, keep))
}
async function reopen(entry: RecentSave) {
  await run(() => importSave(entry, true))
}
async function importSave(entry: RecentSave, keep: boolean) {
  const body = new FormData()
  body.set('save', entry.save)
  if (entry.info) body.set('info', entry.info)
  body.set('mechanics', entry.mechanics)
  const response = await api<{ id: string, view: View, hasInfo: boolean }>('/api/import', {
    method: 'POST', body, headers: { 'X-Star-Valor': 'import' },
  })
  const domain = await api<Snapshot>('/api/' + response.id + '/intelligence?spoilers=false')
  intelligence.value = domain
  workspace.value = 'Command'
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
  const player = response.view.metadata.player
  if (keep) await updateHistory(() => rememberSave({
    ...entry, openedAt: Date.now(), player: typeof player === 'string' ? player : undefined,
  }))
}
async function toggle(event: Event) {
  const checkbox = event.target as HTMLInputElement
  const requested = checkbox.checked
  checkbox.checked = spoilers.value
  await run(async () => {
    const next = await api<View>(base.value + '/view?spoilers=' + requested)
    const domain = await api<Snapshot>(base.value + '/intelligence?spoilers=' + requested)
    intelligence.value = domain
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
      <h1 class="text-3xl font-semibold">Galactic Command</h1>
      <p v-if="intelligence" class="mt-2 text-sm text-slate-400">{{ intelligence.player.name ?? 'Unknown commander' }} · {{ intelligence.player.credits == null ? 'Unknown credits' : intelligence.player.credits.toLocaleString() + ' credits' }}</p>
    </header>
    <details :open="!view" class="rounded-lg border border-slate-700 p-3">
      <summary class="cursor-pointer font-medium">{{ view ? 'Import another save / recent saves' : 'Import a save' }}</summary>
    <form class="mt-3 flex flex-wrap items-end gap-4 rounded-lg border border-slate-700 p-4" @submit.prevent="upload">
      <label class="grid gap-1 text-sm">Main save <input required name="save" type="file" accept=".dat"></label>
      <label class="grid gap-1 text-sm">Companion Info (optional) <input name="info" type="file" accept=".dat"></label>
      <label class="grid gap-1 text-sm">Mechanics profile <select name="mechanics"><option>Unknown</option><option>Vanilla</option><option>Galaxy Reforged</option></select></label>
      <button :disabled="busy" type="submit">Import save</button>
      <label class="flex w-full items-center gap-2 text-sm">
        <input name="remember" type="checkbox" checked> Keep a browser copy for quick loading
      </label>
    </form>
    <section v-if="recent.length" aria-labelledby="recent-saves" class="space-y-3 rounded border border-slate-700 p-4">
      <div class="flex items-center justify-between gap-3">
        <h2 id="recent-saves" class="font-semibold">Recent saves</h2>
        <button :disabled="busy" @click="run(() => updateHistory(clearRecentSaves))">Clear history</button>
      </div>
      <p class="text-sm text-slate-400">Opens the last imported copy. Import the file again to load newer progress.</p>
      <ul class="divide-y divide-slate-800">
        <li v-for="entry in recent" :key="entry.key" class="flex flex-wrap items-center gap-2 py-2">
          <div class="min-w-0 flex-1 break-words">
            <p>{{ entry.player ? entry.player + ' · ' : '' }}{{ entry.save.name }}</p>
            <p class="text-sm text-slate-400">
              {{ entry.mechanics }} · {{ new Date(entry.openedAt).toLocaleString() }}
              <span v-if="entry.info"> · Companion Info</span>
            </p>
          </div>
          <button :disabled="busy" @click="reopen(entry)">Quick load</button>
          <button :disabled="busy" :aria-label="'Remove ' + entry.save.name + ' from history'"
            @click="run(() => updateHistory(() => forgetSave(entry.key)))">Remove</button>
        </li>
      </ul>
    </section>
    <p v-if="historyError" role="status" class="text-amber-300">{{ historyError }}</p>
    </details>
    <p v-if="busy" role="status" class="text-cyan-300">Loading…</p>
    <p v-if="error" role="alert" class="text-red-300">{{ error }}</p>
    <div class="flex flex-wrap items-center gap-4">
      <button :aria-expanded="diagnosticsOpen" aria-controls="resolver-diagnostics" @click="diagnosticsOpen = !diagnosticsOpen">
        {{ diagnosticsOpen ? 'Hide resolver diagnostics' : 'Resolver diagnostics' }}
      </button>
      <label v-if="view && workspace === 'Save Explorer'" class="flex items-center gap-2"><input v-model="showIds" type="checkbox"> Show original IDs</label>
    </div>
    <section v-if="diagnosticsOpen" id="resolver-diagnostics" class="space-y-3 rounded border border-slate-700 p-4">
      <h2 class="font-semibold">Resolver diagnostics</h2>
      <p v-if="diagnosticsError" role="alert" class="text-red-300">{{ diagnosticsError }}</p>
      <p v-else-if="!diagnostics" role="status">Loading resolver diagnostics…</p>
      <template v-else>
        <p>{{ diagnostics.catalog.entries }} catalog entries across {{ Object.keys(diagnostics.catalog.categories).length }} categories</p>
        <ul class="text-sm">
          <li v-for="(source, index) in diagnostics.catalog.sources" :key="index">
            {{ source.label }} · {{ source.entries }} active entries
          </li>
        </ul>
        <p v-for="warning in diagnostics.catalog.warnings" :key="warning" class="text-amber-300">{{ warning }}</p>
        <details v-if="diagnostics.catalog.issues.length">
          <summary>{{ diagnostics.catalog.issues.length }} catalog issues and conflicts</summary>
          <ul class="max-h-64 overflow-auto text-sm">
            <li v-for="(issue, index) in diagnostics.catalog.issues" :key="index">
              {{ issue.severity }} · {{ issue.code }} · {{ issue.source }}
              <span v-if="issue.line"> · Line {{ issue.line }}</span>
              <span v-if="issue.section != null"> · Section {{ issue.section }}</span>
              <span v-if="issue.category"> · {{ issue.category }}</span>
              <span v-if="issue.id != null"> #{{ issue.id }}</span>
            </li>
          </ul>
        </details>
        <template v-if="diagnostics.observed">
          <details v-for="(entries, status) in diagnostics.observed" :key="status">
            <summary>{{ entries.length }} {{ status === 'referenceNames' ? 'base-game reference' : status }} identifiers in this view</summary>
            <ul class="max-h-64 overflow-auto text-sm">
              <li v-for="entry in entries" :key="entry.category + ':' + entry.rawId">
                {{ entry.category }} #{{ entry.rawId }} · {{ entry.name ?? entry.status }}
              </li>
            </ul>
          </details>
        </template>
      </template>
    </section>
    <template v-if="view">
      <nav class="flex gap-3" aria-label="Workspace"><button :aria-pressed="workspace === 'Command'" @click="workspace = 'Command'">Command</button><button :aria-pressed="workspace === 'Save Explorer'" @click="workspace = 'Save Explorer'">Save Explorer</button></nav>
      <section v-if="workspace === 'Save Explorer'" aria-label="Player metadata" class="grid gap-3 sm:grid-cols-3 lg:grid-cols-4">
        <div v-for="key in Object.keys(view.metadata).filter(isDataField)" :key="key" class="rounded border border-slate-800 p-3">
          <span class="text-sm text-slate-400">{{ ({ shipModelID: 'Ship model', savedVersion: 'Game version', baseBuilding: 'Base Building DLC', marketLayout: 'Market layout', info: 'Companion Info' } as Record<string,string>)[key] ?? key }}</span>
          <details v-if="key === 'info'"><summary>File metadata</summary><pre>{{ JSON.stringify(view.metadata[key], null, 2) }}</pre></details>
          <div v-else class="font-medium" :title="hint(view.metadata, key)">{{ cell(view.metadata, key) }}</div>
        </div>
      </section>
      <p v-for="warning in view.warnings" :key="warning" class="text-amber-300">{{ warning }}</p>
      <div class="flex flex-wrap items-center gap-3">
        <label class="flex items-center gap-2"><input type="checkbox" :checked="spoilers" :disabled="busy" @change="toggle"> Show spoilers and raw records</label>
        <span class="text-sm text-slate-400">{{ view.hiddenSectors }} sectors with discovery unconfirmed or false</span>
        <button v-if="workspace === 'Save Explorer'" :disabled="busy" @click="schema = false">Tables</button>
        <button v-if="spoilers && workspace === 'Save Explorer'" :disabled="busy" @click="searchSchema()">Schema</button>
        <a class="action" :href="base + '/intelligence' + exportQuery" download="galactic-intelligence.json">Export intelligence JSON</a>
        <a class="action" :href="base + '/export.json' + exportQuery">Export JSON</a>
        <a v-if="spoilers" class="action" :href="base + '/export.json' + exportQuery + '&raw=true'">Export raw JSON</a>
      </div>
      <Command v-if="workspace === 'Command' && intelligence" :snapshot="intelligence" :baseline="baseline" @pin="baseline = intelligence.provenance.includesSpoilers ? undefined : intelligence" @clear="baseline = undefined" />
      <section v-if="workspace === 'Save Explorer' && !schema && table" class="space-y-3">
        <div class="flex flex-wrap items-center gap-3">
          <select v-model="table" aria-label="Table" @change="page = 0; query = ''"><option v-for="(_, name) in view.tables" :key="name">{{ name }}</option></select>
          <input v-model="query" aria-label="Filter table" placeholder="Filter by name or ID" @input="page = 0">
          <a class="action" :href="base + '/export.csv' + exportQuery + '&table=' + encodeURIComponent(table)">Export table CSV</a>
        </div>
        <div class="overflow-x-auto rounded border border-slate-800">
          <table class="w-full whitespace-nowrap text-sm">
            <thead class="bg-slate-900"><tr><th v-for="key in columns" :key="key">{{ key }}</th></tr></thead>
            <tbody><tr v-for="(row, index) in visibleRows.slice(page * 100, (page + 1) * 100)" :key="index">
              <td v-for="key in columns" :key="key" :title="hint(row, key)">{{ key in row ? cell(row, key) : '' }}</td>
            </tr></tbody>
          </table>
        </div>
        <div class="flex items-center gap-3">
          <button :disabled="page === 0" @click="page--">Previous</button>
          <button :disabled="(page + 1) * 100 >= visibleRows.length" @click="page++">Next</button>
          <span>Page {{ page + 1 }} · {{ visibleRows.length }} rows</span>
        </div>
      </section>
      <section v-if="workspace === 'Save Explorer' && schema" class="space-y-3">
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
