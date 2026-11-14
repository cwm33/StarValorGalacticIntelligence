# Project guidance

- `src/Decoder` contains the standalone .NET 10 NRBF graph reader and evidence-based projections. Keep it independent of ASP.NET Core and the browser.
- `src/Api` exposes the loopback-only import, view, schema, and export endpoints. Keep the application local and server imports transient. Recent saves are bounded browser-local copies managed by the user.
- `frontend` contains Vue 3, TypeScript, Vite, and Tailwind. The production build goes to `src/Api/wwwroot`.
- `tests/Regression` runs synthetic checks and accepts private main/Info pairs by path. Keep real saves and exports outside version control.

## Read-only behavior

Open game saves for reading only. Never modify, rename, replace, or write back any game save or companion Info file. Use System.Formats.Nrbf and explicit record projections. Never invoke BinaryFormatter deserialization or load types named by a payload. Preserve references and unknown serialized fields in the raw graph. Maintain the spoiler gate on raw inspection and full exports.

## Coding and validation

Keep mappings close to their serialized fields and document the evidence for semantic interpretations. Preserve original numeric IDs, distinguish graph IDs, and record uncertainty. Treat Vanilla and Galaxy Reforged profile labels as provenance unless save-level evidence establishes a stronger claim. Verify both documented market layouts before changing extraction. Run `dotnet build`, the synthetic and applicable private fixture regressions, and `npm --prefix frontend run build`. Exercise any changed API endpoint with a local fixture. Trade-route optimization requires validated decoder and extraction behavior and is outside this milestone.

Keep game data bindings in the API host and the decoder independent of the resolver. Apply enrichment and per-save diagnostics after discovery filtering. Use path-free source labels in API responses. Keep localization and override files outside Git and read them without write access. See INTEGRATION.md for the distinct catalog namespaces. Run the resolver tests in addition to the existing build and decoder checks.
