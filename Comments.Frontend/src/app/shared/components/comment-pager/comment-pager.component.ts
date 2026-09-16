import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronLeft, lucideChevronRight } from '@ng-icons/lucide';

@Component({
  selector: 'app-comment-pager',
  standalone: true,
  imports: [NgIcon],
  providers: [provideIcons({ lucideChevronLeft, lucideChevronRight })],
  templateUrl: './comment-pager.component.html',
})
export class CommentPagerComponent {
  @Input() nextCursor: string | null = null;
  @Input() hasPreviousPage = false;
  @Output() readonly pageChanged = new EventEmitter<'next' | 'previous'>();
}
