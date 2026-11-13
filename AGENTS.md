# Project guidance

- `src/Decoder` contains the standalone .NET 10 NRBF graph reader and evidence-based projections. Keep it independent of ASP.NET Core and the browser.
- `src/Api` exposes the loopback-only import, view, schema, and export endpoints. Keep the application local and imports transient.
- `frontend` contains Vue 3, TypeScript, Vite, and Tailwind. The production build goes to `src/Api/wwwroot`.
- `tests/Regression` runs synthetic checks and accepts private main/Info pairs by path. Keep real saves and exports outside version control.

## Read-only behavior

Open game saves for reading only. Never modify, rename, replace, or write back any game save or companion Info file. Use System.Formats.Nrbf and explicit record projections. Never invoke BinaryFormatter deserialization or load types named by a payload. Preserve references and unknown serialized fields in the raw graph. Maintain the spoiler gate on raw inspection and full exports.

## Coding and validation

Keep mappings close to their serialized fields and document the evidence for semantic interpretations. Preserve original numeric IDs, distinguish graph IDs, and record uncertainty. Treat Vanilla and Galaxy Reforged profile labels as provenance unless save-level evidence establishes a stronger claim. Verify both documented market layouts before changing extraction. Run `dotnet build`, the synthetic and applicable private fixture regressions, and `npm --prefix frontend run build`. Exercise any changed API endpoint with a local fixture. Trade-route optimization requires validated decoder and extraction behavior and is outside this milestone.
