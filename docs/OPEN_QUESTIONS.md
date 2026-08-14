# Offene Fragen

## 1. Nährwert-Strategie: Open Food Facts/USDA vs. BLS

**Frage:** Welche Datenquelle ist die primäre Nährwert-/Allergenquelle für Zutaten — die externe Lebensmittel-API (Open Food Facts/USDA, wie in `docs/backend-architektur.md §7` geplant) oder der Bundeslebensmittelschlüssel (BLS, wie im aktuellen Aufgabenkatalog §35 und im langfristigen DGE-Zertifizierungsziel des Nutzers)?

**Kontext:**
- Der Frontend-Code selbst (`frontend/src/features/recipes/types.ts`, Kommentar auf `Rezept.naehrwertePro100g`) geht aktuell von einer offenen Zutaten-API aus — die Zutaten-Stammdaten sind bewusst "Dummy-Platzhalter ohne echte Nährwerte/Allergene ... kommen erst mit Anbindung der offenen Zutaten-API".
- Das langfristige Ziel des Nutzers (siehe Projekt-Memory) ist eine **DGE-Zertifizierung** (Gemeinschaftsgastronomie) über den DGE-VerpflegungsCheck — dafür ist der BLS die maßgebliche, amtliche Referenz, nicht Open Food Facts/USDA (Konsumentendaten, lückenhaft für Großküchen-Grundzutaten).
- Beide Quellen sind über `Zutat.Naehrwert.Quelle` (`OpenFoodFacts|Usda|Manual|Bls`) architektonisch kompatibel — die Frage betrifft die **Priorität/Reihenfolge der Implementierung**, nicht die Machbarkeit.

**Optionen:**
1. Open Food Facts/USDA zuerst (wie ursprünglich geplant, deckt sich mit dem aktuellen Frontend-Kommentar), BLS als spätere Ergänzung für DGE-Zertifizierung.
2. BLS zuerst (passt zum eigentlichen Geschäftsziel DGE-Zertifizierung), Open Food Facts/USDA optional/später oder ganz weglassen.
3. Beide parallel ab Phase 4, Nutzer wählt pro Zutat die Quelle.

**Technische Auswirkung:** `INutritionProvider`-Abstraktion und `Zutat.BlsLebensmittelId` sind in beiden Fällen gleich; unterschiedlich ist nur, welcher Provider zuerst implementiert wird und welche UI-Suche (`/nutrition/search`) das Frontend zuerst bekommt.

**Empfehlung:** Option 2 (BLS zuerst), da es das eigentliche Geschäftsziel (DGE-Zertifizierung) direkt bedient und amtliche Referenzdaten statisch/lizenzfrei importierbar sind (kein Live-API-Risiko). Muss vor Phase 4 final entschieden werden.

---

## 2. MonsterASP-Hosting-Details

**Frage:** Welche konkreten Fähigkeiten bietet der MonsterASP-Hosting-Plan?

**Kontext:** Die Architektur setzt einen `BackgroundService` für den Outbox-Worker (§32) voraus — unter klassischem IIS/ASP.NET-Core-Hosting (In-Process oder Out-of-Process auf MonsterASP) muss sichergestellt sein, dass Hintergrunddienste nicht durch App-Pool-Recycling/Idle-Timeout unterbrochen werden. Ebenso offen: SQL-Server-Version/Edition im Hosting-Paket, ausgehender SMTP-Zugriff, Unterstützung für Docker/Container (falls das lokale `docker-compose`-Setup 1:1 übernommen werden soll) oder klassisches WebDeploy.

**Optionen:** (a) Always-On/Keep-Alive-Konfiguration im MonsterASP-Panel aktivieren, (b) Outbox-Verarbeitung über einen externen Scheduler (z. B. periodischer HTTP-Trigger) statt `BackgroundService`, falls Always-On nicht verfügbar ist.

**Auswirkung:** betrifft nur Phase 10 (Platform/Outbox) und `docs/DEPLOYMENT.md` — blockiert Phase 1 nicht.

**Empfehlung:** Vor Phase 10 klären; bis dahin mit Annahme "Always-On aktivierbar" weiterarbeiten.

---

## 3. SMTP/Mail-Provider

**Frage:** Welcher SMTP-Provider wird produktiv für den E-Mail-Versand (Einladungen, Passwort-Reset, Benachrichtigungen) verwendet?

**Kontext:** Lokal reicht Mailpit/ein Dev-SMTP-Container. Produktiv braucht `IEmailSender` echte Zugangsdaten.

**Auswirkung:** betrifft Phase 3 (Invitations) und Phase 10 (Notifications) — blockiert Phase 1 nicht.

**Empfehlung:** Mit `IEmailSender`-Interface + Dev-Implementierung (Mailpit) weiterarbeiten; Produktiv-Provider vor Phase 3 final klären.
