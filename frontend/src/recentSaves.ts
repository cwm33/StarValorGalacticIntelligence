export type RecentSave = {
  key: string
  save: File
  info?: File
  mechanics: string
  player?: string
  openedAt: number
}

const storeName = 'saves'
const limit = 5

async function withStore<T>(mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await new Promise<IDBDatabase>((resolve, reject) => {
    const request = indexedDB.open('star-valor-recent-saves', 1)
    request.onupgradeneeded = () => request.result.createObjectStore(storeName, { keyPath: 'key' })
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
  try {
    return await new Promise<T>((resolve, reject) => {
      const transaction = db.transaction(storeName, mode)
      const request = action(transaction.objectStore(storeName))
      transaction.oncomplete = () => resolve(request.result)
      transaction.onabort = () => reject(transaction.error)
    })
  } finally {
    db.close()
  }
}

export async function listRecentSaves() {
  const entries: RecentSave[] = await withStore('readonly', store => store.getAll())
  return entries.sort((a, b) => b.openedAt - a.openedAt)
}

export function rememberSave(entry: RecentSave) {
  return withStore('readwrite', store => {
    const put = store.put(entry)
    const read = store.getAll()
    read.onsuccess = () => {
      const entries: RecentSave[] = read.result
      entries.sort((a, b) => b.openedAt - a.openedAt)
      for (const old of entries.slice(limit)) store.delete(old.key)
    }
    return put
  })
}

export function forgetSave(key: string) {
  return withStore('readwrite', store => store.delete(key))
}

export function clearRecentSaves() {
  return withStore('readwrite', store => store.clear())
}
