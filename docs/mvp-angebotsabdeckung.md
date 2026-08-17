# MVP-Abdeckung des Angebots

Stand: 11. August 2026

## 1. Konzeption und technische Planung

Frontend abgedeckt:

- Rollenbereiche für Plattform, Verwaltung, Küche, Einrichtung und Fahrer
- zentrale Domain-Typen für Benutzer, Einrichtungen, Zutaten, Rezepte, Speisepläne, Bestellungen, Produktion und Einkauf
- getrennte Navigations- und Arbeitsbereiche je Benutzerrolle
- klares Rollenmodell: Super Admin als Softwareinhaber, Tenant Owner als Firmenleitung sowie getrennte Fachrollen für Küche, Fahrer und Einrichtungen
- dokumentierte Backend-Architektur und geplante API-Endpunkte

Relevante Dokumente: `backend-architektur.md`, `api-endpunkte.md` und diese Abdeckungsmatrix.

## 2. Administrationsbereich

Frontend abgedeckt:

- Benutzer einladen, Rollen zuweisen, aktivieren und deaktivieren
- Rezeptliste, Rezeptanlage, Bearbeitung, Duplizierung und Portionsskalierung
- Zutatenliste, Zutatenanlage und Bearbeitung
- Speisepläne erstellen, bearbeiten, duplizieren, zur Prüfung senden und veröffentlichen
- Einrichtungen und Unternehmensdaten einsehen
- Bestell-, Produktions-, Einkaufs-, Umsatz- und Routenverwaltung
- Super-Admin-Mandantenverwaltung mit Anlage, Bearbeitung, Sperrung, Reaktivierung und protokollierten Feature-Schaltern
- Supportcenter für Fragen und Fehler der Tenant Owner mit Status, Antworten und Aktivitätsverlauf
- sichtbarer, auf 60 Minuten begrenzter Supportzugriff vom Super Admin in den Mandantenbereich

## 3. Speiseplan- und Bestellmanagement

Frontend abgedeckt:

- Kalenderwochenpläne mit Gerichten je Tag
- Statusfolge Entwurf, Prüfung und Veröffentlichung
- Portionsmengen je Einrichtung und Gericht
- Entwurf speichern und Bestellung verbindlich absenden
- Hinweise je Liefertag übermitteln
- zentrale Bestellübersicht mit Suche und Statusfilter
- Bestellungen bestätigen, sperren und nach Frist mit Begründung zur Korrektur freigeben
- Fristerinnerungen und CSV-Export

## 4. Kundenportal

Frontend abgedeckt:

- rollenbezogener Zugang als Einrichtung
- veröffentlichte Speisepläne je Kalenderwoche
- Portionseingabe und Tageshinweise
- Entwurfs- und Absendeprozess
- Bestellhistorie mit Status, Frist und Positionen
- Einrichtungs-, Benutzer- und Kontaktdaten

## 5. Produktions- und Bedarfsplanung

Frontend abgedeckt:

- Produktionsmengen aus Bestellungen und Zusatzmengen
- automatische Rezeptskalierung auf die Produktionsportionen
- Zutatenbedarf je Gericht und als aggregierter Tagesbedarf
- Lagerorte, Fehlmengen, Chargen, Geräte und Arbeitsplätze
- Einkaufslisten mit Einkaufseinheiten und Lieferanten
- editierbare Bestellmengen, Preisberechnung, CSV, Druck und Einkaufsstatus
- Tourbereitstellung, Packlisten und Fahrerübergabe

## 6. Testing, Qualitätssicherung und Inbetriebnahme

Im Frontend abgedeckt:

- TypeScript im Strict-Modus
- erfolgreicher optimierter Next.js-Produktionsbuild
- getrennte Buildverzeichnisse für Entwicklung und Produktion
- lokale HTTP-Prüfung aller Kernrouten
- responsive Oberflächen, Tastaturfokus und beschriftete Formfelder

Für die produktive Inbetriebnahme noch außerhalb des reinen Frontends erforderlich:

- C#-Backend und persistente Datenbank
- echte Anmeldung, Sessions und serverseitige Rollenprüfung
- serverseitige Frist- und Freigabevalidierung
- E-Mail- beziehungsweise Benachrichtigungsversand
- Datei-, Etiketten- und Druckdienste
- automatisierte End-to-End-Tests gegen das integrierte System
- Hosting, Domains, Backups, Monitoring und Datenschutzkonfiguration
