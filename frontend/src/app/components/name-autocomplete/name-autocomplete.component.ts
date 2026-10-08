import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

/**
 * Återanvändbart namnfält med sök/autocomplete (HTML5 `datalist`).
 * Används både för chaufförssöket (steg 1, se README.md avsnitt 1) och för
 * Operatör/Vakt-fältet i toppraden, så att båda har exakt samma
 * sök-/skriv-beteende.
 */
@Component({
  selector: 'app-name-autocomplete',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './name-autocomplete.component.html',
  styleUrl: './name-autocomplete.component.css',
})
export class NameAutocompleteComponent {
  @Input() inputId = 'name-autocomplete';
  @Input() label = 'Namn';
  @Input() placeholder = '';
  @Input() options: string[] = [];
  @Input() value = '';

  @Output() valueChange = new EventEmitter<string>();

  onInput(value: string): void {
    this.value = value;
    this.valueChange.emit(value);
  }
}
