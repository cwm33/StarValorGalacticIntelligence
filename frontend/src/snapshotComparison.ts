import type { Snapshot, Identity } from './intelligence'
export type Change = { category: string; identity: string; before: number | string | null; after: number | string | null; delta: number | null }
export type Comparison = { changes: Change[]; warnings: string[] }
const itemKey = (i: Identity) => `${i.namespace}:${i.id}`
export function compareSnapshots(before: Snapshot, after: Snapshot): Comparison {
  const changes: Change[] = [], warnings = [
    'Compare only saves from the same campaign. Newly observed records may reflect discovery, not construction or acquisition.',
    'Sector identity is a save-list index with unverified cross-save stability. Sector discovery and sector-price changes are not compared.',
    'Absent records and missing values are unknown, not zero or evidence of loss. Duplicate comparison keys are excluded.',
  ]
  if (before.provenance.includesSpoilers || after.provenance.includesSpoilers) return { changes: [], warnings: ['Disable spoilers to compare discovered snapshots.'] }
  if (before.provenance.gameVersion !== after.provenance.gameVersion || before.provenance.mechanicsProfile !== after.provenance.mechanicsProfile)
    warnings.push('Version or mechanics profile changed. Only matching source-specific records are compared; semantic compatibility is unverified.')
  if (before.player.name !== after.player.name) warnings.push('Player name differs. Campaign continuity must be checked before interpreting changes.')
  function add(category: string, identity: string, a: number | string | null, b: number | string | null) {
    if (a === b) return
    changes.push({ category, identity, before: a, after: b, delta: typeof a === 'number' && typeof b === 'number' ? b - a : null })
  }
  add('Player', 'Credits', before.player.credits, after.player.credits)
  add('Player', 'Ship model', before.player.shipModel ? itemKey(before.player.shipModel) : null, after.player.shipModel ? itemKey(after.player.shipModel) : null)
  const unique = <T>(rows: T[], key: (row: T) => string) => {
    const index = new Map<string, T>(), duplicate = new Set<string>()
    for (const r of rows) { const k = key(r); if (index.has(k)) duplicate.add(k); index.set(k, r) }
    for (const k of duplicate) index.delete(k)
    return index
  }
  const stationsBefore = unique(before.stations, s => String(s.id)), stationsAfter = unique(after.stations, s => String(s.id))
  for (const id of stationsAfter.keys()) if (!stationsBefore.has(id) && !before.stations.some(s => String(s.id) === id))
    add('Station observation', `station:${id}`, null, 'Observed')
  function matched<T>(category: string, a: T[], b: T[], key: (row: T) => string, value: (row: T) => number | string | null) {
    const left = unique(a, key), right = unique(b, key)
    for (const [id, row] of right) if (left.has(id)) add(category, id, value(left.get(id)!), value(row))
  }
  matched('Codex price', before.markets.filter(m => m.source === 'Codex prices' && m.stationId != null), after.markets.filter(m => m.source === 'Codex prices' && m.stationId != null),
    m => `${m.source}:station:${m.stationId}:${itemKey(m.commodity)}`, m => m.codexPrice)
  matched('Inventory stock', before.inventories, after.inventories, i => `${i.source}:station:${i.stationId}:${itemKey(i.item)}:rarity:${i.rarity ?? 'unknown'}`, i => i.stock)
  matched('Faction relationship', before.relations, after.relations, r => `${r.from}:${r.to}`, r => r.value)
  // Compare weapon slots only when explicitly identifiable. Equipment quantities are aggregated only if every quantity is known.
  matched('Installed weapon slot', before.installedItems.filter(i => i.kind === 'weapon' && i.slot != null), after.installedItems.filter(i => i.kind === 'weapon' && i.slot != null), i => `slot:${i.slot}`, i => itemKey(i.item))
  const equipmentTotals = (s: Snapshot) => {
    const groups = new Map<string, { key: string; total: number | null }>()
    for (const i of s.installedItems.filter(i => i.kind === 'equipment')) {
      const key = `${itemKey(i.item)}:rarity:${i.rarity ?? 'unknown'}`, current = groups.get(key)
      const amount = i.quantity != null && i.quantity >= 0 ? i.quantity : null
      groups.set(key, { key, total: current ? current.total == null || amount == null ? null : current.total + amount : amount })
    }
    return [...groups.values()]
  }
  matched('Installed equipment quantity', equipmentTotals(before), equipmentTotals(after), i => i.key, i => i.total)
  return { changes, warnings }
}
