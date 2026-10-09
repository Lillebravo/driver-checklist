import { Component, EventEmitter, Input, Output, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GenerateChecklistRequest, AssistType, ChecklistPage, ChecklistPageResponse, ProductDefinition, Truck, VehicleUnitRequest } from '../../models';
import { assistTypeLabel } from '../../core/assist-type-label.util';
import { ApiService } from '../../services/api.service';
import { allTrailers, normalizeRegNr } from '../../core/vehicle-registry.util';
import { Subscription } from 'rxjs';
import { TankCalculationService } from '../../services/tank-calculation.service';

@Component({
  selector: 'app-checklist-edit-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './checklist-edit-modal.component.html',
  styleUrl: './checklist-edit-modal.component.css',
})
export class ChecklistEditModalComponent implements OnDestroy {
  @Input() isOpen = false;
  @Input() isSaving = false;
  @Input() saveError: string | null = null;
  @Input() operators: string[] = [];
  @Input() products: ProductDefinition[] = [];
  @Input() trucks: Truck[] = [];
  @Input() isDraft = false;

  @Input() set request(value: GenerateChecklistRequest | null) {
    if (value) {
      // Djup kopia så att ändringar i formuläret inte muterar den
      // ursprungliga, redan genererade checklistans data förrän man sparar.
      this.model = structuredClone(value);
      this.hasContainer = false;
      this.vehicleKey = this.currentVehicleKey();
      this.productToAdd = '';
      this.page = this.model.firstPage ?? {
        timestamp: new Date().toISOString(),
        sapNumber: '', loadingAmount: '', containerNumber: '',
        unNumbers: [...new Set(value.selectedProducts.map(p => p.unNumber))],
        rows: [], roles: [], compartmentVolumes: Array(6).fill(''),
      };
      this.timestampLocal = this.localDateTime(this.page.timestamp);
      this.padTanks();
      this.error = '';
      this.loadDefinition();
    } else {
      this.definitionSubscription?.unsubscribe();
      this.model = null;
      this.page = null;
      this.definition = null;
    }
  }

  @Output() save = new EventEmitter<GenerateChecklistRequest>();
  @Output() close = new EventEmitter<void>();

  model: GenerateChecklistRequest | null = null;
  page: ChecklistPage | null = null;
  definition: ChecklistPageResponse | null = null;
  loading = false;
  error = '';
  productToAdd = '';
  timestampLocal = '';
  private definitionSubscription?: Subscription;
  private vehicleKey = '';
  private hasContainer = false;

  constructor(private readonly api: ApiService, private readonly tanks: TankCalculationService) {}

  ngOnDestroy(): void { this.definitionSubscription?.unsubscribe(); }

  private localDateTime(timestamp: string): string {
    const date = new Date(timestamp);
    return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
  }

  loadDefinition(): void {
    if (!this.model || !this.page) return;
    this.definitionSubscription?.unsubscribe();
    this.loading = true;
    this.definition = null;
    this.definitionSubscription = this.api.getFirstPage(this.model.templateType).subscribe({
      next: definition => {
        const page = this.page!;
        this.definition = definition;
        if (this.model && !this.availableAssist(this.model.assistType)) {
          this.model.assistType = AssistType.Unspecified;
          page.roles = [];
        }
        page.rows = definition.rows.map(row => {
          const previous = page.rows.find(r => r.row === row.row);
          return {
            row: row.row,
            tt: row.enabled[0] && (previous?.tt ?? (!page.containerNumber.trim() && row.row >= 14 && row.row <= 19)),
            tc: row.enabled[1] && (previous?.tc ?? (!!page.containerNumber.trim() && row.row >= 14 && row.row <= 19)),
            rc: row.enabled[2] && (previous?.rc ?? false),
            comment: previous?.comment ?? '',
          };
        });
        this.changeContainerNumber(page.containerNumber);
        page.roles = definition.sections.map(section => {
          const previous = page.roles.find(r => r.row === section.row);
          return { row: section.row, selected: previous
            ? previous.selected.filter(r => section.roles.includes(r) && this.roleAllowed(r))
            : this.defaultRoles(section.row, section.roles) };
        });
        page.unNumbers = page.unNumbers.filter(n => definition.unNumbers.includes(n));
        this.loading = false;
      },
      error: () => {
        this.error = 'Kunde inte läsa checklistemallen. Kontrollera att backend och mallfilen finns. Ingen redigering har sparats.';
        this.loading = false;
      },
    });
  }

  changeContainerNumber(value: string): void {
    if (!this.page) return;
    this.page.containerNumber = value;
    const hasContainer = !!value.trim();
    if (hasContainer !== this.hasContainer && this.definition) {
      for (const row of this.page.rows) {
        const enabled = this.definition.rows.find(r => r.row === row.row)?.enabled;
        if (hasContainer && row.tt && enabled?.[1]) {
          row.tt = false;
          row.tc = true;
        } else if (!hasContainer && row.tc && enabled?.[0]) {
          row.tc = false;
          row.tt = true;
        }
      }
    }
    this.hasContainer = hasContainer;
  }

  private defaultRoles(row: number, roles: string[]): string[] {
    if (row === 13) return roles.filter(r => r === 'Vakt');
    const assist = this.model?.assistType;
    return roles.filter(r =>
      assist === AssistType.FullAssist ? r.includes('Full assist') :
      assist === AssistType.HalfAssist ? r.includes('Halv assist') :
      assist === AssistType.SelfLoading && !this.model?.isNewDriver ? r.includes('Själv lastn') : false);
  }

  get sections() {
    return this.definition?.sections.map((section, index, sections) => ({
      ...section,
      selected: this.page?.roles.find(r => r.row === section.row)?.selected ?? [],
      rows: this.definition!.rows.filter(r => r.row > section.row && r.row < (sections[index + 1]?.row ?? 31))
        .map(r => ({ ...r, value: this.page!.rows.find(v => v.row === r.row)! })),
    })) ?? [];
  }

  toggle(values: string[], value: string, selected: boolean): void {
    if (selected && !values.includes(value)) values.push(value);
    else if (!selected) {
      const index = values.indexOf(value);
      if (index >= 0) values.splice(index, 1);
    }
  }

  changeAssist(value: AssistType): void {
    if (!this.model || !this.page || !this.definition) return;
    this.model.assistType = value;
    for (const section of this.definition.sections.filter(s => s.row !== 13)) {
      this.page.roles.find(r => r.row === section.row)!.selected = this.defaultRoles(section.row, section.roles);
    }
  }

  changeDriverStatus(): void {
    if (this.model?.isNewDriver && this.model.assistType === AssistType.SelfLoading) {
      this.changeAssist(AssistType.Unspecified);
    }
    for (const role of this.page?.roles ?? []) {
      role.selected = role.selected.filter(label => this.roleAllowed(label));
    }
  }

  roleAllowed(role: string): boolean {
    return !this.model?.isNewDriver || !role.includes('Själv lastn') ||
      (role.includes('Halv assist') && this.model.assistType === AssistType.HalfAssist);
  }

  availableAssist(value: AssistType): boolean {
    if (value === AssistType.Unspecified) return true;
    if (value === AssistType.SelfLoading && this.model?.isNewDriver) return false;
    const marker = value === AssistType.FullAssist ? 'Full assist' : value === AssistType.HalfAssist ? 'Halv assist' : 'Själv lastn';
    return this.definition?.assistOptions.some(o => o.includes(marker)) ?? false;
  }

  private currentVehicleKey(): string {
    return [this.model?.truck.regNr ?? '', ...(this.model?.trailers.map(t => t.regNr) ?? [])].map(normalizeRegNr).join('|');
  }

  updateVehicleStatus(unit: VehicleUnitRequest, isTruck = false): void {
    if (!this.model) return;
    const truck = this.trucks.find(t => normalizeRegNr(t.regNr) === normalizeRegNr(this.model!.truck.regNr));
    const trailers = allTrailers(this.trucks);
    const known = isTruck ? truck : trailers.find(t => normalizeRegNr(t.regNr) === normalizeRegNr(unit.regNr));
    unit.isNew = !!unit.regNr.trim() && !known;
    unit.approvalExpiry = known?.approvalExpiry ?? '';
    this.rebuildTanks();
  }

  private rebuildTanks(): void {
    if (!this.model) return;
    const truck = this.trucks.find(t => normalizeRegNr(t.regNr) === normalizeRegNr(this.model!.truck.regNr));
    const key = this.currentVehicleKey();
    if (key !== this.vehicleKey) {
      this.vehicleKey = key;
      this.model.tankSlots = this.tanks.calculateTankSlots(this.model.truck.regNr, truck?.tankCode ?? '',
        allTrailers(this.trucks), this.model.trailers[0]?.regNr ?? '', this.model.trailers[1]?.regNr ?? '')
        .map(s => ({ ...s }));
      this.padTanks();
    }
  }

  private padTanks(): void {
    if (!this.model) return;
    while (this.model.tankSlots.length < 4) {
      this.model.tankSlots.push({ tankCode: '', inspectionType: '', lastInspectionMonthYear: '', expiryFormatted: '', isExpired: false });
    }
  }

  removeTrailer(index: number): void {
    if (!this.model) return;
    this.model.trailers.splice(index, 1);
    this.rebuildTanks();
  }

  addTrailer(): void {
    if (this.model && this.model.trailers.length < 2) this.model.trailers.push({ regNr: '', isNew: false, approvalExpiry: '' });
  }

  addProduct(code: string): void {
    const product = this.products.find(p => p.code === code);
    if (!product || !this.model || !this.page) return;
    const current = this.products.find(p => p.displayName === this.model!.selectedProducts[0]?.name);
    if (current && (current.template !== product.template || current.loadingStationId !== product.loadingStationId)) {
      this.model.selectedProducts = [];
    }
    if (!this.model.selectedProducts.some(p => p.name === product.displayName)) {
      this.model.selectedProducts.push({ name: product.displayName, family: product.family, unNumber: product.unNumber });
    }
    this.page.unNumbers = [...new Set(this.model.selectedProducts.map(p => p.unNumber))];
    const changedTemplate = this.model.templateType !== product.template;
    this.model.templateType = product.template;
    this.productToAdd = '';
    this.error = '';
    if (changedTemplate) this.loadDefinition();
    else this.page.unNumbers = this.page.unNumbers.filter(n => this.definition?.unNumbers.includes(n));
  }

  removeProduct(index: number): void {
    if (!this.model || !this.page) return;
    this.model.selectedProducts.splice(index, 1);
    this.page.unNumbers = [...new Set(this.model.selectedProducts.map(p => p.unNumber))]
      .filter(n => this.definition?.unNumbers.includes(n));
  }

  setInspection(index: number, type: string, checked: boolean): void {
    if (this.model) this.model.tankSlots[index].inspectionType = checked ? type : '';
  }

  readonly assistTypeLabel = assistTypeLabel;
  readonly trackIndex = (index: number): number => index;
  readonly trackRow = (_index: number, item: { row: number }): number => item.row;
  readonly assistTypeOptions = [
    AssistType.Unspecified,
    AssistType.FullAssist,
    AssistType.HalfAssist,
    AssistType.SelfLoading,
  ];

  onSave(): void {
    if (this.model && this.page && this.definition && !this.loading && !this.isSaving) {
      if (!this.model.driverName.trim() || !this.model.driverAdrExpiry || !this.model.truck.regNr.trim() ||
          !this.timestampLocal || this.model.selectedProducts.length === 0) {
        this.error = 'Fyll i datum/tid, chaufförens namn, ADR-giltighet, bilens registreringsnummer och minst en produkt.';
        return;
      }
      if (this.model.tankSlots.length > 4) {
        this.error = 'Checklistan har plats för högst fyra tankar. Ändra ekipaget innan du sparar.';
        return;
      }
      this.page.timestamp = new Date(this.timestampLocal).toISOString();
      this.model.firstPage = this.page;
      this.model.trailers = this.model.trailers.filter(t => t.regNr.trim());
      this.save.emit(this.model);
    }
  }

  onClose(): void {
    if (!this.isSaving) this.close.emit();
  }
}
