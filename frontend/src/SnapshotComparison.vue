<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { compareSnapshots } from './snapshotComparison'
import { type Snapshot, number } from './intelligence'
const props = defineProps<{ snapshot: Snapshot; baseline?: Snapshot }>()
defineEmits<{ pin: []; clear: [] }>()
const sameCampaign = ref(false)
watch(() => props.snapshot, () => { sameCampaign.value = false })
const result = computed(() => props.baseline && sameCampaign.value ? compareSnapshots(props.baseline, props.snapshot) : undefined)
function exportResult() {
  if (!result.value) return
  const url = URL.createObjectURL(new Blob([JSON.stringify(result.value, null, 2)], { type: 'application/json' }))
  const a = document.createElement('a'); a.href = url; a.download = 'galactic-snapshot-comparison.json'; a.click(); URL.revokeObjectURL(url)
}
</script>
<template>
  <section class="space-y-4">
    <p class="text-slate-300">Pin a discovered snapshot, then import a later save from the same campaign. The baseline lives only in this tab's memory and disappears on reload. No additional save database is created.</p>
    <div class="flex flex-wrap gap-3"><button :disabled="snapshot.provenance.includesSpoilers" @click="$emit('pin')">Pin current snapshot as baseline</button><button v-if="baseline" @click="$emit('clear')">Clear baseline</button></div>
    <p v-if="snapshot.provenance.includesSpoilers" class="text-amber-200">Disable spoilers to pin or compare discovered snapshots.</p>
    <template v-if="baseline"><p class="text-sm text-slate-400">Baseline: {{ baseline.player.name ?? 'Unknown player' }} · {{ baseline.provenance.gameVersion ?? 'Unknown version' }} · imported {{ new Date(baseline.provenance.importedAt).toLocaleString() }}</p>
      <label class="flex items-center gap-3"><input v-model="sameCampaign" type="checkbox" :disabled="snapshot.provenance.includesSpoilers">These snapshots belong to the same campaign</label>
      <template v-if="result && !snapshot.provenance.includesSpoilers"><p v-for="w in result.warnings" :key="w" class="text-sm text-amber-200">{{ w }}</p><button @click="exportResult">Export comparison JSON</button><div class="overflow-auto"><table class="w-full text-sm"><thead><tr><th>Category</th><th>Stable identity / source</th><th>Before</th><th>After</th><th>Delta</th></tr></thead><tbody><tr v-for="(c, i) in result.changes" :key="i"><td>{{ c.category }}</td><td>{{ c.identity }}</td><td>{{ typeof c.before === 'number' ? number(c.before) : c.before ?? 'Unknown' }}</td><td>{{ typeof c.after === 'number' ? number(c.after) : c.after ?? 'Unknown' }}</td><td>{{ number(c.delta) }}</td></tr></tbody></table><p v-if="!result.changes.length" class="p-5 text-slate-400">No changes in comparable, uniquely identified observations. Unsupported and missing fields remain unknown.</p></div></template>
    </template><p v-else class="rounded border border-slate-700 p-5 text-slate-400">No baseline pinned yet.</p>
  </section>
</template>
