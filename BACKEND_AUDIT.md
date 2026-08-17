# Backend Audit — DailyGourmet

Status: 2026-08-16. Reflects the actual state of the repo right now, not the aspirational plan —
see `BACKEND_IMPLEMENTATION_PLAN.md` for the design and `README.md` for setup. This file is kept
honest on purpose: "Built" ≠ "Tested" ≠ "Frontend wired", and each row says exactly which of those
is true so nothing is silently assumed done.

**Legend** — Backend: ✅ built & unit-verified via live HTTP call in this session · 🔧 built, compiles,
DI-validated, but not exercised via an HTTP call · ❌ not built.
Frontend: ✅ wired to the real API (dummy data/store removed) · ⏳ still on Phase-1 dummy data/store.

---

## Auth & Identity

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Login (`/login`) | `POST /api/auth/login` | User | ✅ | ✅ |
| Current user / role gating | `GET /api/auth/me` | User | ✅ | ✅ |
| Logout | (client-side token clear) | — | ✅ | ✅ |
| Route protection per area (admin/kitchen/portal/driver/super-admin) | — | — | n/a | ✅ (`RequireRole`, replaces the old client-only passphrase gate) |
| User invite / deactivate / activate / resend | `POST /api/users`, `/{id}` PUT, `/deactivate`, `/activate`, `/resend-invitation` | User | 🔧 | ⏳ (`super-admin/users`, `admin` user mgmt still read dummy `benutzer[]`) |

## Facilities & Locations

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Facilities list/search/filter (`admin/facilities`) | `GET /api/facilities` | Facility | ✅ | ✅ |
| Create facility | `POST /api/facilities` | Facility | ✅ | ✅ |
| Edit/deactivate facility | `PUT /api/facilities/{id}` | Facility | 🔧 | ⏳ (no edit UI existed in the original frontend either — see plan §2 gap #1) |
| Locations (tenant-scoped CRUD) | `GET/POST /api/locations`, `PUT /{id}` | Location | 🔧 | ⏳ (facilities page's Standort dropdown reads live via `useStandorte()`; no dedicated locations admin page existed in the original frontend) |
| Super-admin cross-tenant locations | `GET /api/super-admin/locations` | Location | 🔧 | ⏳ |

## Recipes & Ingredients

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Recipes list/search/filter | `GET /api/recipes` | Recipe | ✅ | ⏳ |
| Recipe detail incl. live allergen/additive resolution | `GET /api/recipes/{id}` | Recipe, RecipeIngredient, Allergen | ✅ | ⏳ |
| Recipe create/update (incl. steps + ingredient lines) | `POST/PUT /api/recipes`, `/{id}` | Recipe, RecipePrepStep, RecipeIngredient | ✅ | ⏳ |
| Recipe duplicate / archive | `POST /api/recipes/{id}/duplicate`, `/archive` | Recipe | 🔧 | ⏳ |
| Recipe scaling ("Hochrechnung") | `GET /api/recipes/{id}/scale` | Recipe, RecipeIngredient | ✅ (verified 10kg × 2.5 = 25kg) | ⏳ |
| Ingredients list/search/filter | `GET /api/ingredients` | Ingredient | ✅ | ⏳ |
| Ingredient create (incl. duplicate-article-number conflict) | `POST /api/ingredients` | Ingredient | ✅ (verified 409 on duplicate) | ⏳ |
| Ingredient update / deactivate | `PUT /api/ingredients/{id}`, `/deactivate` | Ingredient | 🔧 | ⏳ |
| Category/allergen/target-group lookups | `GET /api/ingredient-categories`, `/allergens`, `/recipe-categories`, `/target-audience-groups` | (lookup tables) | ✅ | ⏳ |
| Suppliers | `GET/POST /api/suppliers`, `PUT /{id}` | Supplier | 🔧 | ⏳ |

## Meal Plans & Orders

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Meal plan list | `GET /api/meal-plans` | MealPlan | ✅ | ⏳ |
| Meal plan create (auto-generates Mon–Fri days) | `POST /api/meal-plans` | MealPlan, MealPlanDay | 🔧 | ⏳ |
| Meal plan edit / duplicate (ISO week rollover) | `PUT /api/meal-plans/{id}`, `/duplicate` | MealPlan, MealPlanDay, MealPlanItem | 🔧 | ⏳ |
| Submit-review / publish (recipe snapshot) / unpublish / archive | `POST .../submit-review`, `/publish`, `/unpublish`, `/archive` | MealPlan, MealPlanItem.RecipeSnapshotJson | 🔧 | ⏳ |
| Portal meal plan view (facility-scoped) | `GET /api/portal/meal-plans` | MealPlan, MealPlanFacility | ✅ | ⏳ |
| Orders list/filter (admin board) | `GET /api/orders` | Order | ✅ | ⏳ |
| Place/edit order (facility-scoped, deadline-enforced) | `POST/PUT /api/orders` | Order, OrderItem | ✅ (verified weekend-exclusion deadline rollback) | ⏳ |
| Submit order | `POST /api/orders/{id}/submit` | Order | 🔧 | ⏳ |
| Order override with reason (writes AuditLog) | `POST /api/orders/{id}/override` | Order, AuditLog | 🔧 | ⏳ |
| Order history | `GET /api/orders/{id}/history` | AuditLog | 🔧 | ⏳ |

## Production & Kitchen

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Production plan list/detail | `GET /api/production-plans`, `/{date}` | ProductionPlan, ProductionPlanItem | ✅ | ⏳ |
| Create plan (auto-aggregates ordered quantities) | `POST /api/production-plans` | ProductionPlan, ProductionPlanItem | 🔧 | ⏳ |
| Update item status/work-status/staging | `PUT .../items/{id}` | ProductionPlanItem | 🔧 | ⏳ |
| Adjustment with reason (audit trail) | `POST .../items/{id}/adjustments` | ProductionAdjustment | 🔧 | ⏳ |
| Refresh ordered quantities | `POST .../refresh` | ProductionPlanItem | 🔧 | ⏳ |
| Ingredient requirements aggregation | `GET .../requirements` | RecipeIngredient, Ingredient | ✅ (verified against seeded orders) | ⏳ |
| Deviations | `GET/POST /api/deviations`, `/resolve` | Deviation | 🔧 | ⏳ |
| Quality controls (HACCP) | `GET/POST /api/quality-controls` | QualityControl | 🔧 | ⏳ |
| Storage locations | `GET/POST /api/storage-locations`, `PUT /{id}` | StorageLocation | 🔧 | ⏳ |

## Procurement & Logistics

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Generate procurement list from production plan | `POST /api/procurement-lists/generate` | ProcurementList, ProcurementListItem | 🔧 | ⏳ |
| Procurement list/detail/edit item/status/CSV export | `GET/PUT /api/procurement-lists*` | ProcurementList, ProcurementListItem | ✅ (list verified) / 🔧 (rest) | ⏳ |
| Routes list/create (auto-populates stops from orders) | `GET/POST /api/routes` | DeliveryRoute, RouteStop, RouteStopItem | ✅ (list verified against seeded route) | ⏳ |
| Route status transitions | `PUT /api/routes/{id}/status` | DeliveryRoute | 🔧 | ⏳ |
| Driver stop status (deliver/problem), own-route enforced | `PUT .../stops/{id}/status` | RouteStop | 🔧 | ⏳ |
| Packing/loading toggles | `PUT .../items/{id}/packed`, `/loaded` | RouteStopItem | 🔧 | ⏳ |
| Driver "my routes" / "today" | `GET /api/drivers/current/routes*` | DeliveryRoute | ✅ | ⏳ |
| Drivers roster CRUD | `GET/POST /api/drivers`, `PUT /{id}` | Driver | 🔧 | ⏳ |

## Platform Admin (Super-Admin & Tenant Settings)

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Super-admin dashboard KPIs | `GET /api/super-admin/dashboard` | Tenant, User, Facility, Order | ✅ (real aggregates, not hardcoded) | ⏳ |
| Tenant CRUD + lock/unlock/archive (reason → AuditLog) | `GET/POST/PUT /api/super-admin/tenants*`, `/lock`, `/unlock`, `/archive` | Tenant, AuditLog | ✅ (list/create verified) | ⏳ |
| Global user list | `GET /api/super-admin/users` | User | 🔧 | ⏳ |
| Feature flags (global + per-tenant override) | `GET/PUT /api/super-admin/feature-flags`, `PUT /tenants/{id}/feature-flags` | FeatureFlag, TenantFeatureFlag | 🔧 | ⏳ |
| System status (real DB connectivity check) | `GET /api/super-admin/system` | — | ✅ | ⏳ |
| Tenant profile / company data | `GET/PUT /api/tenants/current/profile` | TenantProfile | 🔧 | ⏳ |
| Tenant settings (deadlines, numbering, notifications) | `GET/PUT /api/tenants/current/settings` | TenantSettings, TenantNotificationSetting | 🔧 | ⏳ |
| Audit log (tenant-scoped) | `GET /api/audit-logs` | AuditLog | ✅ | ⏳ |

## Support, Notifications & Revenue

| Frontend Feature | API Endpoint | DB Entity | Backend | Frontend |
|---|---|---|---|---|
| Support tickets + replies | `GET/POST /api/support/tickets`, `/replies`, `/status` | SupportTicket, SupportTicketReply | 🔧 | ⏳ |
| Support session start/end (60-min, server-enforced) | `POST /api/super-admin/tenants/{id}/support-sessions`, `DELETE /support-sessions/{id}` | SupportSession | 🔧 | ⏳ |
| Notifications | `GET /api/notifications`, `POST /{id}/read` | Notification | 🔧 | ⏳ |
| Admin dashboard summary (real formulas, not hardcoded) | `GET /api/dashboard/admin-summary` | Order, MealPlan, Facility | ✅ | ⏳ |
| Portal dashboard summary | `GET /api/dashboard/portal-summary` | Order, MealPlan | 🔧 | ⏳ |
| Revenue report (weekly totals + per-order detail) | `GET /api/revenue` | Order, Facility | 🔧 | ⏳ |

---

## Summary

- **Backend**: all planned endpoints across all 8 domain areas are implemented and compile/DI-validate
  as one solution (23 controllers, 24 handlers, 15 DTO files, 0 build warnings). A representative
  cross-section — auth, facilities, recipes (incl. scaling formula), ingredients (incl. conflict
  handling), meal-plans, orders (incl. deadline computation with weekend-exclusion), production
  requirements aggregation, procurement, routes, driver-scoped access, and super-admin cross-tenant
  aggregates — was exercised with real HTTP calls against the seeded database and confirmed correct.
  The remaining endpoints (marked 🔧) are built and pass compile-time/DI validation but have not each
  been individually smoke-tested with a live request in this session.
- **Frontend**: the auth/routing foundation is fully wired (real JWT login, `/auth/me`-driven route
  guards replacing every hardcoded demo identity and the old client-only super-admin passphrase, real
  logout). One full feature — Facilities — is wired end-to-end to the live API as the proof of the
  migration pattern (`src/lib/services/*.ts` hooks preserving the original `useX()` signatures per
  `docs/ARCHITECTURE.md`'s own Phase 2 plan). Every other feature listed above still reads its
  original Phase-1 dummy data/store — the backend they need is ready, but the swap-over has not been
  done yet.
- **Not testable in this environment**: no browser automation tool was available (Windows sandbox,
  no `chromium-cli`), so verification stopped at TypeScript compilation + a clean `next build` + live
  API calls. Visual/interactive confirmation that pages render and forms behave correctly still needs
  a real browser pass.

## Recommended next steps (in priority order)

1. Wire Ingredients + Recipes (`src/lib/services/ingredients.ts`, `recipes.ts`) — highest value, most
   other features (meal-plans, production, procurement) display recipe/ingredient names.
2. Wire Meal Plans + Orders (admin board and portal ordering flow) — the core day-to-day workflow.
3. Wire Production + Kitchen boards.
4. Wire Procurement + Logistics (admin routes + driver app).
5. Wire Super-Admin + tenant settings + support.
6. Do a full page-by-page regression pass in a real browser once all features are wired, per the
   original brief's §33 checklist (loading/empty/error states, forms, filters, pagination).
