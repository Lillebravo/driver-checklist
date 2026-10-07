import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Driver } from '../../models';

/**
 * Steg 1: Sök/välj chaufför. Visar ADR-utgångsdatum direkt och flaggar
 * okända chaufförer som "Ny Chaufför" enligt README.md avsnitt 1.
 */
@Component({
  selector: 'app-driver-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './driver-select.component.html',
  styleUrl: './driver-select.component.css',
})
export class DriverSelectComponent {
  @Input() drivers: Driver[] = [];

  @Input() driverName = '';
  @Output() driverNameChange = new EventEmitter<string>();

  @Input() adrExpiry = '';
  @Output() adrExpiryChange = new EventEmitter<string>();

  @Input() isNewDriver = false;
  @Output() isNewDriverChange = new EventEmitter<boolean>();

  onNameInput(value: string): void {
    this.driverName = value;
    this.driverNameChange.emit(value);

    const match = this.drivers.find((d) => d.name.toLowerCase() === value.toLowerCase());
    if (match) {
      this.adrExpiry = match.adrExpiry;
      this.adrExpiryChange.emit(match.adrExpiry);
      this.isNewDriverChange.emit(false);
    } else {
      this.isNewDriverChange.emit(true);
    }
  }

  onAdrExpiryInput(value: string): void {
    this.adrExpiryChange.emit(value);
  }
}
