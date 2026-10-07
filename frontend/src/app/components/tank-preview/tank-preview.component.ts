import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TankSlot } from '../../models';

/** Förhandsgranskning av de beräknade tankplatserna (Tank 1-4) med inspektionsstatus. */
@Component({
  selector: 'app-tank-preview',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './tank-preview.component.html',
  styleUrl: './tank-preview.component.css',
})
export class TankPreviewComponent {
  @Input() tankSlots: TankSlot[] = [];
}
