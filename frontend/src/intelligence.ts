export type Identity = { namespace: string; id: number; name: string | null; confidence: string; nameSource: string | null }
export type Evidence = { source: string; graphRecord: number | null; raw: Record<string, unknown>; confidence: string; note: string | null }
export type Sector = { id: number; name: string | null; x: number | null; y: number | null; level: number | null; discovered: boolean | null; spotted: boolean | null; population: number | null; evidence: Evidence }
export type Station = { id: number; sectorId: number; faction: Identity | null; modelId: number | null; buildingPlanId: number | null; discovered: boolean | null; level: number | null; evidence: Evidence }
export type Market = { commodity: Identity; source: string; sectorId: number | null; stationId: number | null; buyingPrice: number | null; sellingPrice: number | null; tradePrice: number | null; codexPrice: number | null; buyingOverstocked: boolean | null; resourceStarved: boolean | null; trading: boolean | null; tradingOverstocked: boolean | null; evidence: Evidence; priceSemantics: string }
export type Inventory = { item: Identity; stationId: number; source: string; stock: number | null; lastStock: number | null; rarity: number | null; evidence: Evidence }
export type Snapshot = {
  provenance: { snapshotId: string; importedAt: string; gameVersion: string | null; mechanicsProfile: string; baseBuilding: boolean | null; includesSpoilers: boolean; sectorIdentity: string }
  player: { name: string | null; credits: number | null; level: number | null; shipModel: Identity | null }
  sectors: Sector[]; stations: Station[]; markets: Market[]; inventories: Inventory[]
  ships: { model: Identity | null; role: string; sectorId: number | null; faction: Identity | null; evidence: Evidence }[]
  installedItems: { item: Identity; kind: string; slot: number | null; quantity: number | null; rarity: number | null; evidence: Evidence }[]
  relations: { from: number; to: number; value: number | null; evidence: Evidence }[]
  connections: { from: number; to: number; confidence: string }[]; warnings: string[]
}
export const number = (n: number | null | undefined) => n == null ? 'Unknown' : n.toLocaleString(undefined, { maximumFractionDigits: 2 })
export const label = (i: Identity | null) => i ? `${i.name ?? i.namespace} #${i.id}${i.confidence === 'BaseGameReference' ? ' (base-game reference)' : ''}` : 'Unknown'
export const sectorLabel = (s: Sector) => s.name || `Sector #${s.id}`
