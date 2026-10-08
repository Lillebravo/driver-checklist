import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { DriverSelectComponent } from './components/driver-select/driver-select.component';
import { VehicleSelectComponent } from './components/vehicle-select/vehicle-select.component';
import { ProductSelectComponent } from './components/product-select/product-select.component';
import { TankPreviewComponent } from './components/tank-preview/tank-preview.component';
import { ChecklistEditModalComponent } from './components/checklist-edit-modal/checklist-edit-modal.component';
import { NameAutocompleteComponent } from './components/name-autocomplete/name-autocomplete.component';

import { ApiService } from './services/api.service';
import { TankCalculationService } from './services/tank-calculation.service';
import { PrintJobPlannerService } from './services/print-job-planner.service';
import { FileDownloadService } from './services/file-download.service';
import { checklistTemplateLabel } from './core/checklist-template-label.util';

import {
  Driver,
  Truck,
  ProductDefinition,
  TankSlot,
  PrintJob,
  GenerateChecklistRequest,
  GeneratedChecklist,
  AssistType,
} from './models';

/**
 * Huvudkomponent som orkestrerar de fyra stegen (chaufför, fordon, produkter,
 * tankförhandsgranskning) och äger all delad state. Själva affärslogiken
 * (tankberäkning, gruppering av checklistor, API-anrop) ligger i egna
 * injicerbara tjänster för att hålla komponenten tunn och testbar.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DriverSelectComponent,
    VehicleSelectComponent,
    ProductSelectComponent,
    TankPreviewComponent,
    ChecklistEditModalComponent,
    NameAutocompleteComponent,
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit {
  operatorName = 'Vakt';
  akeri = '';

  operators: string[] = [];
  drivers: Driver[] = [];
  trucks: Truck[] = [];
  products: ProductDefinition[] = [];

  selectedDriverName = '';
  driverAdrExpiry = '';
  isNewDriver = false;

  selectedTruckReg = '';
  isNewTruck = false;
  truckTankCode = 'ADR';

  selectedTrailer1Reg = '';
  selectedTrailer2Reg = '';
  isNewTrailer1 = false;
  isNewTrailer2 = false;

  isGenerating = false;
  errorMessage: string | null = null;

  /** Checklistor som genererats i den här sessionen, med Visa/Redigera + Skriv ut. */
  generatedChecklists: GeneratedChecklist[] = [];

  /** Vilken genererad checklista som redigeras i modalen just nu (null = stängd). */
  editingChecklist: GeneratedChecklist | null = null;
  isSavingEdit = false;

  readonly checklistTemplateLabel = checklistTemplateLabel;

  constructor(
    private readonly api: ApiService,
    private readonly tankCalculation: TankCalculationService,
    private readonly printJobPlanner: PrintJobPlannerService,
    private readonly fileDownload: FileDownloadService,
  ) {}

  ngOnInit(): void {
    this.api.getInitData().subscribe({
      next: (data) => {
        this.operatorName = data.defaultOperator;
        this.operators = data.operators;
        this.drivers = data.drivers;
        this.trucks = data.trucks;
        this.products = data.products;
      },
      error: () => {
        this.errorMessage = 'Kunde inte hämta masterdata från servern. Är backend igång?';
      },
    });
  }

  onIsNewDriverChange(isNew: boolean): void {
    this.isNewDriver = isNew;
    if (!isNew) {
      this.akeri = 'PH Tank';
    } else if (this.akeri === 'PH Tank') {
      this.akeri = '';
    }

    // Förvälj första kända bilen om chauffören är känd och ingen bil valts än.
    if (!isNew && this.trucks.length > 0 && !this.selectedTruckReg) {
      this.selectedTruckReg = this.trucks[0].regNr;
      this.truckTankCode = this.trucks[0].tankCode;
      this.isNewTruck = false;
    }
  }

  calculateTankSlots(): TankSlot[] {
    return this.tankCalculation.calculateTankSlots(
      this.selectedTruckReg,
      this.truckTankCode,
      this.currentTrailers,
      this.selectedTrailer1Reg,
      this.selectedTrailer2Reg,
    );
  }

  getPrintJobs(): PrintJob[] {
    return this.printJobPlanner.getPrintJobs(this.products);
  }

  /** Bygger begäran för en given grupp av produkter utifrån aktuell formulärstate. */
  private buildRequest(job: PrintJob, tankSlots: TankSlot[]): GenerateChecklistRequest {
    const trailers = [
      ...(this.selectedTrailer1Reg ? [{ regNr: this.selectedTrailer1Reg, isNew: this.isNewTrailer1 }] : []),
      ...(this.selectedTrailer2Reg ? [{ regNr: this.selectedTrailer2Reg, isNew: this.isNewTrailer2 }] : []),
    ];

    return {
      templateType: job.template,
      operatorName: this.operatorName,
      driverName: this.selectedDriverName,
      driverAdrExpiry: this.driverAdrExpiry,
      isNewDriver: this.isNewDriver,
      akeri: this.akeri,
      truck: { regNr: this.selectedTruckReg, isNew: this.isNewTruck },
      trailers,
      tankSlots: tankSlots.map((s) => ({
        tankCode: s.tankCode,
        inspectionType: s.inspectionType,
        lastInspectionMonthYear: s.lastInspectionMonthYear,
        expiryFormatted: s.expiryFormatted,
        isExpired: s.isExpired,
      })),
      selectedProducts: job.products.map((p) => ({ name: p.displayName, unNumber: p.unNumber, family: p.family })),
      // Kända/förkonfigurerade chaufförer i masterdatan är redan godkända
      // självlastare - kryssa i "Själv lastn" automatiskt för dem. För en ny/
      // manuellt inmatad chaufför vet vi inget om detta, så inget kryssas i.
      assistType: this.isNewDriver ? AssistType.Unspecified : AssistType.SelfLoading,
    };
  }

  private buildFileName(request: GenerateChecklistRequest, station: string): string {
    return `Checklista_${station}_${request.truck.regNr}.xlsx`;
  }

  /** Genererar en checklista per (mall, station) och lägger dem i listan nedan - laddar inte ner automatiskt. */
  generateChecklists(): void {
    this.errorMessage = null;
    const printJobs = this.getPrintJobs();
    if (printJobs.length === 0) {
      this.errorMessage = 'Välj minst en produkt att lasta!';
      return;
    }

    const tankSlots = this.calculateTankSlots();
    this.isGenerating = true;
    let pending = printJobs.length;

    // Räknas ner vid både success (next) och fel (error) - RxJS anropar
    // aldrig `complete` efter ett `error`, så den logiken kan inte ligga där
    // utan att isGenerating fastnar på true om en enda checklista misslyckas.
    const onSettled = () => {
      pending -= 1;
      if (pending === 0) {
        this.isGenerating = false;
      }
    };

    printJobs.forEach((job) => {
      const request = this.buildRequest(job, tankSlots);
      const key = `${job.template}_${job.station}`;

      this.api.generateChecklist(request).subscribe({
        next: (blob) => {
          const entry: GeneratedChecklist = {
            key,
            printJob: job,
            request,
            blob,
            fileName: this.buildFileName(request, job.station),
            wasEdited: false,
          };
          const existingIndex = this.generatedChecklists.findIndex((c) => c.key === key);
          if (existingIndex >= 0) {
            this.generatedChecklists[existingIndex] = entry;
          } else {
            this.generatedChecklists.push(entry);
          }
          onSettled();
        },
        error: () => {
          this.errorMessage = `Kunde inte generera checklista för station ${job.station}.`;
          onSettled();
        },
      });
    });
  }

  /** Öppnar redigeringsmodalen (ingen ny flik) för en redan genererad checklista. */
  openEdit(entry: GeneratedChecklist): void {
    this.editingChecklist = entry;
  }

  closeEdit(): void {
    this.editingChecklist = null;
  }

  /** Skickar det redigerade formuläret till backend igen och ersätter den lagrade filen. */
  onEditSave(editedRequest: GenerateChecklistRequest): void {
    const entry = this.editingChecklist;
    if (!entry) {
      return;
    }

    this.isSavingEdit = true;
    this.api.generateChecklist(editedRequest).subscribe({
      next: (blob) => {
        entry.request = editedRequest;
        entry.blob = blob;
        entry.fileName = this.buildFileName(editedRequest, entry.printJob.station);
        entry.wasEdited = true;
        this.isSavingEdit = false;
        this.editingChecklist = null;
      },
      error: () => {
        this.errorMessage = `Kunde inte uppdatera checklistan för station ${entry.printJob.station}.`;
        this.isSavingEdit = false;
      },
    });
  }

  /** Laddar ner den (eventuellt redigerade) Excel-filen så operatören kan öppna och skriva ut den. */
  printChecklist(entry: GeneratedChecklist): void {
    this.fileDownload.download(entry.blob, entry.fileName);
  }

  private get currentTrailers() {
    const truck = this.trucks.find((t) => t.regNr === this.selectedTruckReg);
    return truck ? truck.trailers : [];
  }
}
