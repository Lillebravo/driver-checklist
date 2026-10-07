import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ProductDefinition } from '../../models';

/** Steg 3: Val av produkter som ska lastas under transporten. */
@Component({
  selector: 'app-product-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './product-select.component.html',
  styleUrl: './product-select.component.css',
})
export class ProductSelectComponent {
  @Input() products: ProductDefinition[] = [];
  @Output() selectionChange = new EventEmitter<void>();

  onToggle(): void {
    this.selectionChange.emit();
  }
}
