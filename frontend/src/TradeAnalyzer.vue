<script setup lang="ts">
import { ref, watch } from 'vue'
import { label, number, type Identity, type Snapshot } from './intelligence'
const props = defineProps<{ snapshot: Snapshot }>()
type Opportunity = { commodity: Identity; buyAt: string; sellAt: string; costPerItem: number; proceedsPerItem: number; differencePerItem: number; quantity: number | null; grossProfit: number | null; estimatedNetProfit: number | null; profitPerCargoUnit: number | null; gateJumps: number | null; executable: boolean; warnings: string[] }
const experimental = ref(false), rank = ref('difference'), result = ref<{ interpretation: string; opportunities: Opportunity[]; note: string }>(), error = ref(''), busy = ref(false)
watch([experimental, rank, () => props.snapshot], async (_, __, cleanup) => {
  const abort = new AbortController(); cleanup(() => abort.abort()); result.value = undefined; error.value = ''; busy.value = true
  try {
    const response = await fetch(`/api/${props.snapshot.provenance.snapshotId}/trade?spoilers=${props.snapshot.provenance.includesSpoilers}&experimental=${experimental.value}&rankBy=${rank.value}`, { signal: abort.signal })
    if (!response.ok) throw new Error('Analysis unavailable. Import the save again.')
    result.value = await response.json()
  } catch (e) { if (!abort.signal.aborted) error.value = String(e) }
  finally { if (!abort.signal.aborted) busy.value = false }
}, { immediate: true })
</script>
<template>
  <section class="space-y-4">
    <p class="text-slate-300">Verified transaction analysis becomes available when locally validated price, stock, cargo and access evidence is supplied. No current save layout provides all of that evidence.</p>
    <label class="flex items-center gap-3 rounded border border-amber-700 p-4"><input v-model="experimental" type="checkbox">Enable experimental interpretation: compare saved sector price references</label>
    <p v-if="experimental" class="text-sm text-amber-200">In the supported modern layout, sellingPrice is the player cost reference and buyingPrice is the proceeds reference. Check live prices, stock, demand and access before trading. Gate jumps count links in the visible saved topology.</p>
    <label class="flex items-center gap-3">Rank by<select v-model="rank"><option value="difference">Difference per item</option><option value="gross">Gross profit (when known)</option><option value="net">Net profit (when known)</option><option value="cargo">Profit per cargo unit (when known)</option></select></label>
    <p v-if="busy" role="status">Analyzing…</p><p v-if="error" role="alert" class="text-red-300">{{ error }}</p>
    <template v-if="result"><p class="text-sm text-slate-400">{{ result.note }}</p>
      <p v-if="!result.opportunities.length" class="rounded border border-slate-700 p-6 text-slate-400">{{ experimental ? 'No positive experimental spreads with paired sector price fields in this view.' : 'No verified transaction quotes. Use Market Intelligence to inspect observed price differences, or explicitly enable the experimental interpretation.' }}</p>
      <div v-else class="overflow-auto"><table class="w-full whitespace-nowrap text-sm"><thead><tr><th>Commodity</th><th>Hypothetical origin → destination</th><th>Cost → proceeds</th><th>Difference / item</th><th>Gate jumps</th><th>Quantity</th><th>Gross / net</th><th>Status and constraints</th></tr></thead><tbody><tr v-for="(r, i) in result.opportunities" :key="i"><td>{{ label(r.commodity) }}</td><td>{{ r.buyAt }} → {{ r.sellAt }}</td><td>{{ number(r.costPerItem) }} → {{ number(r.proceedsPerItem) }}</td><td>{{ number(r.differencePerItem) }}</td><td>{{ number(r.gateJumps) }}</td><td>{{ number(r.quantity) }}</td><td>{{ number(r.grossProfit) }} / {{ number(r.estimatedNetProfit) }}</td><td class="max-w-80 whitespace-normal"><details><summary class="text-amber-200">Experimental · Not executable</summary><ul class="mt-2 space-y-1"><li v-for="w in r.warnings" :key="w">{{ w }}</li></ul></details></td></tr></tbody></table><p class="mt-3 text-sm text-slate-400">Top {{ result.opportunities.length }} results, limited to 100.</p></div>
    </template>
  </section>
</template>
