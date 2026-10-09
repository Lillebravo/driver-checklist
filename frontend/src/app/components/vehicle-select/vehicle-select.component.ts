import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Trailer, Truck } from '../../models';
import { allTrailers, normalizeRegNr } from '../../core/vehicle-registry.util';

/**
 * Steg 2: Fordonsekipage. Dragbil och upp till två släpvagnar kan väljas
 * från listan över kända fordon eller skrivas in manuellt, vilket flaggar
 * dem som "Ny Dragbil" / "Nytt Släp 1" / "Nytt Släp 2" enligt README.md avsnitt 1.
 */
@Component({
  selector: 'app-vehicle-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './vehicle-select.component.html',
  styleUrl: './vehicle-select.component.css',
})
export class VehicleSelectComponent {
  @Input() trucks: Truck[] = [];
  @Input() registeredTrailers: Trailer[] = [];

  @Input() truckRegNr = '';
  @Output() truckRegNrChange = new EventEmitter<string>();

  @Input() truckTankCode = '';
  @Output() truckTankCodeChange = new EventEmitter<string>();

  @Input() isNewTruck = false;
  @Output() isNewTruckChange = new EventEmitter<boolean>();

  @Input() trailer1Reg = '';
  @Output() trailer1RegChange = new EventEmitter<string>();
  @Input() isNewTrailer1 = false;
  @Output() isNewTrailer1Change = new EventEmitter<boolean>();

  @Input() trailer2Reg = '';
  @Output() trailer2RegChange = new EventEmitter<string>();
  @Input() isNewTrailer2 = false;
  @Output() isNewTrailer2Change = new EventEmitter<boolean>();

  @Input() truckIsTankContainer = false;
  @Output() truckIsTankContainerChange = new EventEmitter<boolean>();
  @Input() truckContainerNumber = '';
  @Output() truckContainerNumberChange = new EventEmitter<string>();
  @Input() trailer1IsTankContainer = false;
  @Output() trailer1IsTankContainerChange = new EventEmitter<boolean>();
  @Input() trailer1ContainerNumber = '';
  @Output() trailer1ContainerNumberChange = new EventEmitter<string>();
  @Input() trailer2IsTankContainer = false;
  @Output() trailer2IsTankContainerChange = new EventEmitter<boolean>();
  @Input() trailer2ContainerNumber = '';
  @Output() trailer2ContainerNumberChange = new EventEmitter<string>();

  get availableTrailers(): Trailer[] {
    const truck = this.trucks.find((t) => normalizeRegNr(t.regNr) === normalizeRegNr(this.truckRegNr));
    return truck ? truck.trailers : [];
  }

  activeTrailer: 1 | 2 | null = null;
  activeSuggestion = -1;

  get trailerGroups(): { label: string; trailers: Trailer[] }[] {
    const query = normalizeRegNr(this.activeTrailer === 1 ? this.trailer1Reg : this.trailer2Reg);
    const usual = this.availableTrailers;
    const usualKeys = new Set(usual.map(t => normalizeRegNr(t.regNr)));
    const filter = (items: Trailer[]) => items.filter(t => normalizeRegNr(t.regNr).includes(query));
    return [
      { label: 'Vanliga släp för bilen', trailers: filter(usual) },
      { label: 'Övriga registrerade släp', trailers: filter(allTrailers(this.trucks, this.registeredTrailers).filter(t => !usualKeys.has(normalizeRegNr(t.regNr)))) },
    ];
  }

  selectTrailer(slot: 1 | 2, value: string): void {
    if (slot === 1) this.onTrailer1Input(value);
    else this.onTrailer2Input(value);
    this.activeTrailer = null;
  }

  onTrailerKey(event: KeyboardEvent, slot: 1 | 2): void {
    const options = this.trailerGroups.flatMap(g => g.trailers);
    if (event.key === 'Escape') this.activeTrailer = null;
    else if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeTrailer = slot;
      this.activeSuggestion = Math.max(0, Math.min(options.length - 1,
        this.activeSuggestion + (event.key === 'ArrowDown' ? 1 : -1)));
    } else if (event.key === 'Enter' && this.activeTrailer === slot && options[this.activeSuggestion]) {
      event.preventDefault();
      this.selectTrailer(slot, options[this.activeSuggestion].regNr);
    }
  }

  isActiveSuggestion(regNr: string): boolean {
    return this.trailerGroups.flatMap(g => g.trailers)[this.activeSuggestion]?.regNr === regNr;
  }

  onTruckInput(value: string): void {
    this.truckRegNr = value;
    this.truckRegNrChange.emit(value);

    const match = this.trucks.find((t) => normalizeRegNr(t.regNr) === normalizeRegNr(value));
    if (match) {
      this.truckTankCodeChange.emit(match.tankCode);
      this.isNewTruckChange.emit(false);
    } else {
      this.truckTankCodeChange.emit('');
      this.isNewTruckChange.emit(!!value.trim());
    }
  }

  onTrailer1Input(value: string): void {
    const changed = normalizeRegNr(this.trailer1Reg) !== normalizeRegNr(value);
    this.trailer1Reg = value;
    this.activeSuggestion = -1;
    this.trailer1RegChange.emit(value);
    const isNew = !!value.trim() && !allTrailers(this.trucks, this.registeredTrailers).some((t) => normalizeRegNr(t.regNr) === normalizeRegNr(value));
    this.isNewTrailer1Change.emit(isNew);
    if (changed) this.applyTrailerContainer(1, value);
  }

  onTrailer2Input(value: string): void {
    const changed = normalizeRegNr(this.trailer2Reg) !== normalizeRegNr(value);
    this.trailer2Reg = value;
    this.activeSuggestion = -1;
    this.trailer2RegChange.emit(value);
    const isNew = !!value.trim() && !allTrailers(this.trucks, this.registeredTrailers).some((t) => normalizeRegNr(t.regNr) === normalizeRegNr(value));
    this.isNewTrailer2Change.emit(isNew);
    if (changed) this.applyTrailerContainer(2, value);
  }

  private applyTrailerContainer(slot: 1 | 2, regNr: string): void {
    const trailer = allTrailers(this.trucks, this.registeredTrailers).find(t => normalizeRegNr(t.regNr) === normalizeRegNr(regNr));
    const number = trailer?.containerNumber ?? '';
    if (slot === 1) {
      this.trailer1IsTankContainer = !!number;
      this.trailer1ContainerNumber = number;
      this.trailer1IsTankContainerChange.emit(!!number);
      this.trailer1ContainerNumberChange.emit(number);
    } else {
      this.trailer2IsTankContainer = !!number;
      this.trailer2ContainerNumber = number;
      this.trailer2IsTankContainerChange.emit(!!number);
      this.trailer2ContainerNumberChange.emit(number);
    }
  }
}
