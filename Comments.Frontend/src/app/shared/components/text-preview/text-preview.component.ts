import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideX } from '@ng-icons/lucide';

@Component({
  selector: 'app-text-preview',
  standalone: true,
  imports: [NgIcon],
  providers: [provideIcons({ lucideX })],
  templateUrl: './text-preview.component.html',
})
export class TextPreviewComponent {
  @Input() text: { name: string; content: string } | null = null;
  @Output() readonly closed = new EventEmitter<void>();
}
