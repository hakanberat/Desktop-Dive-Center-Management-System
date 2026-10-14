# Verification record

Verified on Windows on 13 September 2026.

| Check | Result |
|---|---|
| Phase builds and final Release build | Passed; zero warnings and errors |
| Real EF Core / SQLite acceptance checks | 123 passed |
| Separate-process restart checks | 5 passed |
| WPF binding diagnostics | No errors |
| Screen rendering | All 15 navigation destinations and 16 entity editors |
| Decimal culture checks | en-US, tr-TR and ro-RO passed |
| Code First migration snapshot | Matches the current model |
| Existing-schema upgrade | Prior history retained; automatic backup verified |
| Online database backup | Opened independently with committed WAL records |
| Optional sample data | Isolated, idempotent; real business services |

The acceptance flow creates a customer, a group reservation with a temporary participant, an operation, a shared dive log and individual diver records. It verifies history, reservation payments, expenses, independent currency balances, combined filters, dashboards and every report type. Invalid measurements, missing required data, category mismatches, invalid transitions, over-capacity boats, duplicate participants, unconfirmed transaction deletion and historical foreign-key deletion are rejected.

The final evidence is in `artifacts/acceptance-release-final/`: `acceptance-results.json`, `persistence-results.json`, `binding-diagnostics.txt`, the isolated database, verified backup and rendered views.

Windows desktop input automation was unavailable because the computer-use native pipe was not present. Visual verification used in-process WPF rendering of the real application resources and viewmodels. Native mouse/keyboard interaction, system file-picker interaction and Explorer launch were not automated. The packaged application uses standard WPF/native dialogs for these actions.
