# Frontend-Architektur — Daily Gourmet

Dieses Dokument beschreibt die Ordner- und Codestruktur des Next.js-Frontends, wie sie sich aus dem feature-basierten Refactoring (2026) ergeben hat. Ziel ist eine Struktur, die über mehrere Jahre Weiterentwicklung trägt, ohne dass `app/` zu einer Ablage für Geschäftslogik wird.

Stand: reines Frontend mit statischen Mock-Daten (Phase 1). In Phase 2 wird die Datenhaltung schrittweise durch Aufrufe gegen das C#-Backend (`/api/v1`, siehe `docs/backend-architektur.md`) ersetzt — die hier beschriebene Struktur ist bewusst so geschnitten, dass dieser Austausch pro Feature isoliert möglich ist (siehe Abschnitt „API-Kommunikation").

## 1. Grundarchitektur

Drei Schichten, mit einer festen Abhängigkeitsrichtung:

```
app/        Next.js App Router — nur Routing, Layouts, dünne Seiten
   ↓ darf importieren aus
features/   Fachliche Domänen — Typen, Daten, Stores, Komponenten
   ↓ darf importieren aus
lib/        Geteiltes Fundament — Primitive, Stammdaten, UI-Bausteine ohne Fachlogik
components/ Reine Präsentations-Komponenten ohne Fachlogik (analog zu lib/)
```

**Regel:** `lib/` importiert nie aus `features/` oder `app/`. `features/` darf aus anderen `features/` importieren (z. B. `recipes` liest `ingredients`), aber nur wenn die Abhängigkeit fachlich echt ist, nicht aus Bequemlichkeit. `app/` darf aus `features/` und `lib/` importieren, nie umgekehrt.

Wenn eine neue Abhängigkeit diese Richtung verletzen würde (z. B. eine gemeinsam genutzte Hilfsfunktion in `lib/` bräuchte plötzlich einen Typ aus einem Feature), ist das ein Signal, die Funktion selbst zu verschieben — nicht die Regel zu umgehen. Beispiel aus diesem Refactoring: `lib/isoWeek.ts` enthielt ursprünglich `generateWeekTage()`, das den `SpeiseplanTag`-Typ aus dem Speiseplan-Feature brauchte. Die reinen Datumsfunktionen blieben in `lib/isoWeek.ts`, `generateWeekTage()` wanderte nach `features/meal-plans/utils.ts`.

## 2. Ordnerstruktur

```
src/
  app/                        Routing (Next.js App Router)
    admin/…/page.tsx           dünner Wrapper, importiert aus features/
    portal/…
    kitchen/…
    driver/…
    super-admin/…
  features/                   Fachliche Domänen (siehe Abschnitt 3)
    recipes/
    ingredients/
    meal-plans/
    production/
    orders/
    procurement/
    tenants/
    support/
    logistics/
    kitchen/
  lib/                        Geteiltes Fundament
    data/index.ts               Echte Stammdaten (Standorte, Einrichtungen, Benutzer,
                                 Bestellungen, Audit-Log) — siehe Abgrenzung unten
    types.ts                    Typen zu den Stammdaten oben
    store/create-store.ts       Generische Store-Factory (siehe Abschnitt 6)
    isoWeek.ts                  Reine ISO-Kalenderwochen-Funktionen
  components/
    ui/                         Reine UI-Bausteine (Button, Card, Table, StatusBadge, …)
    meal-plans/                 Geteilte Präsentations-Komponenten (WeekCalendar, DayColumn, …)
    shell/AppShell.tsx           Layout-Rahmen (Navigation, Header)
```

### Was gehört (noch) zu Recht in `lib/data`/`lib/types`?

Nicht jede Datenstruktur, die von mehreren Features gelesen wird, muss in `lib/` bleiben — die meisten Migrationen in diesem Refactoring haben genau das Gegenteil gezeigt (siehe `Speiseplan`, `ProduktionsPlan`, `LieferRoute`: alle sind vollständig in ihr jeweiliges Feature gewandert, obwohl mehrere andere Bereiche sie lesen). Entscheidend ist nicht „wird das von mehreren Stellen gelesen?", sondern **„gehört das einem Feature, oder ist es Stammdaten-Fundament, auf dem mehrere gleichrangige Features aufbauen?"**

In `lib/` verbleiben deshalb nur:

- **Rolle/Benutzer** — Identität, kein eigenes Feature (Benutzerverwaltung liegt bewusst beim Super Admin, siehe Berechtigungen unten)
- **Standort/Einrichtung** — Stammdaten, die praktisch jedes Feature referenziert (Rezepte nicht, aber Speisepläne, Produktion, Bestellungen, Logistik, Mandanten alle)
- **Bestellung/BestellPosition/BestellStatus** — bewusst *nicht* in `features/orders` verschoben, obwohl es dort einen Store und eigene UI gibt: Das Bestellungs-Array selbst wird von `features/production`, `features/meal-plans` (Umsatzberechnung) und `lib/logistics-store.ts` (vor der eigenen Migration) gelesen. `features/orders` besitzt die *Bearbeitungslogik* (Session-Store, Status-Übergänge, Admin-/Portal-UI), nicht die Stammdaten selbst.
- **AuditEintrag/Benachrichtigung** — plattformweite Querschnittsfunktionen ohne einzelnen fachlichen Besitzer
- **Einheit** — geteiltes Primitiv (g/kg/ml/l/Stück), von Rezepten, Zutaten und Produktion genutzt

Wenn du unsicher bist, ob etwas Neues in `lib/data` gehört: Standardmäßig **nein** — leg es in das Feature, das es primär bearbeitet. Nur wenn mindestens zwei gleichrangige Features die Daten unabhängig voneinander *lesen und keines sie bearbeitet*, ist `lib/data` die richtige Wahl.

## 3. Feature-Struktur

Jedes Feature unter `features/<name>/` folgt demselben Schema (nicht jede Datei ist in jedem Feature nötig):

```
features/<name>/
  types.ts        Domänen-Typen dieses Features
  data.ts          Statische Mock-Daten + reine Lookup-/Ableitungsfunktionen
  store.ts         Session-Store (siehe Abschnitt 6) — nur wenn das Feature
                    im Client bearbeitbaren Zustand hat
  utils.ts         Reine Hilfsfunktionen, die zu spezifisch für data.ts sind
  components/      Feature-eigene Komponenten (Formulare, Tabellen, Detailansichten)
```

Beispiel `features/recipes/`:

```
recipes/
  types.ts          Rezept, RezeptZutat, Schwierigkeitsgrad
  data.ts            rezepte[], REZEPT_KATEGORIEN, rezeptById(), rezeptAllergene()
  store.ts           addRezept(), updateRezept(), duplicateRezept(), useRezepte(),
                      + „Live"-Berechnungshelfer (rezeptAllergeneLive() etc.), die
                      zur Laufzeit mit der aktuellen Zutatenliste rechnen
  components/
    rezept-formular.tsx
    rezepte-tabelle.tsx
    rezept-detail.tsx
    skalierung.tsx
```

`app/admin/recipes/page.tsx` besteht dadurch nur noch aus Layout-Kram und einem Import:

```tsx
import { RezepteTabelle } from "@/features/recipes/components/rezepte-tabelle";

export default function RecipesPage() {
  return (
    <>
      <PageHeader title="Rezepte" actions={<Button href="/admin/recipes/new">Neu</Button>} />
      <RezepteTabelle />
    </>
  );
}
```

### Features dürfen von anderen Features abhängen

`features/recipes/data.ts` importiert `zutatById` aus `features/ingredients/data.ts` — das ist beabsichtigt und keine Ausnahme von der Schichtregel, solange die Richtung „vom spezielleren zum allgemeineren Feature" eingehalten wird. `production` hängt von `recipes`, `ingredients` und `orders`(-Stammdaten aus `lib/`) ab; `kitchen` hängt von `production`, `recipes`, `ingredients` und `logistics` ab. Ein Kreis (A importiert B, B importiert A) ist ein Zeichen, dass eine der beiden Seiten die falsche Zuständigkeit hat.

### Wann bekommt Portal-/Fahrer-/Küchen-UI ein eigenes `components/`-Verzeichnis im Feature?

Zwei Muster existieren nebeneinander, je nachdem, wie die UI zum Store steht:

1. **UI ist nur ein sekundärer Konsument** eines Stores, den ein anderer Bereich „besitzt" (z. B. `app/portal/orders/orders-history.tsx` liest nur `useBestellungen()`) → bleibt unter `app/portal/…`, importiert direkt aus dem Feature.
2. **UI enthält die eigentliche fachliche Interaktion** (z. B. `driver/**` mit Ladeliste abhaken, Stopp-Status setzen — das ist keine reine Anzeige, sondern der Kern des Logistik-Workflows) → wandert vollständig nach `features/logistics/components/`.

Im Zweifel: Wenn die Komponente `useState` für mehr als reines UI-Ein-/Ausklappen nutzt oder Store-Mutatoren aufruft, gehört sie ins Feature.

## 4. API-Kommunikation

Aktuell (Phase 1) gibt es keine echte API — `data.ts`-Dateien enthalten statische Arrays, die die geplanten Backend-DTOs 1:1 abbilden (siehe Kommentar-Header in `lib/types.ts` und `docs/api-endpunkte.md`). Session-Stores (`store.ts`) simulieren Schreiboperationen rein im Client-Speicher.

Für Phase 2 ist die Struktur bewusst so geschnitten, dass der Austausch pro Feature isoliert erfolgen kann:

- `data.ts` wird durch TanStack-Query-Hooks ersetzt (`useRezepte()` bekommt dieselbe Signatur, liefert aber Server-Daten statt Store-Daten).
- `store.ts`-Mutatoren (`addRezept`, `updateRezept`, …) werden zu Mutations, die den Query-Cache invalidieren, statt ein lokales Modul-Objekt zu verändern.
- Komponenten in `components/` ändern sich im besten Fall nicht, da sie nur gegen die Hook-Signatur (`useRezepte(): Rezept[]`) programmieren, nicht gegen die Implementierung.
- Ein künftiger `lib/api/client.ts` (zentraler Fetch-Wrapper mit Base-URL, Auth-Header, Fehlerbehandlung) wird von den TanStack-Query-Hooks in jedem Feature genutzt — nicht direkt von Komponenten.

## 5. Server- vs. Client-Components

Grundregel: **so viel Server Component wie möglich, so wenig `"use client"` wie nötig.**

- `app/**/page.tsx` sind, wo möglich, Server Components (kein `"use client"`) — sie lesen Daten (aktuell aus `features/*/data.ts`, künftig serverseitig aus der API) und reichen sie an Client-Komponenten weiter oder rendern direkt.
- Alles mit `useState`, `useEffect`, Event-Handlern oder Store-Hooks (`useX()` aus `store.ts`) braucht `"use client"` — das sitzt in `features/*/components/`.
- Reine Anzeige-Detailseiten mit dynamischem Routen-Segment (`[id]/page.tsx`) folgen einem von zwei Mustern:
  - **Async Server Component mit `notFound()`**, wenn nicht gefunden werden zu einer echten 404 führen soll (z. B. `features/kitchen/components/recipe-requirement.tsx`, weil ein Produktionsplan/Rezept, das nicht existiert, ein Server-seitiger Fehlerfall ist).
  - **Client Component mit `EmptyState`**, wenn die Daten aus einem Session-Store kommen und „nicht gefunden" ein normaler, transienter Zustand sein kann (z. B. `features/meal-plans/components/plan-detail.tsx` — ein Plan kann existieren, aber noch nicht im Store geladen sein).

  Welches Muster passt, hängt davon ab, ob die Datenquelle serverseitig statisch (→ `notFound()`) oder clientseitig veränderlich (→ `EmptyState`) ist.

## 6. Session-Stores

Da es (noch) kein Backend gibt, simulieren Features mit bearbeitbaren Daten client-seitigen Zustand über `lib/store/create-store.ts`:

```ts
export function createStore<T>(initial: T) {
  // Modul-Zustand + Listener-Set + useSyncExternalStore
  return { get, set, subscribe, useValue };
}
```

Ein Store hält **einen** Wert. Braucht ein Feature mehrere unabhängige Zustände (z. B. `features/support/store.ts` mit Tickets, Sitzung, Ereignissen und Feature-Flags), werden mehrere `createStore()`-Instanzen nebeneinander in derselben `store.ts` verwendet — nicht ein Store mit verschachteltem Objekt, weil sonst jede Komponente bei jeder Änderung irgendeines Teilzustands neu rendert.

Seed-Daten kommen immer als **Deep Clone** aus `data.ts`:

```ts
const store = createStore<Rezept[]>(
  rezepte.map((r) => ({ ...r, zutaten: r.zutaten.map((rz) => ({ ...rz })) }))
);
```

Mutatoren dürfen ausschließlich aus Client-Event-Handlern aufgerufen werden, nie aus Server-Code — das Modul bleibt serverseitig prozessweit resident, ein Server-seitiger Aufruf würde also für alle Nutzer gleichzeitig gelten.

**Bekannte Einschränkung, bewusst nicht automatisch behoben:** Mehrere Bereiche lesen an einer Stelle den *statischen* Seed (`data.ts`) und an anderer Stelle den *Session-Store* (`store.ts`) desselben Features — z. B. liest die Küche `produktionsplaene` direkt aus `features/production/data.ts`, während der Admin-Bereich über `useProduktionsplaene()` den bearbeitbaren Store nutzt. Ändert der Admin eine Zusatzmenge, sieht die Küche das nicht. Das ist historisch gewachsen (unabhängig entwickelte Bereiche) und wurde beim Verschieben unverändert mitgenommen, da eine Vereinheitlichung eine Verhaltensänderung wäre, die einer bewussten Produktentscheidung bedarf, keiner Architekturentscheidung. Bei künftiger Arbeit an Küche, Produktion, Bestellungen oder Logistik: erst prüfen, ob eine Komponente den Store oder den statischen Seed lesen sollte.

## 7. Types

- Jedes Feature definiert seine Typen in der eigenen `types.ts` — keine globale „types.ts mit allem".
- `lib/types.ts` enthält nur die in Abschnitt 2 begründeten Stammdaten-Typen.
- Enums/Status-Unions leben beim zugehörigen Datentyp (`SpeiseplanStatus` in `features/meal-plans/types.ts`, nicht in `lib/types.ts`), außer der Status-Typ wird auch von Stammdaten gebraucht (`BestellStatus` bleibt in `lib/types.ts`, weil `Bestellung` dort bleibt).
- Keine Typ-Duplikate: Wenn zwei Features dieselbe Struktur brauchen, prüfen, ob eines der beiden eigentlich vom anderen abhängen sollte (siehe Abschnitt 3), statt den Typ zu kopieren.

## 8. Validierung

Aktuell keine Formular-Validierungsbibliothek im Projekt (kein Zod, kein React Hook Form) — Formulare nutzen native `required`/`type="number"` HTML-Validierung plus einfache `kannSpeichern`-Booleans in der Komponente. Für Phase 2 ist vorgesehen:

- **Client**: Zod-Schemas pro Feature in `features/<name>/schemas.ts`, geteilt zwischen Formular-Validierung und (optional) Typ-Inferenz für `types.ts`.
- **Server**: FluentValidation im C#-Backend (siehe `docs/backend-architektur.md`) bleibt die verbindliche Quelle der Wahrheit; Client-Validierung ist nur UX-Vorabprüfung.

## 9. Berechtigungen

Aktuell keine echte Authentifizierung (Phase 1, reines Frontend-Mock) — jeder App-Bereich (`admin`, `portal`, `kitchen`, `driver`, `super-admin`) ist eine eigene Route-Gruppe mit fest hinterlegtem Beispielnutzer in `layout.tsx` (`userName`, `userRole`), keine Rollenprüfung zur Laufzeit.

Eine bereits getroffene, bewusste Entscheidung: **Benutzerverwaltung gibt es nur auf Plattformebene beim Super Admin** (`super-admin/users`, rein lesend), nicht als eigenständiges Feature/Bereich innerhalb eines Mandanten — im Rahmen des Wartungsvertrags verwaltet ausschließlich Daily Gourmet als Betreiber die Zugänge. Das ist der Grund, warum es kein `features/users/` gibt, obwohl `Benutzer` ein eigener Datentyp ist.

Für Phase 2 vorgesehen: zentrale Permission-Helper (`lib/permissions.ts` oder je Feature `can<Aktion><Ressource>(user, …)`), serverseitig durchgesetzt über EF-Core-Query-Filter + Policy-Handler (siehe `docs/backend-architektur.md`, Abschnitt 3–4), clientseitig nur zur UI-Steuerung (Buttons aus-/einblenden), nie als alleinige Zugriffskontrolle.

## 10. Coding-Konventionen

| Was | Konvention | Beispiel |
|---|---|---|
| Dateien | kebab-case | `rezept-formular.tsx`, `create-store.ts` |
| Komponenten | PascalCase (Funktionsname = Dateiname in PascalCase) | `RezeptFormular`, `TenantsManager` |
| Hooks | `useX` | `useRezepte()`, `useSupportTickets()` |
| Store-Mutatoren | Verb + Substantiv, keine Präfix-Konvention wie `set`/`update` erzwungen — sprechender Name zählt mehr als Konsistenz um der Konsistenz willen | `addRezept`, `duplicatePlan`, `refreshBestellmengen` |
| Imports | `@/`-Alias statt relativer Pfade über Verzeichnisgrenzen hinweg; relative Importe (`./`, `../`) nur innerhalb desselben Features | `import { useRezepte } from "@/features/recipes/store"` |
| Deutsche Fachbegriffe | Domänen-Namen bleiben deutsch (Rezept, Speiseplan, Bestellung, Einrichtung) — spiegelt die Fachsprache der Nutzer:innen und die geplanten Backend-Entitäten | — |
| Kommentare | nur wenn der Grund nicht aus dem Code hervorgeht (Workaround, Business-Constraint) — keine Was-Kommentare | siehe `PRODUKTIONS_META_FALLBACK`-Kommentar in `features/kitchen/data.ts` |

## 11. Wie baue ich ein neues Feature? Beispiel: `suppliers` (Lieferanten)

Angenommen, es soll eine eigenständige Lieferantenverwaltung entstehen (aktuell steckt `lieferant`/`artikelnummer` nur als Freitextfeld in `Zutat`).

1. **Typen zuerst.** `features/suppliers/types.ts`:
   ```ts
   export interface Lieferant {
     id: string;
     name: string;
     ansprechpartner: string;
     telefon: string;
     email: string;
     kategorien: string[];
   }
   ```

2. **Daten.** `features/suppliers/data.ts` — statisches Seed-Array + reine Lookups (`lieferantById`), analog zu `features/tenants/data.ts`.

3. **Store, falls bearbeitbar.** `features/suppliers/store.ts` nach dem Muster aus Abschnitt 6 — nur anlegen, wenn Admins Lieferanten im Client anlegen/bearbeiten können sollen. Reine Anzeige-Features (aktuell keine im Projekt) brauchen keinen Store.

4. **Blast-Radius prüfen, bevor Bestehendes angefasst wird.** Falls `Zutat.lieferant` (aktuell ein `string` in `features/ingredients/types.ts`) auf `lieferantId: string` umgestellt werden soll: grep nach allen Stellen, die `zutat.lieferant` lesen (`procurement-board.tsx` u. a.), und einzeln umstellen — nicht den Typ ändern und hoffen, dass TypeScript alles findet, sondern die Konsumenten vorher kennen. Falls die Umstellung von `Zutat` echte fachliche Unsicherheit aufwirft (z. B. „was passiert mit bestehenden Freitext-Werten ohne passenden Lieferanten-Datensatz?") — das ist eine Produktentscheidung, keine Architekturentscheidung, und gehört dem Team vorgelegt, nicht autonom entschieden.

5. **Komponenten.** `features/suppliers/components/lieferanten-tabelle.tsx`, `lieferant-formular.tsx`, `lieferant-detail.tsx` — nach dem Muster von `features/tenants/components/` (die strukturell am ähnlichsten ist: Liste + Anlage-Formular + Detailseite mit Bearbeiten-Modus).

6. **Route anlegen.** `app/admin/suppliers/page.tsx`, `new/page.tsx`, `[id]/page.tsx` — jeweils nur Import + minimales Layout, keine Logik:
   ```tsx
   import { LieferantenTabelle } from "@/features/suppliers/components/lieferanten-tabelle";

   export default function SuppliersPage() {
     return (
       <>
         <PageHeader title="Lieferanten" actions={<Button href="/admin/suppliers/new">Neu</Button>} />
         <LieferantenTabelle />
       </>
     );
   }
   ```

7. **Navigation ergänzen.** Eintrag in `app/admin/layout.tsx`s `nav`-Array.

8. **Verifikation.** `npx tsc --noEmit -p .`, `npm run build`, `npm run lint`, kurzer Dev-Server-Check der neuen Routen — siehe Vorgehen in allen bisherigen Feature-Migrationen dieser Session.

Kein Schritt in dieser Liste erfordert, `lib/` anzufassen — das ist der Test dafür, dass ein neues Feature sauber eingebettet ist.
