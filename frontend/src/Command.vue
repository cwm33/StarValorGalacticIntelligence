<script setup lang="ts">
import { computed, ref } from 'vue'
import Arsenal from './Arsenal.vue'
import TradeAnalyzer from './TradeAnalyzer.vue'
import HomeAdvisor from './HomeAdvisor.vue'
import Industry from './Industry.vue'
import SnapshotComparison from './SnapshotComparison.vue'
import Atlas from './Atlas.vue'
import MarketIntelligence from './MarketIntelligence.vue'
import { label, number, type Snapshot } from './intelligence'
const props = defineProps<{ snapshot: Snapshot; baseline?: Snapshot }>()
defineEmits<{ pin: []; clear: [] }>()
const tab = ref('Overview')
const metrics = computed(() => [
  ['Visible sectors', props.snapshot.sectors.length], ['Observed stations', props.snapshot.stations.length],
  ['Price observations', props.snapshot.markets.length], ['Inventory observations', props.snapshot.inventories.length]
])
</script>
<template>
  <section class="space-y-5">
    <nav class="flex flex-wrap gap-2" aria-label="Intelligence views"><button v-for="t in ['Overview', 'Galactic Atlas', 'Market Intelligence', 'Trade Analyzer', 'Home Sector Advisor', 'Fleet Command', 'Arsenal & Inventory', 'Industry', 'Snapshot Comparison']" :key="t" :aria-pressed="tab === t" :class="tab === t ? 'border-cyan-400 text-cyan-200' : ''" @click="tab = t">{{ t }}</button></nav>
    <div class="flex flex-wrap items-center justify-between gap-3"><h2 class="text-2xl font-semibold">{{ tab }}</h2><p class="text-sm text-slate-400">{{ snapshot.provenance.mechanicsProfile }} · v{{ snapshot.provenance.gameVersion ?? '?' }} · {{ snapshot.provenance.includesSpoilers ? 'Spoilers enabled' : 'Discovered records only' }}</p></div>
    <template v-if="tab === 'Overview'">
      <p class="text-slate-300">{{ snapshot.player.name ?? 'Commander' }} · {{ number(snapshot.player.credits) }} credits · {{ label(snapshot.player.shipModel) }}</p>
      <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><div v-for="m in metrics" :key="String(m[0])" class="rounded-xl border border-slate-700 bg-slate-900/50 p-5"><p class="text-sm text-slate-400">{{ m[0] }}</p><p class="mt-2 text-3xl text-cyan-200">{{ m[1] }}</p></div></div>
      <div class="grid gap-4 md:grid-cols-2"><button class="p-5 text-left" @click="tab = 'Galactic Atlas'"><strong>Plan your next exploration</strong><p class="mt-2 text-sm text-slate-400">Locate known sectors, inspect station presence and find observed markets.</p></button><button class="p-5 text-left" @click="tab = 'Market Intelligence'"><strong>Research market differences</strong><p class="mt-2 text-sm text-slate-400">Compare saved price fields while keeping inventory and codex observations separate.</p></button></div>
      <details class="rounded border border-slate-700 p-4"><summary>Data confidence and missing evidence</summary><ul class="mt-3 list-disc space-y-2 pl-5 text-sm text-amber-200"><li v-for="w in snapshot.warnings" :key="w">{{ w }}</li></ul><p class="mt-3 text-sm text-slate-400">{{ snapshot.provenance.sectorIdentity }}. Import time is not observation time.</p></details>
    </template>
    <Arsenal v-if="tab === 'Arsenal & Inventory'" :snapshot="snapshot" />
    <Atlas v-if="tab === 'Galactic Atlas'" :snapshot="snapshot" />
    <MarketIntelligence v-if="tab === 'Market Intelligence'" :snapshot="snapshot" />
    <SnapshotComparison v-if="tab === 'Snapshot Comparison'" :snapshot="snapshot" :baseline="baseline" @pin="$emit('pin')" @clear="$emit('clear')" />
    <Industry v-if="tab === 'Industry'" :snapshot="snapshot" />
    <TradeAnalyzer v-if="tab === 'Trade Analyzer'" :snapshot="snapshot" />
    <HomeAdvisor v-if="tab === 'Home Sector Advisor'" :snapshot="snapshot" />
    <section v-if="tab === 'Fleet Command'" class="space-y-4">
      <div class="rounded border border-slate-700 p-5"><h3 class="text-xl">Current ship · {{ label(snapshot.player.shipModel) }}</h3><p class="mt-2 text-sm text-slate-400">{{ snapshot.installedItems.filter(i => i.kind === 'weapon').length }} installed weapon records · {{ snapshot.installedItems.filter(i => i.kind === 'equipment').length }} equipment records. Slot capacity, cargo volume, crew roster, drones, DPS and power budget are unknown.</p></div>
      <div class="overflow-auto"><table class="w-full text-sm"><thead><tr><th>Installed item</th><th>Kind</th><th>Slot</th><th>Saved quantity</th><th>Rarity</th><th>Name confidence</th></tr></thead><tbody><tr v-for="(i, n) in snapshot.installedItems" :key="n"><td>{{ label(i.item) }}</td><td>{{ i.kind }}</td><td>{{ number(i.slot) }}</td><td>{{ number(i.quantity) }}</td><td>{{ number(i.rarity) }}</td><td>{{ i.item.confidence }} · {{ i.item.nameSource ?? 'Unknown' }}</td></tr></tbody></table><p v-if="!snapshot.installedItems.length" class="p-5 text-slate-400">No installed loadout records could be extracted.</p></div>
      <details v-if="snapshot.cargo?.length"><summary>Player cargo and storage ({{ snapshot.cargo.length }} saved records)</summary>
        <div class="max-h-80 overflow-auto"><table class="w-full text-sm"><thead><tr><th>Item</th><th>Quantity</th><th>Rarity</th><th>Storage</th></tr></thead><tbody><tr v-for="(item, index) in snapshot.cargo" :key="index"><td>{{ label(item.item) }}</td><td>{{ number(item.quantity) }}</td><td>{{ number(item.rarity) }}</td><td>{{ item.storage }}</td></tr></tbody></table></div>
      </details>
      <p>Saved Fleet Commander skill {{ number(snapshot.player.fleetCommanderBase) }} · Total fleet capacity unknown</p>
      <details v-if="snapshot.ships.some(ship => ship.role === 'Player fleet')"><summary>Player fleet roster</summary>
        <ul class="list-disc pl-5"><li v-for="ship in snapshot.ships.filter(ship => ship.role === 'Player fleet')" :key="String(ship.evidence.raw.memberRecord)">{{ label(ship.model) }} · Captain ID {{ ship.evidence.raw.crewMemberID ?? 'Unknown' }}</li></ul>
      </details>
      <details><summary>Directly extracted player ship values</summary><pre class="mt-3 text-sm">{{ JSON.stringify(snapshot.ships.filter(s => s.role === 'Player ship').map(s => s.evidence.raw), null, 2) }}</pre></details>
      <p class="text-sm text-slate-400">{{ snapshot.ships.filter(s => s.role === 'Sector ships').length }} observed sector ships are NPC observations; the player roster comes from the saved player character.</p>
    </section>
  </section>
</template>
