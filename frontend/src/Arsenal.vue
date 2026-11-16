<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { label, number, type Identity, type Evidence, type Snapshot } from './intelligence'
const props = defineProps<{ snapshot: Snapshot }>()
type Row = { item: Identity; rarity: number | null; storage: string; scope: string; fitted: boolean; quantity: number | null; records: number; evidence: Evidence[]; baseValue: number | null; rarityValue: number | null; rarityTotal: number | null; replacementPerItem: number | null; liquidationPerItem: number | null; replacementValue: number | null; liquidationValue: number | null; priceConfidence: string; priceSource: string | null }
type Report = { storedGearBaseValue: number | null; pricedGearGroups: number; storedGearGroups: number; items: Row[]; distinctModels: number; knownQuantity: number | null; playerGroups: number; missingReplacementValues: number; missingLiquidationValues: number; unknownQuantityRows: number; replacementCoverage: number; liquidationCoverage: number; knownReplacementValue: number | null; knownLiquidationValue: number | null; totalsScope: string }
const report = ref<Report>(), error = ref(''), query = ref(''), kind = ref(''), rarity = ref(''), storage = ref(''), scope = ref('Player'), minimum = ref<number | ''>(''), fitted = ref(''), sort = ref('name'), descending = ref(false), page = ref(1)
let revision = 0
async function load(catalog?: unknown) {
  const current = ++revision
  error.value = ''
  try {
    const response = await fetch(`/api/${props.snapshot.provenance.snapshotId}/arsenal${catalog ? '/value' : ''}?spoilers=${props.snapshot.provenance.includesSpoilers}`, catalog ? { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(catalog) } : undefined)
    if (!response.ok) throw new Error(await response.text())
    const result = await response.json()
    if (current === revision) report.value = result
  } catch (e) { if (current === revision) error.value = String(e) }
}
watch(() => props.snapshot, () => { report.value = undefined; page.value = 1; void load() }, { immediate: true })
async function catalogFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  try { await load(JSON.parse(await file.text())) } catch (e) { error.value = String(e) }
}
const categorySummary = computed(() => {
  const rows = (report.value?.items ?? []).filter(i => i.scope === 'Player')
  return [...new Set(rows.map(i => i.item.namespace))].sort().map(namespace => {
    const group = rows.filter(i => i.item.namespace === namespace)
    return { namespace, quantity: group.reduce((sum, i) => sum + (i.quantity ?? 0), 0), models: new Set(group.map(i => i.item.id)).size, unknown: group.filter(i => i.quantity == null).length }
  })
})
const kinds = computed(() => [...new Set(report.value?.items.map(i => i.item.namespace))].sort())
const rarities = computed(() => [...new Set(report.value?.items.map(i => i.rarity == null ? 'Unknown' : String(i.rarity)))].sort())
const storages = computed(() => [...new Set(report.value?.items.map(i => i.storage))].sort())
const filtered = computed(() => (report.value?.items ?? []).filter(i =>
  (!scope.value || i.scope === scope.value) && (!kind.value || i.item.namespace === kind.value) &&
  (!rarity.value || (i.rarity == null ? 'Unknown' : String(i.rarity)) === rarity.value) &&
  (!storage.value || i.storage === storage.value) && (!fitted.value || String(i.fitted) === fitted.value) &&
  (minimum.value === '' || i.quantity != null && i.quantity >= minimum.value) &&
  `${i.item.name ?? ''} ${i.item.id} ${i.item.namespace}`.toLowerCase().includes(query.value.toLowerCase()))
  .sort((a, b) => {
    const av = sort.value === 'name' ? label(a.item) : sort.value === 'quantity' ? a.quantity : sort.value === 'rarity' ? a.rarity : sort.value === 'value' ? a.replacementValue : a.storage
    const bv = sort.value === 'name' ? label(b.item) : sort.value === 'quantity' ? b.quantity : sort.value === 'rarity' ? b.rarity : sort.value === 'value' ? b.replacementValue : b.storage
    if (av == null) return bv == null ? 0 : 1
    if (bv == null) return -1
    return (typeof av === 'number' && typeof bv === 'number' ? av - bv : String(av).localeCompare(String(bv))) * (descending.value ? -1 : 1)
  }))
watch([query, kind, rarity, storage, scope, minimum, fitted, sort, descending], () => { page.value = 1 })
const items = computed(() => filtered.value.filter(i => i.item.namespace !== 'ship-model'))
const ships = computed(() => filtered.value.filter(i => i.item.namespace === 'ship-model'))
const visible = computed(() => items.value.slice((page.value - 1) * 100, page.value * 100))
const breakdownBy = ref<'model' | 'rarity' | 'storage'>('model')
const breakdown = computed(() => {
  const groups = new Map<string, { label: string; units: number; priced: number; total: number }>()
  for (const row of filtered.value) {
    if (row.scope !== 'Player' || !['weapon', 'equipment'].includes(row.item.namespace)) continue
    const key = breakdownBy.value === 'model' ? row.item.namespace + ' · ' + label(row.item)
      : breakdownBy.value === 'rarity' ? String(row.rarity ?? 'Unknown') : row.storage
    const group = groups.get(key) ?? { label: key, units: 0, priced: 0, total: 0 }
    group.units += row.quantity ?? 0
    if (row.rarityTotal != null) { group.priced += row.quantity ?? 0; group.total += row.rarityTotal }
    groups.set(key, group)
  }
  return [...groups.values()].sort((a, b) => b.total - a.total || a.label.localeCompare(b.label))
})
const distinct = computed(() => new Set(filtered.value.map(i => `${i.item.namespace}:${i.item.id}`)).size)
function download(format: 'csv' | 'json') {
  const cell = (v: unknown) => { let s = v == null ? '' : String(v); if (/^[=+@-]/.test(s)) s = "'" + s; return '"' + s.replaceAll('"', '""') + '"' }
  const fields = ['namespace', 'id', 'name', 'rarity', 'storage', 'scope', 'fitted', 'quantity', 'baseValue', 'rarityValue', 'rarityTotal', 'replacementPerItem', 'liquidationPerItem', 'replacementValue', 'liquidationValue', 'priceConfidence', 'priceSource']
  const csv = [fields, ...filtered.value.map(i => [i.item.namespace, i.item.id, i.item.name, i.rarity, i.storage, i.scope, i.fitted, i.quantity, i.baseValue, i.rarityValue, i.rarityTotal, i.replacementPerItem, i.liquidationPerItem, i.replacementValue, i.liquidationValue, i.priceConfidence, i.priceSource])].map(r => r.map(cell).join(',')).join('\r\n')
  const url = URL.createObjectURL(new Blob([format === 'json' ? JSON.stringify({ provenance: props.snapshot.provenance, items: filtered.value }, null, 2) : csv], { type: format === 'json' ? 'application/json' : 'text/csv' }))
  const a = document.createElement('a'); a.href = url; a.download = `arsenal.${format}`; a.click(); URL.revokeObjectURL(url)
}
</script>
<template>
  <section class="space-y-4">
    <p>Weapons and equipment are grouped by model, rarity, location and fitted status. Commodities keep their own namespace; unmapped item types remain unknown. Station market observations are separate from player possessions.</p>
    <p v-if="error" role="alert" class="text-red-300">{{ error }}</p>
    <template v-if="report">
      <p class="text-cyan-200">Player items: {{ number(report.knownQuantity) }} known units · {{ report.distinctModels }} distinct models · {{ report.unknownQuantityRows }} groups with unknown quantities</p>
      <div class="flex flex-wrap gap-3"><button v-for="c in categorySummary" :key="c.namespace" class="p-3 text-left" @click="kind = c.namespace; scope = 'Player'"><strong>{{ c.namespace }}</strong><p>{{ number(c.quantity) }} known units · {{ c.models }} models · {{ c.unknown }} unknown quantity groups</p></button></div>
      <p class="text-lg text-cyan-200">Spare weapons and equipment · {{ number(report.storedGearBaseValue) }} credits at rarity-adjusted base value</p>
      <p class="text-sm text-slate-400">{{ report.pricedGearGroups }} / {{ report.storedGearGroups }} spare gear groups valued.</p>
      <p>Estimated replacement: {{ number(report.knownReplacementValue) }} credits ({{ report.replacementCoverage }} / {{ report.playerGroups }} priced groups; {{ report.missingReplacementValues }} uncovered) · Estimated liquidation: {{ number(report.knownLiquidationValue) }} credits ({{ report.liquidationCoverage }} / {{ report.playerGroups }} priced groups; {{ report.missingLiquidationValues }} uncovered)</p>
      <p class="text-sm text-slate-400">{{ report.totalsScope }}. Unknown prices are excluded, never zero. Estimates use catalog base values and explicit rarity multipliers; modifications, skills, reputation and transaction conditions are not priced.</p>
      <details><summary>Load a local pricing catalog</summary><label class="block py-3">Pricing catalog JSON <input type="file" accept=".json,application/json" @change="catalogFile"></label><p>Catalog version and profile must match this save. Catalogs are transient and require source and confidence labels. The app loads baseline equipment prices automatically for 2.251 saves. Upload a catalog to use save-specific weapon prices.</p><button @click="load()">Restore default pricing</button></details>
      <div class="flex flex-wrap gap-3">
        <input v-model="query" aria-label="Search Arsenal" placeholder="Search name or numeric ID">
        <select v-model="scope" aria-label="Inventory scope"><option value="Player">Player possessions</option><option value="Market observation">Station market observations</option><option value="">All scopes</option></select>
        <select v-model="kind" aria-label="Item type"><option value="">All item types</option><option v-for="k in kinds" :key="k">{{ k }}</option></select>
        <select v-model="rarity" aria-label="Item rarity"><option value="">All rarities</option><option v-for="r in rarities" :key="r">{{ r }}</option></select>
        <select v-model="storage" aria-label="Storage location"><option value="">All locations</option><option v-for="s in storages" :key="s">{{ s }}</option></select>
        <select v-model="fitted" aria-label="Fitted status"><option value="">Fitted and stored</option><option value="true">Fitted</option><option value="false">Stored</option></select>
        <input v-model.number="minimum" type="number" min="0" aria-label="Minimum quantity" placeholder="Minimum quantity">
        <select v-model="sort" aria-label="Arsenal sort"><option value="name">Name / ID</option><option value="quantity">Quantity</option><option value="rarity">Rarity</option><option value="storage">Storage</option><option value="value">Replacement value</option></select>
        <label><input v-model="descending" type="checkbox"> Descending</label>
        <button @click="download('csv')">Export Arsenal CSV</button><button @click="download('json')">Export Arsenal JSON</button>
      </div>
      <details v-if="breakdown.length" class="rounded border border-slate-700 p-3"><summary>Gear value breakdown</summary>
        <label class="block py-2">Group by <select v-model="breakdownBy"><option value="model">Model</option><option value="rarity">Rarity</option><option value="storage">Storage location</option></select></label>
        <div class="max-h-80 overflow-auto"><table class="w-full text-sm"><thead><tr><th>Group</th><th>Known units</th><th>Priced units</th><th>Rarity-adjusted base total</th></tr></thead><tbody><tr v-for="group in breakdown" :key="group.label"><td>{{ group.label }}</td><td>{{ number(group.units) }}</td><td>{{ number(group.priced) }}</td><td>{{ number(group.priced ? group.total : null) }}</td></tr></tbody></table></div>
      </details>
      <p>{{ filtered.length }} matching groups · {{ distinct }} distinct models</p>
      <div class="overflow-auto"><table class="w-full text-sm"><thead><tr><th>Item / raw ID</th><th>Type</th><th>Rarity</th><th>Storage</th><th>Status</th><th>Quantity</th><th>Base / rarity-adjusted unit</th><th>Rarity-adjusted total</th><th>Unit replacement / liquidation</th><th>Replacement / liquidation total</th><th>Evidence</th></tr></thead><tbody><tr v-for="(i, n) in visible" :key="n"><td>{{ label(i.item) }}</td><td>{{ i.item.namespace }}</td><td>{{ number(i.rarity) }}</td><td>{{ i.storage }} · {{ i.scope }}</td><td>{{ i.fitted ? 'Fitted' : 'Stored' }}</td><td>{{ number(i.quantity) }}</td><td>{{ number(i.baseValue) }} / {{ number(i.rarityValue) }}</td><td>{{ number(i.rarityTotal) }}</td><td>{{ number(i.replacementPerItem) }} / {{ number(i.liquidationPerItem) }}</td><td>{{ number(i.replacementValue) }} / {{ number(i.liquidationValue) }}</td><td><details><summary>{{ i.priceConfidence }} · {{ i.priceSource ?? 'No pricing catalog' }}</summary><pre>{{ JSON.stringify(i.evidence, null, 2) }}</pre></details></td></tr></tbody></table></div>
      <p v-if="!items.length">No matching inventory items.</p>
      <div class="flex gap-3"><button :disabled="page <= 1" @click="page--">Previous</button><span>Page {{ page }} / {{ Math.max(1, Math.ceil(items.length / 100)) }}</span><button :disabled="page * 100 >= items.length" @click="page++">Next</button></div>
      <section><h3 class="text-lg">Stored ships</h3><p v-if="!ships.length">No identifiable stored ships in this selection.</p><ul><li v-for="(s, n) in ships" :key="n">{{ label(s.item) }} · {{ s.storage }} · {{ number(s.quantity) }} · {{ s.scope }}</li></ul></section>
    </template>
  </section>
</template>
