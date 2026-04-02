## Dashboard Organization Pass Plan

Date: 2026-04-02

Goal: reorganize the dashboard into a more operator-first flow without changing data sources, endpoints, or telemetry scope.

Scope:
- Keep the hero focused on top-level health and readiness only.
- Group the main dashboard by operator tasks instead of transport slices.
- Move diagnostic tools and bridge-detail content lower in the page.
- Split frame activity out of the generic telemetry-pages card.
- Reduce duplicated bridge-state information between the hero and the main cards.

Target structure:
- Hero: overall bridge status and summary counters
- Primary telemetry: player overview, target overview
- Secondary telemetry: cast/resources, combat/follow
- Diagnostics: bridge details/tools, telemetry pages, frame activity

Validation:
- Build `DesktopDotNet/ChromaLink.HttpBridge/ChromaLink.HttpBridge.csproj`
- Sanity-check the updated HTML structure and client-side bindings

Keep/avoid:
- Keep existing data contracts and polling behavior unchanged.
- Avoid backend changes or new telemetry features during this pass.
