import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-image-lightbox',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './image-lightbox.component.html',
})
export class ImageLightboxComponent {
  @Input() image: { url: string; name: string } | null = null;
  @Output() closed = new EventEmitter<void>();
  scale = 1;
  zoom(amount: number) {
    this.scale = Math.min(3, Math.max(0.25, this.scale + amount));
  }
  onWheel(event: WheelEvent) {
    event.preventDefault();
    this.zoom(event.deltaY < 0 ? 0.25 : -0.25);
  }
}
