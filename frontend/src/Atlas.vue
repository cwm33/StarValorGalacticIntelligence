<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { label, number, sectorLabel, type Snapshot, type Sector } from './intelligence'
const props = defineProps<{ snapshot: Snapshot }>()
const query = ref(''), discovery = ref('all'), marketsOnly = ref(false)
const faction = ref(''), selected = ref<number | null>(null)
const destination = ref<number | null>(null)
const route = ref<{ path: number[] | null; jumps: number | null }>()
const routeError = ref(false)
watch([selected, destination, () => props.snapshot], async ([from, to, snapshot], _, cleanup) => {
  route.value = undefined
  routeError.value = false
  if (from == null || to == null) return
  const controller = new AbortController()
  cleanup(() => controller.abort())
  const base = '/api/' + snapshot.provenance.snapshotId + '/route'
  const query = new URLSearchParams({ from: String(from), to: String(to), spoilers: String(snapshot.provenance.includesSpoilers) })
  try {
    const response = await fetch(base + '?' + query, { signal: controller.signal })
    if (!response.ok) throw new Error('Route request failed')
    const result = await response.json()
    if (!controller.signal.aborted) route.value = result
  } catch {
    if (!controller.signal.aborted) routeError.value = true
  }
})
const zoom = ref(1), pan = ref({ x: 0, y: 0 })
let drag: { x: number; y: number; px: number; py: number; sector: number | null } | undefined
let moved = false
const stationsBySector = computed(() => {
  const index = new Map<number, Snapshot['stations']>()
  for (const s of props.snapshot.stations) { const list = index.get(s.sectorId) ?? []; list.push(s); index.set(s.sectorId, list) }
  return index
})
const marketSectors = computed(() => new Set(props.snapshot.markets.flatMap(m => m.sectorId == null ? [] : [m.sectorId])))
const factions = computed(() => [...new Map(props.snapshot.stations.filter(s => s.faction).map(s => [s.faction!.id, s.faction!])).values()])
const filtered = computed(() => props.snapshot.sectors.filter(s =>
  (!query.value || `${sectorLabel(s)} ${s.id}`.toLowerCase().includes(query.value.toLowerCase())) &&
  (discovery.value === 'all' || (discovery.value === 'discovered' ? s.discovered === true : discovery.value === 'spotted' ? s.discovered !== true && s.spotted === true : s.discovered !== true && s.spotted !== true)) &&
  (!marketsOnly.value || marketSectors.value.has(s.id)) &&
  (!faction.value || stationsBySector.value.get(s.id)?.some(st => String(st.faction?.id) === faction.value))))
const bounds = computed(() => {
  const located = props.snapshot.sectors.filter(s => s.x != null && s.y != null)
  let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity
  for (const s of located) { minX = Math.min(minX, s.x!); maxX = Math.max(maxX, s.x!); minY = Math.min(minY, s.y!); maxY = Math.max(maxY, s.y!) }
  return { minX, minY, width: Math.max(maxX - minX, 1), height: Math.max(maxY - minY, 1) }
})
function point(s: Sector) {
  const b = bounds.value, scale = Math.min(880 / b.width, 520 / b.height)
  return { x: 500 + (s.x! - b.minX - b.width / 2) * scale, y: 300 - (s.y! - b.minY - b.height / 2) * scale }
}
const plotted = computed(() => filtered.value.filter(s => s.x != null && s.y != null).map(s => ({ s, ...point(s) })))
const current = computed(() => props.snapshot.sectors.find(s => s.id === selected.value))
const sectorStations = computed(() => selected.value == null ? [] : stationsBySector.value.get(selected.value) ?? [])
const exits = computed(() => [...new Set(props.snapshot.connections.filter(c => c.from === selected.value).map(c => c.to))])
const stationStockCounts = computed(() => {
  const counts = new Map<number, number>()
  for (const row of props.snapshot.inventories) {
    if (row.stationRecord != null) counts.set(row.stationRecord, (counts.get(row.stationRecord) ?? 0) + 1)
  }
  return counts
})
function sectorName(id: number) {
  const sector = props.snapshot.sectors.find(s => s.id === id)
  return sector ? sectorLabel(sector) : 'Sector #' + id
}
const playerSector = computed(() => props.snapshot.player.currentSectorId)
const playerDistance = computed(() => {
  const origin = props.snapshot.sectors.find(s => s.id === playerSector.value)
  const target = current.value
  if (origin?.realX == null || origin.realY == null || target?.realX == null || target.realY == null) return null
  return Math.hypot(target.realX - origin.realX, target.realY - origin.realY)
})
const knownLinks = computed(() => {
  const sectors = new Map(filtered.value.map(s => [s.id, s]))
  const edges = new Map(props.snapshot.connections.map(c => [c.from + ':' + c.to, c]))
  return [...edges.values()].flatMap(c => {
    const reciprocal = edges.has(c.to + ':' + c.from)
    if (reciprocal && c.from > c.to) return []
    const a = sectors.get(c.from), b = sectors.get(c.to)
    return a?.x != null && a.y != null && b?.x != null && b.y != null ? [{ a: point(a), b: point(b), key: c.from + ":" + c.to, reciprocal, observed: c.confidence !== 'Verified', nearPlayer: c.from === playerSector.value || c.to === playerSector.value }] : []
  })
})
function reset() { zoom.value = 1; pan.value = { x: 0, y: 0 } }
function start(e: PointerEvent) { if (e.button !== 0) return; (e.currentTarget as SVGSVGElement).setPointerCapture(e.pointerId); moved = false; drag = { x: e.clientX, y: e.clientY, px: pan.value.x, py: pan.value.y, sector: (e.target as Element).closest('[data-sector]')?.getAttribute('data-sector') != null ? Number((e.target as Element).closest('[data-sector]')!.getAttribute('data-sector')) : null } }
function move(e: PointerEvent) {
  if (!drag) return
  const box = (e.currentTarget as SVGSVGElement).getBoundingClientRect()
  if (Math.abs(e.clientX - drag.x) + Math.abs(e.clientY - drag.y) > 4) moved = true
  pan.value = { x: drag.px + (e.clientX - drag.x) * 1000 / box.width, y: drag.py + (e.clientY - drag.y) * 600 / box.height }
}
function end() { if (!moved && drag?.sector != null) selected.value = drag.sector; drag = undefined }
watch(() => props.snapshot, () => { selected.value = null; destination.value = null; reset() })
</script>
<template>
  <section class="space-y-4">
    <div class="flex flex-wrap gap-3">
      <input v-model="query" aria-label="Search sectors" placeholder="Search sector name or ID">
      <select v-model="discovery" aria-label="Discovery filter"><option value="all">All visible sectors</option><option value="discovered">Discovered</option><option value="spotted">Spotted, undiscovered</option><option value="unknown">Discovery unconfirmed / false</option></select>
      <select v-model="faction" aria-label="Faction presence"><option value="">Any station faction</option><option v-for="f in factions" :key="f.id" :value="String(f.id)">{{ label(f) }}</option></select>
      <label class="flex items-center gap-2"><input v-model="marketsOnly" type="checkbox">Observed markets</label>
    </div>
    <p class="text-sm text-slate-400">{{ filtered.length }} sectors match · Solid: discovered · Ring: spotted · Dim: discovery unconfirmed / false. Factions describe station presence; sector ownership and danger are unknown.</p>
    <div class="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(16rem,1fr)]">
      <div class="rounded-xl border border-slate-700 bg-slate-900/50 p-3">
        <div class="flex gap-2"><button aria-label="Zoom in" @click="zoom = Math.min(12, zoom * 1.4)">+</button><button aria-label="Zoom out" @click="zoom = Math.max(.5, zoom / 1.4)">−</button><button @click="reset">Reset map</button><span class="self-center text-sm text-slate-400">Drag to pan · {{ zoom.toFixed(1) }}×</span></div>
        <svg class="mt-3 aspect-[5/3] w-full touch-none rounded bg-slate-950" viewBox="0 0 1000 600" role="img" aria-label="Galactic sector map" @wheel.prevent="zoom = Math.min(12, Math.max(.5, zoom * ($event.deltaY > 0 ? .9 : 1.1)))" @pointerdown="start" @pointermove="move" @pointerup="end" @pointercancel="drag = undefined">
          <defs><marker id="gate-direction" viewBox="0 0 10 10" refX="15" refY="5" markerWidth="8" markerHeight="8" orient="auto"><path d="M 0 0 L 10 5 L 0 10 z" fill="context-stroke" /></marker></defs>
          <g :transform="`translate(${pan.x} ${pan.y}) translate(500 300) scale(${zoom}) translate(-500 -300)`">
            <line v-for="c in knownLinks" data-gate-link :key="c.key" :x1="c.a.x" :y1="c.a.y" :x2="c.b.x" :y2="c.b.y" :stroke="c.nearPlayer ? '#5eead4' : '#a78bfa'" vector-effect="non-scaling-stroke" stroke-linecap="round" pointer-events="none" :stroke-dasharray="c.observed ? '6 4' : undefined" :marker-end="c.reciprocal ? undefined : 'url(#gate-direction)'" :stroke-width="c.nearPlayer ? 4 : 3" />
            <g v-for="p in plotted" :key="p.s.id" :data-sector="p.s.id" :data-player-sector="p.s.id === playerSector ? true : undefined">
              <title>{{ sectorLabel(p.s) }}{{ p.s.id === playerSector ? ' · Player location' : '' }} · Level {{ number(p.s.level) }} · {{ stationsBySector.get(p.s.id)?.length ?? 0 }} observed stations</title>
              <rect v-if="p.s.id === playerSector" :x="p.x - 10 / Math.sqrt(zoom)" :y="p.y - 10 / Math.sqrt(zoom)" :width="20 / Math.sqrt(zoom)" :height="20 / Math.sqrt(zoom)" :transform="`rotate(45 ${p.x} ${p.y})`" fill="none" stroke="#5eead4" stroke-width="2" vector-effect="non-scaling-stroke" pointer-events="none" />
              <circle :cx="p.x" :cy="p.y" :r="(selected === p.s.id ? 8 : 5) / Math.sqrt(zoom)" :fill="p.s.discovered === true ? (marketSectors.has(p.s.id) ? '#22d3ee' : '#94a3b8') : p.s.spotted === true ? 'transparent' : '#334155'" :stroke="selected === p.s.id ? '#fbbf24' : '#94a3b8'" :stroke-width="1.5 / zoom" class="cursor-pointer" />
            </g>
          </g>
        </svg>
        <p v-if="!plotted.length" class="p-4 text-slate-400">No matching sectors with known coordinates.</p>
        <ul class="mt-3 flex flex-wrap gap-x-4 gap-y-2 text-xs text-slate-300" aria-label="Map legend">
          <li v-if="knownLinks.length" class="flex items-center gap-2"><span class="w-6 border-t-[3px] border-violet-400"></span>{{ knownLinks.length }} jumpgate links</li>
          <li v-if="knownLinks.some(link => link.observed)" class="flex items-center gap-2"><span class="w-6 border-t-[3px] border-dashed border-violet-400"></span>Saved links, travel unverified</li>
          <li class="flex items-center gap-2"><span class="size-2.5 rotate-45 border-2 border-teal-300"></span>Player location</li>
          <li class="flex items-center gap-2"><span class="size-2 rounded-full bg-cyan-400"></span>Market</li>
        </ul>
        <p v-if="!knownLinks.length && plotted.length" class="mt-2 text-xs text-slate-400">No saved gate links connect the sectors shown.</p>
        <label class="mt-3 grid gap-1 text-sm">Select a sector (keyboard accessible)<select :value="selected ?? ''" @change="selected = ($event.target as HTMLSelectElement).value === '' ? null : Number(($event.target as HTMLSelectElement).value)"><option value="">Choose sector</option><option v-for="s in filtered" :key="s.id" :value="s.id">{{ sectorLabel(s) }} #{{ s.id }}</option></select></label>
      </div>
      <aside class="space-y-3 rounded-xl border border-slate-700 p-4">
        <template v-if="current">
          <h3 class="text-xl font-semibold">{{ sectorLabel(current) }}</h3><p v-if="current.id === playerSector" class="text-sm text-teal-300">Player location at save time</p>
          <p class="text-slate-400">Sector #{{ current.id }} · ({{ number(current.x) }}, {{ number(current.y) }})</p>
          <p v-if="playerDistance != null && current.id !== playerSector">Direct distance from saved sector · {{ playerDistance.toFixed(1) }} pc</p>
          <p>Level {{ number(current.level) }} · Population {{ number(current.population) }}</p>
          <p v-if="current.industry" class="text-sm text-slate-300">{{ current.industry.environment ?? 'Unknown environment' }} · Solar factor {{ number(current.industry.solarOutput) }} · Mineral factor {{ number(current.industry.mineralRichness) }} · Observed asteroid fields {{ number(current.industry.observedAsteroidFields) }} · Known deposit units {{ number(current.industry.resourceDeposits) }}</p>
          <p>Saved gate exits · {{ exits.length ? exits.map(sectorName).join(', ') : 'None visible' }}</p>
          <label class="grid gap-1 text-sm">Gate route destination<select v-model="destination"><option :value="null">Choose destination</option><option v-for="s in snapshot.sectors.filter(s => s.id !== current!.id)" :key="s.id" :value="s.id">{{ sectorLabel(s) }} #{{ s.id }}</option></select></label>
          <p v-if="route?.path" class="text-sm text-cyan-200">{{ route.jumps }} gate jumps · {{ route.path.map(sectorName).join(' → ') }}</p>
          <p v-else-if="route" class="text-sm text-slate-400">No path through visible generated gates.</p>
          <p v-if="routeError" role="alert">Gate route unavailable.</p>
          <h4 class="font-semibold">{{ sectorStations.length }} observed station records</h4>
          <p v-if="!sectorStations.length" class="text-slate-400">No station records in this view. Services remain unknown.</p>
          <div v-for="s in sectorStations" :key="s.evidence.graphRecord ?? s.id" class="rounded border border-slate-800 p-3 text-sm">
            <p>{{ s.kind ?? 'Unknown structure' }} #{{ s.id }} · {{ label(s.faction) }}<span v-if="s.operationState"> · {{ s.operationState }}</span><span v-if="s.playerOwned"> · Player-owned</span><span v-else-if="s.evidence.raw.ownerID === 1"> · Ownership unverified</span></p><p class="text-slate-400">Raw owner {{ s.evidence.raw.ownerID ?? 'Unknown' }} · Building state {{ s.evidence.raw.buildingState ?? 'Unknown' }} · State {{ s.evidence.raw.state ?? 'Unknown' }} · Model {{ number(s.modelId) }} · Plan {{ number(s.buildingPlanId) }} · Level {{ number(s.level) }}</p>
            <p>{{ stationStockCounts.get(s.evidence.graphRecord ?? -1) ?? 0 }} inventory observations</p>
          </div>
          <p class="text-xs text-slate-400">Source: {{ current.evidence.source }} · {{ current.evidence.confidence }} · {{ snapshot.provenance.gameVersion ?? 'Unknown version' }}</p>
        </template><p v-else class="text-slate-400">Select a sector to inspect its observed stations, markets and location.</p>
      </aside>
    </div>
  </section>
</template>
