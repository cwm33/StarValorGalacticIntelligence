<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { label, number, type Snapshot, type Identity } from './intelligence'
const props = defineProps<{ snapshot: Snapshot }>()
const recipeId = ref(''), recipeSource = ref('User-entered scenario (unverified)'), cycles = ref(1)
const inputs = ref<{ id: number | null; amount: number | null; available: number | null }[]>([])
const outputs = ref<{ id: number | null; amount: number | null }[]>([])
const station = ref(''), query = ref(''), scenario = ref(''), error = ref(''), busy = ref(false)
const result = ref<{ resources: { item: Identity; consumed: number; produced: number; available: number | null; shortfall: number | null }[]; warnings: string[] }>()
const stocks = computed(() => props.snapshot.inventories.filter(i => (!station.value || String(i.stationId) === station.value) && (!query.value || label(i.item).toLowerCase().includes(query.value.toLowerCase()))))
function emptyScenario() { return { profile: props.snapshot.provenance.mechanicsProfile, gameVersion: props.snapshot.provenance.gameVersion ?? 'Unknown', recipes: [], orders: [], available: [] } }
function initialize() { scenario.value = JSON.stringify(emptyScenario(), null, 2); result.value = undefined; error.value = ''; station.value = '' }
watch(() => props.snapshot, initialize, { immediate: true })
async function planForm() {
  if (!outputs.value.length) { error.value = 'Add at least one recipe output.'; return }
  const context = emptyScenario()
  const amount = (r: { id: number | null; amount: number | null }) => ({ item: { namespace: 'commodity', id: r.id }, amount: r.amount })
  scenario.value = JSON.stringify({ ...context, recipes: [{ id: recipeId.value, profile: context.profile, gameVersion: context.gameVersion, source: recipeSource.value,
    inputs: inputs.value.map(amount), outputs: outputs.value.map(amount) }], orders: [{ recipeId: recipeId.value, cycles: cycles.value }],
    available: inputs.value.filter(r => r.available != null).map(r => ({ item: { namespace: 'commodity', id: r.id }, amount: r.available })) }, null, 2)
  await plan()
}
async function plan() {
  busy.value = true; error.value = ''; result.value = undefined
  try {
    const body = JSON.stringify(JSON.parse(scenario.value))
    const response = await fetch('/api/industry/plan', { method: 'POST', body, headers: { 'Content-Type': 'application/json', 'X-Star-Valor': 'scenario' } })
    const data = await response.json(); if (!response.ok) throw new Error(data.error ?? 'Invalid scenario')
    result.value = data
  } catch (e) { error.value = String(e) }
  finally { busy.value = false }
}
</script>
<template>
  <section class="space-y-4">
    <p class="text-slate-300">Inspect observed station supplies and calculate aggregate resource balances from your own recipe catalog. Station ownership, production records and resource deposits have no validated extraction. No built-in recipe data is available.</p>
    <div class="flex flex-wrap gap-3"><select v-model="station" aria-label="Industry station"><option value="">All observed stations</option><option v-for="s in snapshot.stations" :key="s.id" :value="String(s.id)">Station #{{ s.id }} · Sector #{{ s.sectorId }}</option></select><input v-model="query" aria-label="Search industrial inventories" placeholder="Item name or ID"></div>
    <div class="max-h-80 overflow-auto"><table class="w-full text-sm"><thead><tr><th>Station</th><th>Item</th><th>Observed stock</th><th>Source</th></tr></thead><tbody><tr v-for="(i, n) in stocks" :key="n"><td>#{{ i.stationId }}</td><td>{{ label(i.item) }}</td><td>{{ number(i.stock) }}</td><td>{{ i.source }}</td></tr></tbody></table><p v-if="!stocks.length" class="p-5 text-slate-400">No matching inventory observations.</p></div>
    <form class="space-y-4 rounded border border-slate-700 p-4" @submit.prevent="planForm">
      <h3 class="text-lg font-semibold">Custom recipe resource calculator</h3>
      <p class="text-sm text-amber-200">Enter a recipe you have checked locally. Results remain a user scenario; the application cannot verify recipe mechanics. Quantities are per production cycle. Available resources are your own confirmed stock.</p>
      <div class="flex flex-wrap gap-3"><label class="grid gap-1 text-sm">Recipe label<input v-model="recipeId" required placeholder="Your recipe name"></label><label class="grid gap-1 text-sm">Evidence / source<input v-model="recipeSource" required></label><label class="grid gap-1 text-sm">Cycles<input v-model.number="cycles" type="number" min="0" max="1000000" step="1" required></label></div>
      <p class="text-sm text-slate-400">Profile: {{ snapshot.provenance.mechanicsProfile }} · Version: {{ snapshot.provenance.gameVersion ?? 'Unknown' }}</p>
      <div class="grid gap-4 md:grid-cols-2"><fieldset class="space-y-3"><legend class="font-semibold">Inputs</legend><div v-for="(r, i) in inputs" :key="i" class="flex flex-wrap gap-2"><label class="grid gap-1 text-xs">Commodity ID<input v-model.number="r.id" class="w-28" type="number" min="0" step="1" required></label><label class="grid gap-1 text-xs">Per cycle<input v-model.number="r.amount" class="w-28" type="number" min="0" step="any" required></label><label class="grid gap-1 text-xs">Available (optional)<input v-model.number="r.available" class="w-32" type="number" min="0" step="any" @change="r.available = ($event.target as HTMLInputElement).value === '' ? null : r.available"></label><button type="button" :aria-label="'Remove input ' + (i + 1)" @click="inputs.splice(i, 1)">Remove</button></div><button type="button" @click="inputs.push({ id: null, amount: null, available: null })">Add input</button></fieldset>
      <fieldset class="space-y-3"><legend class="font-semibold">Outputs</legend><div v-for="(r, i) in outputs" :key="i" class="flex flex-wrap gap-2"><label class="grid gap-1 text-xs">Commodity ID<input v-model.number="r.id" class="w-28" type="number" min="0" step="1" required></label><label class="grid gap-1 text-xs">Per cycle<input v-model.number="r.amount" class="w-28" type="number" min="0" step="any" required></label><button type="button" :aria-label="'Remove output ' + (i + 1)" @click="outputs.splice(i, 1)">Remove</button></div><button type="button" @click="outputs.push({ id: null, amount: null })">Add output</button></fieldset></div>
      <button :disabled="busy">Calculate custom recipe</button>
    </form>
    <details class="space-y-3 rounded border border-slate-700 p-4"><summary>Custom production scenario calculator</summary>
      <p class="text-sm text-amber-200">User-supplied scenarios are experimental. Vanilla, Base Building and Galaxy Reforged behavior must be validated separately. Catalog profile and version must match the scenario. This calculator never assumes market stock belongs to you.</p>
      <p class="text-sm text-slate-400">Add recipes with id, profile, gameVersion, source, inputs and outputs. Each amount has item: { namespace: "commodity", id: your ID } and amount. Orders contain recipeId and cycles. Available lists your manually confirmed resource amounts. Omitted amounts remain unknown.</p>
      <label class="grid gap-2 text-sm">Scenario JSON<textarea v-model="scenario" class="min-h-64 w-full rounded border border-slate-600 bg-slate-950 p-3 font-mono" spellcheck="false"></textarea></label>
      <button :disabled="busy" @click="plan">Calculate resource balance</button>
    </details>
      <p v-if="error" role="alert" class="text-red-300">{{ error }}</p>
      <template v-if="result"><p v-for="w in result.warnings" :key="w" class="text-sm text-amber-200">{{ w }}</p><div class="overflow-auto"><table class="w-full text-sm"><thead><tr><th>Resource</th><th>Consumed</th><th>Produced</th><th>Available</th><th>Aggregate shortfall</th></tr></thead><tbody><tr v-for="r in result.resources" :key="r.item.id"><td>{{ label(r.item) }}</td><td>{{ number(r.consumed) }}</td><td>{{ number(r.produced) }}</td><td>{{ number(r.available) }}</td><td>{{ number(r.shortfall) }}</td></tr></tbody></table><p v-if="!result.resources.length" class="p-4 text-slate-400">No production orders supplied. Add your validated recipes and requested cycles.</p></div></template>
  </section>
</template>
