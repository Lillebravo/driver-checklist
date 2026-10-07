import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Trailer, Truck } from '../../models';

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

  get availableTrailers(): Trailer[] {
    const truck = this.trucks.find((t) => t.regNr === this.truckRegNr);
    return truck ? truck.trailers : [];
  }

  onTruckInput(value: string): void {
    this.truckRegNr = value;
    this.truckRegNrChange.emit(value);

    const match = this.trucks.find((t) => t.regNr.toUpperCase() === value.toUpperCase());
    if (match) {
      this.truckTankCodeChange.emit(match.tankCode);
      this.isNewTruckChange.emit(false);
    } else {
      this.isNewTruckChange.emit(true);
    }
  }

  onTrailer1Input(value: string): void {
    this.trailer1RegChange.emit(value);
    const isNew = !!value && !this.availableTrailers.some((t) => t.regNr.toUpperCase() === value.toUpperCase());
    this.isNewTrailer1Change.emit(isNew);
  }

  onTrailer2Input(value: string): void {
    this.trailer2RegChange.emit(value);
    const isNew = !!value && !this.availableTrailers.some((t) => t.regNr.toUpperCase() === value.toUpperCase());
    this.isNewTrailer2Change.emit(isNew);
  }
}
