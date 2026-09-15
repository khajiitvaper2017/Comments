import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-comment-pager',
  standalone: true,
  templateUrl: './comment-pager.component.html',
})
export class CommentPagerComponent {
  @Input() nextCursor: string | null = null;
  @Input() hasPreviousPage = false;
  @Output() readonly pageChanged = new EventEmitter<'next' | 'previous'>();
}
