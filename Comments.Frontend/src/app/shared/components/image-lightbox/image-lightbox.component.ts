import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideDownload, lucideMinus, lucidePlus, lucideX } from '@ng-icons/lucide';

@Component({
  selector: 'app-image-lightbox',
  standalone: true,
  imports: [CommonModule, NgIcon],
  providers: [provideIcons({ lucideDownload, lucideMinus, lucidePlus, lucideX })],
  templateUrl: './image-lightbox.component.html',
})
export class ImageLightboxComponent implements OnChanges {
  @Input() image: { url: string; name: string } | null = null;
  @Output() closed = new EventEmitter<void>();
  scale = 1;
  isClosing = false;

  ngOnChanges(changes: SimpleChanges) {
    if (changes['image']?.currentValue) {
      this.scale = 1;
      this.isClosing = false;
    }
  }

  zoom(amount: number) {
    this.scale = Math.min(3, Math.max(0.25, this.scale + amount));
  }

  onWheel(event: WheelEvent) {
    event.preventDefault();
    event.stopPropagation();
    const factor = Math.exp(-event.deltaY * 0.0015);
    this.scale = Math.min(3, Math.max(0.25, this.scale * factor));
  }

  close() {
    if (this.isClosing) return;
    this.isClosing = true;
    window.setTimeout(() => this.closed.emit(), 120);
  }
}
