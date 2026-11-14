<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { label, number, sectorLabel, type Snapshot, type Market } from './intelligence'
const props = defineProps<{ snapshot: Snapshot }>()
const query = ref(''), source = ref('Sector prices'), sector = ref(''), station = ref(''), faction = ref(''), sort = ref('commodity'), descending = ref(false), page = ref(0)
const stationIndex = computed(() => new Map(props.snapshot.stations.filter(s => s.evidence.graphRecord != null).map(s => [s.evidence.graphRecord!, s])))
const stationsByGameId = computed(() => {
  const counts = new Map<number, number>()
  for (const s of props.snapshot.stations) counts.set(s.id, (counts.get(s.id) ?? 0) + 1)
  return new Map(props.snapshot.stations.filter(s => s.id >= 0 && counts.get(s.id) === 1).map(s => [s.id, s]))
})
const sectorIndex = computed(() => new Map(props.snapshot.sectors.map(s => [s.id, s])))
const inventoryMode = computed(() => source.value === 'Station stock' || source.value === 'Station market modules' || source.value === 'Legacy station market')
const factions = computed(() => [...new Map(props.snapshot.stations.filter(s => s.faction).map(s => [s.faction!.id, s.faction!])).values()])
const observations = computed(() => [
  ...props.snapshot.markets.map(m => ({ ...m, identity: m.commodity, stock: null, stationRecord: m.stationId == null || m.sectorId == null ? null : stationsByGameId.value.get(m.stationId)?.evidence.graphRecord ?? null })),
  ...props.snapshot.inventories.map(i => ({ identity: i.item, source: i.source, stationId: i.stationId, sectorId: i.sectorId ?? null, stationRecord: i.stationRecord ?? null, buyingPrice: null, sellingPrice: null, tradePrice: null, codexPrice: null, stock: i.stock, evidence: i.evidence, buyingOverstocked: null, resourceStarved: null, trading: null, tradingOverstocked: null }))
])
function anomaly(m: { buyingPrice: number | null; sellingPrice: number | null; tradePrice: number | null; codexPrice: number | null; stock: number | null }) {
  return [m.buyingPrice, m.sellingPrice, m.tradePrice, m.codexPrice, m.stock].some(v => v != null && v < 0) ? 'Negative / sentinel value; excluded from spread calculations' : ''
}
const rows = computed(() => {
  const result = observations.value.filter(m => m.source === source.value &&
    (!query.value || `${label(m.identity)} ${m.stationId ?? ''}`.toLowerCase().includes(query.value.toLowerCase())) &&
    (!sector.value || String(m.sectorId) === sector.value) && (!station.value || String(m.stationRecord) === station.value) &&
    (!faction.value || (m.stationRecord != null && String(stationIndex.value.get(m.stationRecord)?.faction?.id) === faction.value)))
  return result.sort((a, b) => {
    if (sort.value === 'commodity') return (descending.value ? -1 : 1) * label(a.identity).localeCompare(label(b.identity))
    const key = sort.value as 'buyingPrice' | 'sellingPrice' | 'tradePrice' | 'codexPrice' | 'stock'
    const av = a[key], bv = b[key]
    if (av == null) return bv == null ? 0 : 1
    if (bv == null) return -1
    return (av - bv) * (descending.value ? -1 : 1)
  })
})
// Compare the same serialized price field, source and commodity only; never invent a transaction direction.
const spreads = computed(() => {
  if (inventoryMode.value) return []
  const groups = new Map<string, { item: string; field: string; min: number; max: number; count: number }>()
  for (const m of rows.value) for (const field of ['buyingPrice', 'sellingPrice', 'tradePrice', 'codexPrice'] as const) {
    const value = m[field]; if (value == null || value < 0) continue
    const key = `${m.identity.namespace}:${m.identity.id}:${field}`
    const g = groups.get(key) ?? { item: label(m.identity), field, min: value, max: value, count: 0 }
    g.min = Math.min(g.min, value); g.max = Math.max(g.max, value); g.count++; groups.set(key, g)
  }
  return [...groups.values()].filter(g => g.count > 1).sort((a, b) => (b.max - b.min) - (a.max - a.min)).slice(0, 20)
})
function flags(m: Pick<Market, 'resourceStarved' | 'buyingOverstocked' | 'trading' | 'tradingOverstocked'>) {
  return [['IsResourceStarved', m.resourceStarved], ['IsBuyingOverstocked', m.buyingOverstocked], ['IsTrading', m.trading], ['IsTradingOverstocked', m.tradingOverstocked]].filter(([, v]) => v != null).map(([k, v]) => `${k}=${v}`).join(' · ') || 'Unknown'
}
watch(() => props.snapshot, s => { source.value = s.markets.some(m => m.source === 'Sector prices') ? 'Sector prices' : s.markets.some(m => m.source === 'Codex prices') ? 'Codex prices' : s.inventories[0]?.source ?? 'Sector prices' }, { immediate: true })
watch([query, source, sector, station, faction, sort, descending, () => props.snapshot], () => { page.value = 0 })
</script>
<template>
  <section class="space-y-4">
    <div class="rounded border border-amber-800 bg-amber-950/20 p-4 text-sm text-amber-200">Saved market observations. In the supported modern layout, sellingPrice is the player cost reference and buyingPrice is the proceeds reference. Stock sources remain separate. Live prices and availability require an in-game check.</div>
    <div class="flex flex-wrap gap-3">
      <input v-model="query" placeholder="Commodity, equipment or ID" aria-label="Search markets">
      <select v-model="source" aria-label="Observation source"><option>Sector prices</option><option>Codex prices</option><option>Station stock</option><option>Legacy station market</option><option>Station market modules</option></select>
      <select v-model="sector" aria-label="Market sector"><option value="">All sectors</option><option v-for="s in snapshot.sectors" :key="s.id" :value="String(s.id)">{{ sectorLabel(s) }}</option></select>
      <select v-model="station" aria-label="Market station"><option value="">All stations</option><option v-for="s in snapshot.stations" :key="s.evidence.graphRecord ?? s.id" :value="String(s.evidence.graphRecord)">Station #{{ s.id }} · Sector #{{ s.sectorId }}</option></select>
      <select v-model="faction" aria-label="Market faction"><option value="">All station factions</option><option v-for="f in factions" :key="f.id" :value="String(f.id)">{{ label(f) }}</option></select>
      <select v-model="sort" aria-label="Market sort"><option value="commodity">Name / ID</option><option value="buyingPrice">buyingPrice</option><option value="sellingPrice">sellingPrice</option><option value="tradePrice">tradePrice</option><option value="codexPrice">Codex price</option><option value="stock">Stock</option></select>
      <label class="flex items-center gap-2"><input v-model="descending" type="checkbox">Descending</label>
    </div>
    <div class="overflow-auto rounded border border-slate-800">
      <table class="w-full whitespace-nowrap text-sm"><thead><tr><th>Identity</th><th>Location</th><template v-if="inventoryMode"><th>Stock</th></template><template v-else><th>buyingPrice</th><th>sellingPrice</th><th>tradePrice</th><th>Codex price</th><th>Saved flags</th></template><th>Evidence</th></tr></thead>
        <tbody><tr v-for="(m, i) in rows.slice(page * 100, (page + 1) * 100)" :key="i"><td :title="m.identity.confidence + ' · ' + (m.identity.nameSource ?? 'No catalog source')">{{ label(m.identity) }}<span v-if="anomaly(m)" class="ml-2 text-amber-300" :title="anomaly(m)">⚠ Sentinel</span></td><td>{{ m.sectorId == null ? 'Unknown sector' : sectorIndex.get(m.sectorId)?.name || `Sector #${m.sectorId}` }}<span v-if="m.stationId != null"> · Station #{{ m.stationId }}</span></td><td v-if="inventoryMode">{{ number(m.stock) }}</td><template v-else><td>{{ number(m.buyingPrice) }}</td><td>{{ number(m.sellingPrice) }}</td><td>{{ number(m.tradePrice) }}</td><td>{{ number(m.codexPrice) }}</td><td>{{ flags(m) }}</td></template><td>{{ m.source }} · {{ m.evidence.confidence }} · v{{ snapshot.provenance.gameVersion ?? '?' }}</td></tr></tbody>
      </table>
      <p v-if="!rows.length" class="p-6 text-slate-400">No matching observations in this source and discovery view. Try another source or clear filters.</p>
    </div>
    <div class="flex items-center gap-3"><button :disabled="page === 0" @click="page--">Previous</button><button :disabled="(page + 1) * 100 >= rows.length" @click="page++">Next</button><span>{{ rows.length }} observations · Page {{ page + 1 }}</span></div>
    <details v-if="spreads.length" class="rounded border border-slate-700 p-4"><summary>Largest observed differences in this filtered source</summary><p class="my-3 text-sm text-slate-400">Same commodity and same field only. These differences are research leads, not profit estimates. Negative values are excluded.</p><div class="overflow-auto"><table class="w-full text-sm"><thead><tr><th>Commodity</th><th>Field</th><th>Minimum</th><th>Maximum</th><th>Difference</th><th>Observations</th></tr></thead><tbody><tr v-for="g in spreads" :key="g.item + g.field"><td>{{ g.item }}</td><td>{{ g.field }}</td><td>{{ number(g.min) }}</td><td>{{ number(g.max) }}</td><td>{{ number(g.max - g.min) }}</td><td>{{ g.count }}</td></tr></tbody></table></div></details>
  </section>
</template>
