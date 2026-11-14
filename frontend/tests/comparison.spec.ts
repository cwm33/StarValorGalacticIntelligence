import { test, expect } from '@playwright/test'
import { compareSnapshots } from '../src/snapshotComparison'
import type { Snapshot } from '../src/intelligence'
function snapshot(): Snapshot {
  return { provenance: { snapshotId: 'synthetic', importedAt: '2026-01-01', gameVersion: '2.251', mechanicsProfile: 'Galaxy Reforged', includesSpoilers: false, baseBuilding: true, sectorIdentity: 'list index' }, player: { name: 'Synthetic', credits: 100, level: 1, shipModel: null }, sectors: [], stations: [], markets: [], inventories: [], ships: [], installedItems: [], relations: [], connections: [], warnings: [] }
}
test('comparison preserves unknown and version warnings', () => {
  const before = snapshot(), after = snapshot(); after.player.credits = null; after.provenance.gameVersion = 'future'
  const result = compareSnapshots(before, after)
  expect(result.changes[0]).toMatchObject({ before: 100, after: null, delta: null })
  expect(result.warnings.some(w => w.includes('Version'))).toBeTruthy()
})
test('comparison uses station and namespaced item IDs, never graph record IDs', () => {
  const before = snapshot(), after = snapshot()
  const stock = (ns: string, amount: number, graphRecord: number) => ({ item: { namespace: ns, id: 18, name: null, confidence: 'Unknown', nameSource: null }, stationId: 101, source: 'Station stock', stock: amount, lastStock: null, rarity: 0, evidence: { source: 'Station stock', graphRecord, raw: {}, confidence: 'Extracted', note: null } })
  before.inventories = [stock('commodity', 4, 1), stock('weapon', 7, 2)]
  after.inventories = [stock('commodity', 9, 500), stock('weapon', 7, 3)]
  const result = compareSnapshots(before, after)
  expect(result.changes).toHaveLength(1)
  expect(result.changes[0]).toMatchObject({ delta: 5, identity: 'Station stock:station:101:commodity:18:rarity:0' })
  after.inventories.push(stock('commodity', 12, 99))
  expect(compareSnapshots(before, after).changes).toHaveLength(0)
})
test('comparison cannot expose spoiler snapshots', () => {
  const before = snapshot(), after = snapshot(); after.provenance.includesSpoilers = true; after.player.credits = 500
  expect(compareSnapshots(before, after).changes).toHaveLength(0)
})
