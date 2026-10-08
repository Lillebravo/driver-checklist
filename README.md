# Utlastning Checklista Generator

Ett internt webbaserat verktyg för utlastning av kemikalier. Systemet slår upp chaufförer och fordonskombinationer från en delad Excel-fil (read-only), kontrollerar ADR-giltighet, hanterar kopplingar till dragbilar och upp till två släpvagnar, samt genererar och skriver ut rätt uppsättning färdigifyllda checklistor baserat på valda produkter och utlastningsstationer.

---

## 1. Huvudflöde i Gränssnittet

1. **Sök / Välj Chaufför:**
   * Operatören söker på chaufförens namn (autocomplete från Excel-arkivet).
   * Systemet visar chaufförens **ADR-utgångsdatum** direkt (med färgvarning om det närmar sig eller har passerat).
   * Om chauffören inte finns: Operatören anger manuellt namn och ADR-utgångsdatum. Chauffören flaggas internt som `Ny Chaufför`.
2. **Välj / Fyll i Fordonsekipage:**
   * När chauffören valts visas en lista över fordon som chauffören brukar köra.
   * Operatören kan välja en befintlig dragbil eller skriva in ett nytt registreringsnummer manuellt (flaggar som `Ny Dragbil`).
   * **Släpvagnar:** Operatören kan välja eller fylla i upp till **2 släpvagnar** (Släp 1 och Släp 2) med respektive besiktningsdatum, trycktestdatum och tankkod. Manuellt inmatade släp flaggas som `Nytt Släp 1` respektive `Nytt Släp 2`.
3. **Välj Produkter:**
   * Operatören bockar för vilka produkter som ska lastas under transporten.
4. **Generera, Visa/Redigera & Skriv Ut:**
   * Motorn beräknar hur många och vilka checklistor som krävs baserat på utlastningsplatser och malltyper.
   * Vid nya chaufförer/fordon stämplas tydliga textmarkeringar (`[NY CHAUFFÖR]`, `[NY DRAGBIL]`, `[NYTT SLÄP 1]`, `[NYTT SLÄP 2]`) på checklistan så att transportledare vet att uppgifterna ska föras in i master-Excelen manuellt i efterhand.
   * Varje genererad checklista får två knappar: **Visa/Redigera** (öppnar ett formulär i en modal på sidan - ingen ny flik - för att rätta till fel innan utskrift, se avsnitt 4) och **Skriv ut** (laddar ner Excel-filen så operatören kan öppna den och skriva ut via Ctrl+P). Ingen data skrivs tillbaka till master-Excel-filen.

---

## 2. Produkter, UN-nummer & Stationer

### 2.1 Produktkatalog & Stationsregler

| Produktgrupp | Variant / Koncentration | UN-nr | Utlastningsplats (Station ID) | Checklistemall |
| :--- | :--- | :--- | :--- | :--- |
| **Saltsyra (SAS)** | Saltsyra | UN 1789 | `STATION_SAS` | **Typ 1** |
| **Svavelsyra (SVS 94–97 %)** | SVS 94–97 % | UN 1830 | `STATION_SVS_97` | **Typ 2** |
| **Svavelsyra (SVS 98 %)** | SVS 98 % | UN 1830 | `STATION_SVS_98_37` *(Gemensam)* | **Typ 2** |
| **Svavelsyra (SVS 37 %)** | SVS 37 % | UN 2796 | `STATION_SVS_98_37` *(Gemensam)* | **Typ 2** |
| **Fennosize (AKD)** | Fennosize KD 364M | UN 1760 | `STATION_AKD` | **Typ 2** |
| **Aluminiumsulfat (ALS)**| ALS | UN 3264 | `STATION_ALS` | **Typ 3** |
| **Natronlut (LUT)** | Natronlut | UN 1824 | `STATION_LUT` | **Typ 3** |
| **PIX** | PIX 111, 113, 118, 311 | UN 2582 / UN 3264 | `STATION_PIX` | **Typ 1** |
| **PAX** | PAX 15, 60, 100 | UN 1760 / UN 3264 | `STATION_PAX_BDP` *(Gemensam)* | **Typ 1** |
| **BDP** | BDP 865, 870 | UN 1760 / UN 3264 | `STATION_PAX_BDP` *(Gemensam)* | **Typ 1** |

> **Status i denna MVP:** Typ 1- och Typ 2-mallarna är kopplade till riktiga Excel-filer och fullt fungerande. Typ 3 (ALS/LUT) finns med i produktkatalogen och domänmodellen för att vara redo när en mallfil tas fram, men saknar ännu en fysisk mall (se [Services/TemplateResolver.cs](backend/DriverChecklist.Api/Services/TemplateResolver.cs)).

### 2.2 Utskrifts- och Grupperingsregler

1. **Gemensamma utlastningar:**
   * **PAX & BDP:** Delar utlastningsplats (`STATION_PAX_BDP`) och använder **Typ 1**. Dessa kan samsas på en gemensam checklista.
   * **SVS 98 % & SVS 37 %:** Delar utlastningsplats (`STATION_SVS_98_37`) och använder **Typ 2**. Lastar en bil både 98 % och 37 % SVS krävs endast **en** checklista för dessa.
2. **Separata utlastningar:**
   * **SVS 94–97 %:** Har en egen station (`STATION_SVS_97`). Om en transport lastar både 97 % och 98 % genereras **två** separata checklistor av Typ 2.
   * **ALS & LUT:** Båda använder **Typ 3**, men har separata stationer (`STATION_ALS` respektive `STATION_LUT`) → kräver **två** separata checklistor av Typ 3.
   * **SAS & PIX:** Båda använder **Typ 1**, men har separata stationer (`STATION_SAS` respektive `STATION_PIX`) → kräver separata checklistor.
3. **Interna varianter:**
   * Olika produktnummer inom samma familj (t.ex. PIX 111 och PIX 113 eller PAX 15 och PAX 60) kräver inte separata checklistor sinsemellan då de delar station.
4. **Formel för antal checklistor:**

   ```
   Antal checklistor = Antal unika kombinationer av (ChecklistType, LoadingStationId)
   ```

   Implementerat i frontend av [PrintJobPlannerService](frontend/src/app/services/print-job-planner.service.ts).

---

## 3. Ekipage- och Flagglogik (Ny Chaufför / Fordon)

Eftersom Excel-filen är delad via Microsoft Teams/SharePoint och öppnas i **read-only** sker inga databasskrivningar från systemet. För att uppmärksamma manuell registrering sätts tydliga textmarkeringar i checklistan:

```text
┌────────────────────────────────────────────────────────┐
│              STATUS FÖR REGISTRERING                   │
│                                                        │
│ [X] NY CHAUFFÖR   [ ] NY DRAGBIL   [X] NYTT SLÄP 1     │
│ (Förs in manuellt i fordonsregistret av transportledare)│
└────────────────────────────────────────────────────────┘
```

Domänmodellen för detta (`GenerateChecklistRequest`, `VehicleUnit`, `ProductDefinition`, `ChecklistTemplate`-enumet) finns i [backend/DriverChecklist.Api/Models](backend/DriverChecklist.Api/Models).

---

## 4. Checklistegenerering

1. Valda produkter grupperas per (`ChecklistTemplate`, `LoadingStationId`) → en grupp = en checklista.
2. Varje grupp matchas mot rätt Excel-mall via [TemplateResolver](backend/DriverChecklist.Api/Services/TemplateResolver.cs).
3. Mallen fylls i via ClosedXML i minnet (`MemoryStream`) av [ChecklistGeneratorService](backend/DriverChecklist.Api/Services/ChecklistGeneratorService.cs):
   * Chaufför, ADR-giltighet, åkeri, datum/tid.
   * När en känd chaufför väljs förifylls åkerifältet med `PH Tank`. Det kan ändras manuellt och följer med till Excel-kopian. Vid byte till okänd chaufför rensas `PH Tank`, men andra manuellt angivna åkerinamn behålls.
   * Dragbil och släpens registreringsnummer.
   * UN-nummer och produktnamn för just den checklistan.
   * Statusflaggor (`[NY CHAUFFÖR]`, `[NY DRAGBIL]`, `[NYTT SLÄP 1/2]`).
   * Check-in-kryssrutor, ADR-giltighet, tankkoder (Tank 1–4) och inspektionstyp/datum.
   * Självlastning kryssas bara för kända chaufförer, även efter Visa/Redigera. Full/halv assist kan fortfarande väljas manuellt.
   * Signaturfält på baksidan: endast namnen placeras nederst till höger i separata textfält; mallens instruktionstext och dess placering lämnas orörda. Tredje namnfältet lämnas tomt.
   * Datumen på baksidan är centrerade i datumrutorna med 14 punkters text.
   * Mallens ritobjekt och bildrotationer bevaras, inklusive den horisontella Kemira-loggan.
4. Filen returneras direkt till frontend som nedladdning (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`), skrivs aldrig till disk på servern.

### Tankplatslogik (Tank 1–4)

Beräknas i frontend av [TankCalculationService](frontend/src/app/services/tank-calculation.service.ts):

* **Tank 1** = Dragbilen.
* **Tank 2–4** = Släp 1:s fack, därefter Släp 2:s fack, i exakt ordning.
* Giltighet: **+3 år** för tankbil/tanktrailer, **+2,5 år** om det är en tankcontainer.
* Inspektionstyp växlar varannan gång mellan **L** och **P**; gränssnittet varnar visuellt om datumet har passerat.

### Visa/Redigera och Skriv ut

Efter att en checklista genererats laddas den **inte** ner automatiskt. Den visas istället som ett kort under "Genererade checklistor" med två knappar:

* **Visa / Redigera** öppnar [ChecklistEditModalComponent](frontend/src/app/components/checklist-edit-modal/checklist-edit-modal.component.ts) - en modal direkt på sidan (ingen ny flik/fönster) med ett formulär förifyllt med exakt samma värden som skickades till backend vid genereringen (chaufför, ADR, åkeri, dragbil, släp, tankkoder). Produkterna visas read-only eftersom de styrs av produktvalet i steg 3. Vid **"Spara & uppdatera"** skickas det redigerade formuläret igenom samma `/api/checklist/generate`-endpoint igen, vilket regenererar Excel-filen i minnet med de rättade värdena - exakt samma fyllnadslogik som vid den ursprungliga genereringen återanvänds, så resultatet garanteras bli konsekvent. Checklistan märks då med en "Redigerad"-badge.
* **Skriv ut** laddar ner den (eventuellt redigerade) Excel-filen till datorn. Webbläsare kan inte skicka en `.xlsx`-fil direkt till en skrivare - filen öppnas i Excel (eller valfritt kalkylprogram) där operatören trycker Ctrl+P för att skriva ut.

---

## 5. Säkerhet & Fillåsning

Master-Excelen (masterdata) och checklistemallarna är tänkta att öppnas enbart i läsläge med delad åtkomst:

```csharp
new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
```

Inga ändringar skrivs tillbaka till Excel-filerna. Detta eliminerar risken för låsningar och filkorruption när filerna är öppna av andra användare i Microsoft Teams eller SharePoint. Se [ChecklistGeneratorService.cs](backend/DriverChecklist.Api/Services/ChecklistGeneratorService.cs).

**Mallfilernas plats (MVP):** Mallarna ligger just nu på den inloggade användarens Skrivbord (`Templates:Path` i `appsettings.json` är tom, vilket defaultar till skrivbordet). När mallarna flyttas till en delad Teams/OneDrive-kanal, sätt `Templates:Path` i `appsettings.json` till den synkade mappens sökväg - ingen kodändring krävs. På samma sätt är masterdata (chaufförer/fordon) hårdkodad i [InitialDataStore.cs](backend/DriverChecklist.Api/Data/InitialDataStore.cs) bakom gränssnittet `IMasterDataService`, redo att bytas ut mot en implementation som läser samma data read-only från den delade Excelen.

---

## 6. Projektstruktur

```
driver-checklist/
├── backend/
│   └── DriverChecklist.Api/          .NET 8 Minimal API
│       ├── Configuration/            Starkt typade options (TemplateOptions)
│       ├── Data/                     ProductCatalog + InitialDataStore (hårdkodad masterdata, MVP)
│       ├── Endpoints/                Extension-metoder som mappar API-routes
│       ├── Models/                   DTO:er och enums (delade kontrakt mot frontend)
│       │   └── MasterData/           Chaufförer, fordon, produktkatalog
│       ├── Services/                 Affärslogik (checklistegenerering, mallupplösning, masterdata)
│       └── Templates/                Lokal mallmapp (valfritt alternativ till Skrivbordet, se Templates/README.md)
├── frontend/
│   └── src/app/                      Angular 19 (standalone components)
│       ├── components/               driver-select, vehicle-select, product-select, tank-preview, checklist-edit-modal
│       ├── core/                     API-konfiguration, checklistemallens etiketter
│       ├── models/                   TypeScript-motsvarigheter till backendens DTO:er + GeneratedChecklist
│       └── services/                 ApiService, TankCalculationService, PrintJobPlannerService, FileDownloadService
└── docs/
    └── SPEC.md                       Denna specifikation (bakgrund/historik)
```

---

## 7. Kom igång

### Förutsättningar

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Node.js 20+](https://nodejs.org/) och Angular CLI (`npm install -g @angular/cli`)
* De två checklistemallarna (`.xlsx`) måste finnas på ditt Skrivbord med exakt dessa filnamn:
  * `Ny 1 Saltsyra , Pix, mm. Tankar MED skyddande beläggning.xlsx`
  * `Ny 2 Svavelsyra 94,97 och 98 Fennosize. Tankar UTAN skyddande beläggning.xlsx`

### Starta backend (http://localhost:5000)

```powershell
cd backend\DriverChecklist.Api
dotnet run
```

### Starta frontend (http://localhost:4200)

```powershell
cd frontend
npm install   # endast första gången
npm start
```

Öppna sedan `http://localhost:4200` i webbläsaren.

### Köra tester

```powershell
# Backend
cd backend
dotnet build
dotnet run --project DriverChecklist.Tests -- ".\DriverChecklist.Api\Templates"

# Frontend
cd frontend
npm test -- --watch=false --browsers=ChromeHeadless
```

Backendens regressionstest kräver de två riktiga Excel-mallarna i den angivna
mappen (byt sökvägen om de ligger på Skrivbordet). Det verifierar båda mallarna
med kända/nya chaufförer och alla assistansval, inklusive ritobjektens rotation,
namnens placering, datumformat och att mallfilerna inte ändras.

---

## 8. Kända begränsningar & nästa steg (MVP)

* Masterdata (chaufförer/fordon) är hårdkodad i `InitialDataStore.cs`. Ska bytas mot en tjänst som läser read-only från den delade Teams/OneDrive-Excelen (gränssnittet `IMasterDataService` finns redan på plats för detta).
* Checklistemallarna läses just nu från Skrivbordet. Ska pekas om till en synkad Teams/OneDrive-mapp via `Templates:Path` i `appsettings.json`.
* Typ 3-mallen (ALS/LUT) saknar ännu en fysisk Excel-fil.
* Dragbilens egna besiktnings-/trycktestdatum (Tank 1) är i dagsläget hårdkodade placeholder-värden i `TankCalculationService`, eftersom masterdatan för dragbilar ännu inte innehåller dessa fält.
* "Skriv ut"-knappen laddar ner Excel-filen - webbläsare kan inte skicka en `.xlsx`-fil direkt till en fysisk skrivare utan att öppna den i ett program som Excel. Om genuin ett-klicks-utskrift (utan att öppna Excel) behövs senare krävs en server-side konvertering till PDF (t.ex. via LibreOffice headless), vilket är ett medvetet val att inte göra i denna MVP.
