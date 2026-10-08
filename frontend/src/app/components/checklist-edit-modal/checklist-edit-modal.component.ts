import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GenerateChecklistRequest, AssistType } from '../../models';
import { assistTypeLabel } from '../../core/assist-type-label.util';

/**
 * Modal (inget nytt webbläsarfönster/ny flik) för att rätta till enstaka
 * fält på en redan genererad checklista - t.ex. om chaufförens namn stavats
 * fel eller en tankkod är felaktig. Formuläret speglar exakt samma fält som
 * fylldes i vid den ursprungliga genereringen (se README.md avsnitt 4),
 * och vid "Spara & uppdatera" skickas hela begäran igenom samma
 * `/api/checklist/generate`-endpoint igen, vilket garanterar att den
 * uppdaterade Excel-filen fylls i exakt likadant som vid första
 * genereringen.
 *
 * Produkterna som ingår i checklistan redigeras inte här - de styrs av
 * produktvalet i steg 3 och grupperingen i `PrintJobPlannerService`, och
 * visas därför read-only.
 */
@Component({
  selector: 'app-checklist-edit-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './checklist-edit-modal.component.html',
  styleUrl: './checklist-edit-modal.component.css',
})
export class ChecklistEditModalComponent {
  @Input() isOpen = false;
  @Input() isSaving = false;
  @Input() operators: string[] = [];

  @Input() set request(value: GenerateChecklistRequest | null) {
    if (value) {
      // Djup kopia så att ändringar i formuläret inte muterar den
      // ursprungliga, redan genererade checklistans data förrän man sparar.
      this.model = JSON.parse(JSON.stringify(value)) as GenerateChecklistRequest;
    }
  }

  @Output() save = new EventEmitter<GenerateChecklistRequest>();
  @Output() close = new EventEmitter<void>();

  model: GenerateChecklistRequest | null = null;

  readonly assistTypeLabel = assistTypeLabel;
  readonly assistTypeOptions = [
    AssistType.Unspecified,
    AssistType.FullAssist,
    AssistType.HalfAssist,
    AssistType.SelfLoading,
  ];

  onSave(): void {
    if (this.model) {
      this.save.emit(this.model);
    }
  }

  onClose(): void {
    this.close.emit();
  }
}
