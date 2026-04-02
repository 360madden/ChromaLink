## Dashboard Layout Polish Plan

Date: 2026-04-02

Goal: address obvious dashboard UI issues without changing product scope or data contracts.

Scope:
- Let key/value rows wrap cleanly instead of truncating long values.
- Make frame activity rows tolerate longer frame names without awkward clipping.
- Rework the footer summary into wrapped status items instead of one long sentence.
- Remove the remaining inline spacing style in the generic pages section.

Validation:
- Reload the dashboard page and inspect the updated markup/CSS paths.
- Build the HTTP bridge project to catch any static asset or packaging regressions.

Keep/avoid:
- Keep the same telemetry cards and backend endpoints.
- Avoid new features or dashboard data changes during this pass.
