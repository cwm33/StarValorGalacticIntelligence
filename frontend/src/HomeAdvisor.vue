<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { number, type Snapshot } from './intelligence'
const props = defineProps<{ snapshot: Snapshot }>()
type Candidate = { sectorId: number; name: string | null; score: number | null; evidenceCoverage: number; reasons: { criterion: string; weight: number; score: number | null; reason: string }[] }
const weights = ref({ stations: 1, markets: 1, equipment: 1, ships: 1, connectivity: 1 }), candidates = ref<Candidate[]>([]), selected = ref<number[]>([]), query = ref(''), error = ref(''), busy = ref(false)
watch([weights, () => props.snapshot], async (_, __, cleanup) => {
  const abort = new AbortController(); cleanup(() => abort.abort()); error.value = ''; busy.value = true
  try {
    const params = new URLSearchParams({ spoilers: String(props.snapshot.provenance.includesSpoilers), ...Object.fromEntries(Object.entries(weights.value).map(([k, v]) => [k, String(v)])) })
    const response = await fetch(`/api/${props.snapshot.provenance.snapshotId}/advisor?${params}`, { signal: abort.signal })
    if (!response.ok) throw new Error('Advisor unavailable. Import the save again.')
    candidates.value = await response.json()
  } catch (e) { if (!abort.signal.aborted) error.value = String(e) }
  finally { if (!abort.signal.aborted) busy.value = false }
}, { immediate: true, deep: true })
watch(() => props.snapshot, () => { selected.value = []; candidates.value = [] })
const filtered = computed(() => candidates.value.filter(c => !query.value || `${c.name} ${c.sectorId}`.toLowerCase().includes(query.value.toLowerCase())))
const compared = computed(() => candidates.value.filter(c => selected.value.includes(c.sectorId)))
</script>
<template>
  <section class="space-y-4">
    <p class="text-slate-300">Rank observed commercial presence for headquarters research. Scores reflect your preferences and available evidence, not a verified optimal base location. Sector level is not treated as technology. Missing criteria are excluded, and coverage shows the weight of criteria with evidence.</p>
    <div class="grid gap-3 sm:grid-cols-5"><label v-for="(_, k) in weights" :key="k" class="grid gap-2 rounded border border-slate-700 p-3 text-sm">{{ k }} · {{ weights[k] }}<input v-model.number="weights[k]" type="range" min="0" max="10" step="1" :aria-label="k + ' scoring weight'"></label></div>
    <p v-if="error" role="alert" class="text-red-300">{{ error }}</p><p v-if="busy" role="status">Updating scores…</p>
    <div v-if="compared.length" class="grid gap-3 md:grid-cols-3"><article v-for="c in compared" :key="c.sectorId" class="space-y-3 rounded border border-cyan-800 p-4"><h3 class="font-semibold">{{ c.name ?? `Sector #${c.sectorId}` }}</h3><p>{{ number(c.score) }} / 100 · {{ number(c.evidenceCoverage * 100) }}% coverage</p><div v-for="r in c.reasons" :key="r.criterion" class="text-sm"><p>{{ r.criterion }}: {{ number(r.score) }} <span v-if="r.weight">(weight {{ r.weight }})</span></p><p class="text-xs text-slate-400">{{ r.reason }}</p></div></article></div>
    <input v-model="query" placeholder="Filter candidate sectors" aria-label="Filter headquarters candidates">
    <div class="max-h-[38rem] overflow-auto"><table class="w-full text-sm"><thead><tr><th>Compare up to 3</th><th>Sector</th><th>Evidence score</th><th>Coverage</th><th>Reasons</th></tr></thead><tbody><tr v-for="c in filtered" :key="c.sectorId"><td><input v-model="selected" type="checkbox" :value="c.sectorId" :disabled="!selected.includes(c.sectorId) && selected.length >= 3" :aria-label="'Compare sector ' + c.sectorId"></td><td>{{ c.name ?? 'Unnamed' }} #{{ c.sectorId }}</td><td>{{ number(c.score) }}</td><td>{{ number(c.evidenceCoverage * 100) }}%</td><td><details><summary>Score breakdown</summary><ul class="mt-2 space-y-2"><li v-for="r in c.reasons" :key="r.criterion">{{ r.criterion }}: {{ number(r.score) }} · {{ r.reason }}</li></ul></details></td></tr></tbody></table><p v-if="!filtered.length" class="p-5 text-slate-400">No matching sectors.</p></div>
  </section>
</template>
