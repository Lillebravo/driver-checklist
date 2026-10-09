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
   * **Släpvagnar:** Operatören kan välja eller fylla i upp till **2 släpvagnar**. Söklistan visar bilens vanliga släp först och övriga registrerade släp under en avskiljare. Registreringsnummer jämförs utan skillnad på mellanslag eller stora/små bokstäver. Ett släp är nytt endast om det saknas i hela registret; tankuppgifter hämtas även när släpet används med en annan eller ny bil.
3. **Välj Produkter:**
   * Operatören bockar för vilka produkter som ska lastas under transporten.
4. **Generera, Visa/Redigera & Skriv Ut:**
   * Motorn beräknar hur många och vilka checklistor som krävs baserat på utlastningsplatser och malltyper.
   * Före generering kan varje planerad checklista öppnas med **Redigera första sidan** och sparas som utkast.
   * **NY CHAUFFÖR** skrivs i fetstil efter ADR-datumet. **NY BIL** och **NY SLÄP** skrivs i fetstil efter respektive giltighetsdatum vid godkännandecertifikatet. SAP-/mängdraden används inte för statusflaggor.
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
| **PAX** | PAX 15, 60, 100 | UN 1760 / UN 3264 | `STATION_PAX` | **Typ 1** |
| **BDP** | BDP 865, 870 | UN 1760 / UN 3264 | `STATION_BDP` | **Typ 1** |

> **Status i denna MVP:** Typ 1- och Typ 2-mallarna är kopplade till riktiga Excel-filer och fullt fungerande. Typ 3 (ALS/LUT) finns med i produktkatalogen och domänmodellen för att vara redo när en mallfil tas fram, men saknar ännu en fysisk mall (se [Services/TemplateResolver.cs](backend/DriverChecklist.Api/Services/TemplateResolver.cs)).

### 2.2 Utskrifts- och Grupperingsregler

1. **Gemensamma utlastningar:**
   * **SVS 98 % & SVS 37 %:** Delar utlastningsplats (`STATION_SVS_98_37`) och använder **Typ 2**. Lastar en bil både 98 % och 37 % SVS krävs endast **en** checklista för dessa.
2. **Separata utlastningar:**
   * **SVS 94–97 %:** Har en egen station (`STATION_SVS_97`). Om en transport lastar både 97 % och 98 % genereras **två** separata checklistor av Typ 2.
   * **ALS & LUT:** Båda använder **Typ 3**, men har separata stationer (`STATION_ALS` respektive `STATION_LUT`) → kräver **två** separata checklistor av Typ 3.
   * **SAS & PIX:** Båda använder **Typ 1**, men har separata stationer (`STATION_SAS` respektive `STATION_PIX`) → kräver separata checklistor.
   * **PAX & BDP:** Båda använder **Typ 1**, men har separata stationer (`STATION_PAX` respektive `STATION_BDP`) → kräver separata checklistor.
3. **Interna varianter:**
   * Olika produktnummer inom samma familj (t.ex. PIX 111 och PIX 113, PAX 15 och PAX 60 eller BDP 865 och BDP 870) kräver inte separata checklistor sinsemellan då de delar station.
4. **Formel för antal checklistor:**

   ```
   Antal checklistor = Antal unika kombinationer av (ChecklistType, LoadingStationId)
   ```

   Implementerat i frontend av [PrintJobPlannerService](frontend/src/app/services/print-job-planner.service.ts).

---

## 3. Ekipage- och Flagglogik (Ny Chaufför / Fordon)

Eftersom Excel-filen är delad via Microsoft Teams/SharePoint och öppnas i **read-only** sker inga databasskrivningar från systemet. Markeringarna placeras vid relevanta kontrollpunkter:

```text
ADR (E15): Giltighet: 2028-10-08 NY CHAUFFÖR
Godkännandecertifikat (E17): Bil: 2028-04-05 NY BIL
                           Släp 1: 2028-06-07 NY SLÄP
```

Endast markeringstexten läggs till som fetstilt rich text. Bil och släp kan båda markeras i samma checklista. Ett registrerat släp blir inte nytt bara för att det kopplas till en annan bil.

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
   * Fetstilta ny-markeringar vid ADR-giltighet respektive godkännandecertifikat.
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

* **Redigera första sidan** finns även innan någon fil har genererats. **Spara utkast** sparar endast i webbsessionens minne; ingen fil skapas. Vid nästa generering används utkastet. Om underlaget ändras måste utkastet granskas och sparas igen.
* **Visa / Redigera** öppnar samma [ChecklistEditModalComponent](frontend/src/app/components/checklist-edit-modal/checklist-edit-modal.component.ts) efter generering. **Spara & uppdatera** regenererar Excel-kopian med ändringarna.
* Det moderna formuläret följer första sidans fält och ordning: datum/tid, åkeri, chaufför, bil/släp, SAP-nummer, mängd, container/vagn, UN-nummer, samtliga kontrollrader, roller, TT/TC/RC-kryss, kommentarer, ADR-/certifikatdatum, assistans, fyra tankkoder/inspektioner och sex fackvolymer. Grå rutor är spärrade precis som i mallen. Avmarkeringar ersätter automatiska kryss.
* Kontrollfrågor, roller, tillåtna krysskolumner och UN-nummer läses read-only från Excel via `GET /api/checklist/first-page/{templateType}`. Samma komponent fungerar för båda mallarna.
* Dragbil, Släp 1 och Släp 2 har var sin **TC (tankcontainer)**-ruta bredvid ett kompakt registreringsfält. TC visar enhetens containernummerfält på samma rad (på mycket smala skärmar radbryts fältet). Aktiva nummer följer med till utkast, redigering och Excel, i ordningen Dragbil / Släp 1 / Släp 2, separerade med ` / `. Ett ifyllt containernummer placerar automatiska kryss i TC (mittkolumnen) istället för TT. När numret läggs till eller tas bort i redigeraren flyttas befintliga kryss mellan TT och TC där målkolumnen är tillåten; avmarkeringar, RC och kommentarer bevaras. Avstängd TC skickar inte enhetens dolda nummer.
* Alla produkter finns i en gemensam dropdown. Produkter vid samma station kan kombineras; byte till en annan station byter produktgrupp och vid behov mall. Separata stationer genereras fortfarande som separata checklistor. Typ 3 kräver fortfarande en fysisk mall.
* Fel vid malläsning eller uppdatering visas i redigeraren. Utkast och genererade filer finns bara i minnet och försvinner vid omladdning.
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

### Portabel Windows-app (utan VS Code, Node.js eller .NET på arbetsdatorn)

Kör `packaging\Build-Portable.ps1` på utvecklingsdatorn med Node.js,
projektets npm-beroenden och .NET SDK installerade:

```powershell
powershell -NoProfile -File .\packaging\Build-Portable.ps1 -IncludeTemplates
```

Skriptet bygger frontend och publicerar backend self-contained för Windows x64.
Det skapar en tidsstämplad mapp och ZIP under `artifacts` (ignoreras av Git).
`-IncludeTemplates` inkluderar lokala checklistemallar; paketet måste då överföras
privat, inte publiceras. Utan flaggan kopierar du mallarna manuellt till paketets
`Templates`-mapp. Registerfilen ingår aldrig.

Packa upp **hela** ZIP-filen på arbetsdatorn och dubbelklicka
`Start-DriverChecklist.cmd` eller `DriverChecklist.Api.exe`. Appen öppnar
`http://localhost:5080` och lyssnar enbart lokalt. Låt konsolfönstret vara öppet;
stoppa med Ctrl+C. Ingen separat frontend-process eller installation behövs.
Det är ett portabelt mappaket, inte en ensam flyttbar exe-fil.

`portable.json` pekar initialt på `C:\Users\a6bn\Downloads\Regnummer.xlsx`.
Ändra filen med Anteckningar om sökvägen skiljer sig (JSON kräver dubbla backslash)
och starta om appen. Mallmappen `Templates` är relativ till exe-filens mapp.
Första körningen packar upp native-bibliotek till användarens temporära .NET-mapp.
Det är en osignerad testapp; följ företagets IT-policy och kringgå inte säkerhetskontroller.
Paketets `READ-ME.txt` innehåller start-, konfigurations- och felsökningsanvisningar.

### Förutsättningar

* [.NET 8 SDK](https://dotnet.microsoft.com/download)
* [Node.js 22](https://nodejs.org/) (minst 22.0) med npm. Angular CLI används från projektet; ingen global installation behövs.
* De två checklistemallarna (`.xlsx`) måste finnas på ditt Skrivbord med exakt dessa filnamn:
  * `Ny 1 Saltsyra , Pix, mm. Tankar MED skyddande beläggning.xlsx`
  * `Ny 2 Svavelsyra 94,97 och 98 Fennosize. Tankar UTAN skyddande beläggning.xlsx`

### Starta backend (http://localhost:5000)

```powershell
cd backend\DriverChecklist.Api
dotnet run --launch-profile http
```

### Starta frontend (http://localhost:4200)

```powershell
cd frontend
npm install   # endast första gången
npm start
```

Öppna sedan `http://localhost:4200` i webbläsaren.

### Test på arbetsdatorn med en nedladdad Regnummer.xlsx

Frontend och backend körs på **samma arbetsdator** i två separata PowerShell-fönster.
Ingen serverinstallation eller åtkomst från andra datorer behövs. Följ arbetsplatsens
regler för installation av .NET SDK och Node.js; fråga IT om installation är blockerad.

1. Kopiera den uppdaterade koden till arbetsdatorn, eller klona repot med Git:

   ```powershell
   git clone https://github.com/Lillebravo/driver-checklist.git
   cd driver-checklist
   ```

   Kloningen innehåller bara publicerad kod: lokala ändringar måste först överföras
   eller publiceras. Företagets Excel-register och checklistemallar ska **inte**
   läggas i Git. En ZIP av koden fungerar också om Git saknas.

2. Kontrollera verktygen med `dotnet --list-sdks`, `node --version` och `npm --version`.
   .NET 8 SDK eller senare måste kunna bygga projektets `net8.0`; körning kräver även
   .NET 8 / ASP.NET Core 8 runtime (ingår i .NET 8 SDK).

3. Kontrollera den nedladdade filen:

   ```powershell
   Test-Path 'C:\Users\a6bn\Downloads\Regnummer.xlsx'
   ```

   Resultatet ska vara `True`. Detta är en lokal ögonblickskopia av SharePoint-filen,
   inte en live-koppling. Ladda ner en ny kopia när du vill testa uppdaterade uppgifter.

4. Starta backend från repots rot i det första fönstret:

   ```powershell
   $env:MasterData__Path = 'C:\Users\a6bn\Downloads\Regnummer.xlsx'
   # Bara om checklistemallarna inte ligger på Windows Skrivbord:
   # $env:Templates__Path = 'C:\Users\a6bn\Downloads\Checklistemallar'
   dotnet run --project .\backend\DriverChecklist.Api --launch-profile http
   ```

   `Templates__Path` är en **mapp** med de två checklistemallarna ovan;
   `MasterData__Path` är sökvägen till **registerfilen**. De är olika källor.
   Miljövariablerna gäller bara detta PowerShell-fönster och måste anges igen nästa gång.

5. Starta frontend från repots rot i det andra fönstret:

   ```powershell
   cd frontend
   npm ci
   npm start
   ```

   `npm ci` behövs första gången och efter ändringar i paket/låsfilen.
   Frontend på `http://localhost:4200` proxyar `/api` till backend på `http://localhost:5000`.
   Starta om `npm start` efter ändringar i proxykonfigurationen.
   Låt båda fönstren vara öppna; stoppa med Ctrl+C.

6. Öppna `http://localhost:4200`. Sidan ska visa **Fordonsregister: Regnummer.xlsx**.
   Kontrollera att en bil och ett släp du känner igen finns i söklistorna och att
   godkännandedatumen är rätt. För exemplet blir bil `MBP 94C`, tankkod `L4BN`,
   godkänd till `2027-03-19`, och släp `RHA 067`, tankkod `L4BN`, godkänt till `2027-03-22`.
   API-svaret kan kontrolleras separat:

   ```powershell
   $data = Invoke-RestMethod 'http://localhost:5000/api/init-data'
   $data.vehicleRegistrySource
   $data.trucks | Select-Object regNr, tankCode, approvalExpiry
   ```

**Importens omfattning:** `bil`, `Släp/Trailer`, `Chaufförer`, `ADR Kort Datum`
och `Åkeri` läses. Kolumnnamnen
matchas oberoende av stora/små bokstäver, blanksteg och radbrytningar, bland de första
30 använda raderna på varje blad. Andra blad utan dessa rubriker ignoreras.
Fordonsceller läses per identifierbart reg.nr, inte som ett enda fordon.
Tankkod och datum kan stå i olika ordning; kolon, streck och rollanteckningar
stöds. Flera släp i samma cell importeras var för sig med egna uppgifter.
**Link betyder släp 1 och Trailer släp 2** när båda finns, oavsett textordning.
Släp utan bilkoppling finns under övriga registrerade släp; ingen bil uppfinns.
Tankkoder som `ADR`, `L4BN`, `L4BH`, `L4DH`, `LGBH`, `LGCH`, kombinationer
och containerkoder som `T11` bevaras. Kommentarer ger varningar, inte lastningstillstånd.
Fullständiga datum i ÅÅÅÅ-MM-DD eller DD-MM-ÅÅÅÅ stöds. Ogiltiga kalenderdatum,
enbart år/månad och tvåsiffriga år lämnas tomma med varning; inget datum gissas.
Enbart reg.nr (exempelvis `ABC 123`, `DN 21143` eller `XYZ789`) stöds också:
tankkod och godkännandedatum lämnas tomma och en importvarning visas.
Om samma reg.nr finns med kompletta uppgifter på en annan rad används dessa;
en ofullständig rad raderar aldrig redan kända uppgifter.
Fackbeskrivningar som `F.1&3 L4BN, F.2 L4BV(+)` bevaras som text;
de tolkas ännu inte som tankfack. Ett tomt släpfält stöds.
En avslutande containeranteckning som `Cont. HAAU 725001-0: L4BN`,
`Cont Nr ...` eller `Contnr: ...` utan
godkännandedatum stöds: det registrerade släpet importeras, och containern visas
som importvarning för manuell tank-/besiktningskontroll. Vid val av släpet
bockas TC automatiskt i och containernumret förifylls i motsvarande släpfält.
Containeruppgifter rensas vid byte till ett annat släp utan containerkoppling.
Containern registreras inte som släp och ärver inte släpets godkännandedatum.
Bil/släp med samma reg.nr dedupliceras utan hänsyn till blanksteg eller skiftläge;
återkommande rader lägger till bilens släpkopplingar. Motstridiga tankkoder/datum
lämnas tomma med varning, inte ersätts godtyckligt med första/sista raden.
Rena skrivskillnader i tankkoder, exempelvis `F.2`/`F-2`/`F2`,
blanksteg och `L4BV(+)`/`L4BV+`, orsakar inte konflikt.
Fackintervall (`1-3`) likställs däremot inte med enskilda fack (`1&3`).
Det gäller även olika containernummer för samma släp: ange aktuell container manuellt.
Otydliga poster varnas med blad/rad utan att stoppa säkra poster.
Enbart numeriska platshållare eller anteckningar utan reg.nr blir inte fordon.

Chaufförsnamn i samma cell separeras med radbrytning, semikolon eller minst två
blanksteg. Vanliga blanksteg inom ett namn bevaras. Om bladet saknar både
`Chaufförer` och `ADR Kort Datum` importeras enbart fordon med en synlig varning.
ADR-datum tolkas som dag-månad-år (`25-01-2029`), dag/månad/år (`12/11/2028`)
eller ISO-datum (`2030-07-26`). Blanksteg kring datumstreck och dubbla kolon stöds.
Excel-celler med datumtyp stöds också.
En etikett som `C:`, `LP:` eller `H.` matchas mot första bokstaven, fulla
namninitialer, första/sista namninitialerna eller en flerteckensprefixt som
`Mi:` / `Ma:` eller hela förnamnet (`David:`).
Datum används endast om kopplingen är entydig. En komplett uppsättning olika
etiketter kan exempelvis skilja `M:` från `MH:` när två chaufförer har samma förnamn.
Namn som råkat skrivas ihop kan separeras när en enda uppdelning stöds av
andra fullständiga namn i registret eller skilda datumetiketter; ordantal ensamt räcker inte.
Ett datum utan etikett stöds bara när raden har ett enda chaufförsnamn.
Namn utan entydigt datum importeras med tomt ADR-datum och varning;
datum gissas aldrig utifrån ordningen i cellen. Ett entydigt datum på en annan rad
för samma namn kan fylla i det saknade datumet. Motstridiga datum för samma namn
lämnas tomma. Ett frågetecken eller felaktigt datum för en annan etikett raderar inte
övriga tydligt kopplade datum på raden. Formatproblem varnas fortfarande med källrad.
Varning om saknat datum sammanställs per chaufför efter att alla rader slagits ihop,
så att ett datum som hittats på en annan rad inte längre rapporteras som saknat.
Åkeri förifylls endast om uppgiften är entydig för chauffören.
Bilkopplingarna hämtas från respektive rad i stället för att välja första bilen i registret.
Kontrollera den förvalda bilen när chauffören kör flera bilar.
ADR-datum måste fyllas i före generering. Att finnas i registret betyder inte
att chauffören är godkänd för självlastning; assistansval görs manuellt i Excel-läget.

**Inte importerat ännu:** Material, UN-nummer, signaturer, provtryckning och
stickprov/efterkontroll.
Excel-läget använder inga demochaufförer, påhittade fack eller demobesiktningsdatum.
Tankkoder förifylls i förhandsvisning, redigering och utskrift: bil först, sedan
släp 1 och släp 2. `ADR` är också en tankkod. Fackbeskrivningar bevaras på respektive
fordons tankrad, utan att uppfinna separata tankfack eller volymer.
Saknade koder och besiktningsuppgifter förblir tomma.
Kontrollera och fyll i återstående tankuppgifter via **Redigera första sidan** före utskrift.
Manuellt angivna chaufförer markeras som nya enligt befintlig logik.
Produkt- och operatörslistor är fortfarande programmets konfiguration.
Detta är ett funktionstest av fordonsuppslag, inte ett komplett underlag för verklig utlastning.

Filen öppnas endast för läsning och läses på nytt vid varje hämtning av masterdata
(ladda om webbsidan efter att du bytt kopian). Om den är låst exklusivt av Excel,
stäng Excel och försök igen. Vid läs-/formatfel visas ett fel; programmet faller
**inte** tillbaka till demofordon. Backend-fönstret innehåller felinformationen.
Utan `MasterData__Path` / `MasterData:Path` körs det gamla demoläget.

SharePoint-länken är en webbredigeringslänk och kan inte användas som lokal filsökväg.
Arbetsdatorns webbläsarinloggning är inte automatiskt backendens inloggning.
Den nedladdade kopian behöver däremot ingen SharePoint-autentisering i programmet.
Senare kan samma inställning peka på en OneDrive-synkad lokal fil.

### Köra tester

```powershell
# Backend
cd backend
dotnet build
dotnet run --project DriverChecklist.Tests -- ".\DriverChecklist.Api\Templates"
# Bara fordonsimporten (skapar syntetiska Excel-filer, inga företagsfiler behövs)
dotnet run --project DriverChecklist.Tests -- --registry-only
# Kontrollera en verklig arbetsbok read-only; skriver antal och radvarningar.
dotnet run --project DriverChecklist.Tests -- --registry-file "C:\Users\a6bn\Downloads\Regnummer.xlsx"

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

* Fordon, chaufförer, entydiga ADR-datum och åkeri kan läsas read-only från en lokal Excel-fil via `MasterData:Path` (se avsnitt 7). Utan sökväg används hårdkodad demodata i `InitialDataStore.cs`. Import av fack/besiktning återstår.
* Checklistemallarna läses just nu från Skrivbordet. Ska pekas om till en synkad Teams/OneDrive-mapp via `Templates:Path` i `appsettings.json`.
* Typ 3-mallen (ALS/LUT) saknar ännu en fysisk Excel-fil.
* Dragbilens egna besiktnings-/trycktestdatum (Tank 1) är placeholder-värden i demoläget. I Excel-läget används inga demobesiktningsdatum; tankuppgifter fylls i manuellt via första sidans redigering.
* "Skriv ut"-knappen laddar ner Excel-filen - webbläsare kan inte skicka en `.xlsx`-fil direkt till en fysisk skrivare utan att öppna den i ett program som Excel. Om genuin ett-klicks-utskrift (utan att öppna Excel) behövs senare krävs en server-side konvertering till PDF (t.ex. via LibreOffice headless), vilket är ett medvetet val att inte göra i denna MVP.
