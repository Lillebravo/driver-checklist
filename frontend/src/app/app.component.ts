import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';

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
import { allTrailers, normalizeRegNr } from './core/vehicle-registry.util';

import {
  Driver,
  Truck,
  Trailer,
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
  registeredTrailers: Trailer[] = [];
  products: ProductDefinition[] = [];
  vehicleRegistrySource: string | null = null;
  importWarnings: string[] = [];

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
  truckIsTankContainer = false;
  truckContainerNumber = '';
  trailer1IsTankContainer = false;
  trailer1ContainerNumber = '';
  trailer2IsTankContainer = false;
  trailer2ContainerNumber = '';
  private readonly containerTimestamp = new Date().toISOString();

  isGenerating = false;
  errorMessage: string | null = null;

  /** Checklistor som genererats i den här sessionen, med Visa/Redigera + Skriv ut. */
  generatedChecklists: GeneratedChecklist[] = [];

  /** Vilken genererad checklista som redigeras i modalen just nu (null = stängd). */
  editingChecklist: GeneratedChecklist | null = null;
  editingJob: PrintJob | null = null;
  editingRequest: GenerateChecklistRequest | null = null;
  drafts = new Map<string, { request: GenerateChecklistRequest; baseline: string; containerNumber: string }>();
  isSavingEdit = false;

  readonly checklistTemplateLabel = checklistTemplateLabel;
  readonly trackPrintJob = (_index: number, job: PrintJob): string => `${job.template}_${job.station}`;

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
        this.registeredTrailers = data.trailers ?? [];
        this.products = data.products;
        this.vehicleRegistrySource = data.vehicleRegistrySource ?? null;
        this.importWarnings = data.importWarnings ?? [];
      },
      error: (error: HttpErrorResponse) => {
        const detail: unknown = error.error?.detail;
        this.errorMessage = typeof detail === 'string'
          ? `Kunde inte hämta masterdata. ${detail}`
          : error.status >= 500
          ? 'Servern kunde inte läsa masterdata. Kontrollera felmeddelandet i programmets konsolfönster.'
          : 'Kunde inte hämta masterdata från servern. Är backend igång?';
      },
    });
  }

  onIsNewDriverChange(isNew: boolean): void {
    this.isNewDriver = isNew;
    if (this.vehicleRegistrySource) {
      const driver = this.drivers.find(d => d.name.toLowerCase() === this.selectedDriverName.toLowerCase());
      if (!isNew && driver) {
        this.akeri = driver.haulier ?? '';
        const truck = this.trucks.find(t => driver.truckRegNrs?.some(reg => normalizeRegNr(reg) === normalizeRegNr(t.regNr)));
        if (truck && !this.selectedTruckReg) {
          this.selectedTruckReg = truck.regNr;
          this.truckTankCode = truck.tankCode;
          this.isNewTruck = false;
        }
      } else {
        this.akeri = '';
      }
      return;
    }
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
    if (this.vehicleRegistrySource) {
      const truck = this.trucks.find(t => normalizeRegNr(t.regNr) === normalizeRegNr(this.selectedTruckReg));
      return this.tankCalculation.registryTankSlots(
        this.selectedTruckReg, truck?.tankCode ?? (this.isNewTruck ? this.truckTankCode : ''),
        this.currentTrailers, this.selectedTrailer1Reg, this.selectedTrailer2Reg,
        this.trailer1IsTankContainer, this.trailer2IsTankContainer,
      );
    }
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
    const knownTrailers = allTrailers(this.trucks, this.registeredTrailers);
    const trailerUnit = (regNr: string) => {
      const match = knownTrailers.find(t => normalizeRegNr(t.regNr) === normalizeRegNr(regNr));
      return { regNr, isNew: !match, approvalExpiry: match?.approvalExpiry };
    };
    const truck = this.trucks.find(t => normalizeRegNr(t.regNr) === normalizeRegNr(this.selectedTruckReg));
    const trailers = [
      ...(this.selectedTrailer1Reg ? [trailerUnit(this.selectedTrailer1Reg)] : []),
      ...(this.selectedTrailer2Reg ? [trailerUnit(this.selectedTrailer2Reg)] : []),
    ];

    return {
      templateType: job.template,
      operatorName: this.operatorName,
      driverName: this.selectedDriverName,
      driverAdrExpiry: this.driverAdrExpiry,
      isNewDriver: this.isNewDriver,
      akeri: this.akeri,
      truck: { regNr: this.selectedTruckReg, isNew: !!this.selectedTruckReg && !truck, approvalExpiry: truck?.approvalExpiry },
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
      assistType: this.isNewDriver || this.vehicleRegistrySource ? AssistType.Unspecified : AssistType.SelfLoading,
      ...(this.currentContainerNumber ? {
        firstPage: {
          timestamp: this.containerTimestamp,
          sapNumber: '', loadingAmount: '', containerNumber: this.currentContainerNumber,
          unNumbers: [...new Set(job.products.map(p => p.unNumber))],
          rows: [], roles: [], compartmentVolumes: [],
        },
      } : {}),
    };
  }

  private buildFileName(request: GenerateChecklistRequest, station: string): string {
    return `Checklista_${station}_${request.truck.regNr}.xlsx`;
  }

  /** Genererar en checklista per (mall, station) och lägger dem i listan nedan - laddar inte ner automatiskt. */
  generateChecklists(): void {
    this.errorMessage = null;
    if (this.vehicleRegistrySource && (!this.selectedDriverName.trim() || !this.driverAdrExpiry)) {
      this.errorMessage = 'Välj chaufför och kontrollera/fyll i ADR-datum före generering.';
      return;
    }
    const printJobs = this.getPrintJobs();
    if (printJobs.length === 0) {
      this.errorMessage = 'Välj minst en produkt att lasta!';
      return;
    }

    const tankSlots = this.calculateTankSlots();
    if (printJobs.some(job => {
      const draft = this.drafts.get(`${job.template}_${job.station}`);
      return draft && draft.baseline !== JSON.stringify(this.buildRequest(job, tankSlots));
    })) {
      this.errorMessage = 'Underlaget har ändrats efter redigeringen. Öppna och spara utkastet igen innan generering.';
      return;
    }
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
      const key = `${job.template}_${job.station}`;
      const request = this.drafts.get(key)?.request ?? this.buildRequest(job, tankSlots);

      this.api.generateChecklist(request).subscribe({
        next: (blob) => {
          const entry: GeneratedChecklist = {
            key,
            printJob: job,
            request,
            blob,
            fileName: this.buildFileName(request, job.station),
            wasEdited: this.drafts.has(key),
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
    this.errorMessage = null;
    this.editingChecklist = entry;
    this.editingJob = null;
    this.editingRequest = entry.request;
  }

  openDraft(job: PrintJob): void {
    this.errorMessage = null;
    const request = this.buildRequest(job, this.calculateTankSlots());
    const draft = this.drafts.get(`${job.template}_${job.station}`);
    this.editingChecklist = null;
    this.editingJob = job;
    this.editingRequest = draft?.baseline === JSON.stringify(request) ? draft.request : {
      ...request,
      firstPage: draft?.request.firstPage ? {
        ...draft.request.firstPage,
        containerNumber: draft.containerNumber !== this.currentContainerNumber
          ? this.currentContainerNumber : draft.request.firstPage.containerNumber,
        rows: draft.containerNumber !== this.currentContainerNumber &&
          draft.request.firstPage.containerNumber.trim() && !this.currentContainerNumber
          ? draft.request.firstPage.rows.map(row => ({ ...row, tt: row.tt || row.tc, tc: false }))
          : draft.request.firstPage.rows,
      } : request.firstPage,
    };
    if (draft && draft.baseline !== JSON.stringify(request)) {
      this.errorMessage = 'Utkastets transportuppgifter har uppdaterats från underlaget. Kontrollkryss och kommentarer är bevarade.';
    }
  }

  closeEdit(): void {
    if (this.isSavingEdit) return;
    this.editingChecklist = null;
    this.editingJob = null;
    this.editingRequest = null;
  }

  /** Skickar det redigerade formuläret till backend igen och ersätter den lagrade filen. */
  onEditSave(editedRequest: GenerateChecklistRequest): void {
    const matchingProducts = this.products.filter(p => editedRequest.selectedProducts.some(s =>
      s.name === p.displayName && s.family === p.family));
    const jobs = this.printJobPlanner.getPrintJobs(matchingProducts.map(p => ({ ...p, selected: true })));
    if (jobs.length !== 1) {
      this.errorMessage = 'En checklista måste innehålla produkter från samma station och mall.';
      return;
    }
    const updatedJob = jobs[0];
    if (this.editingJob) {
      const oldJob = this.editingJob;
      for (const product of this.products) {
        if (oldJob.products.includes(product)) product.selected = false;
        if (matchingProducts.includes(product)) product.selected = true;
      }
      this.drafts.delete(`${oldJob.template}_${oldJob.station}`);
      const key = `${updatedJob.template}_${updatedJob.station}`;
      const job = this.getPrintJobs().find(j => `${j.template}_${j.station}` === key)!;
      editedRequest.selectedProducts = job.products.map(p => ({ name: p.displayName, family: p.family, unNumber: p.unNumber }));
      this.drafts.set(key, {
        request: editedRequest,
        baseline: JSON.stringify(this.buildRequest(job, this.calculateTankSlots())),
        containerNumber: this.currentContainerNumber,
      });
      this.closeEdit();
      this.errorMessage = null;
      return;
    }
    const entry = this.editingChecklist;
    if (!entry) {
      return;
    }

    this.isSavingEdit = true;
    this.api.generateChecklist(editedRequest).subscribe({
      next: (blob) => {
        entry.request = editedRequest;
        entry.printJob = updatedJob;
        entry.key = `${updatedJob.template}_${updatedJob.station}`;
        entry.blob = blob;
        entry.fileName = this.buildFileName(editedRequest, entry.printJob.station);
        entry.wasEdited = true;
        this.isSavingEdit = false;
        this.closeEdit();
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
    return allTrailers(this.trucks, this.registeredTrailers);
  }

  private get currentContainerNumber(): string {
    return [
      this.truckIsTankContainer ? this.truckContainerNumber.trim() : '',
      this.trailer1IsTankContainer ? this.trailer1ContainerNumber.trim() : '',
      this.trailer2IsTankContainer ? this.trailer2ContainerNumber.trim() : '',
    ].filter(Boolean).join(' / ');
  }
}
