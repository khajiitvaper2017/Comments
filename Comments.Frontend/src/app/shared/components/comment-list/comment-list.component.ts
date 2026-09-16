import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideList, lucideTable2 } from '@ng-icons/lucide';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentCardsComponent } from '@app/shared/components/comment-cards/comment-cards.component';
import { CommentPagerComponent } from '@app/shared/components/comment-pager/comment-pager.component';
import { CommentTableComponent } from '@app/shared/components/comment-table/comment-table.component';

@Component({
  selector: 'app-comment-list',
  standalone: true,
  imports: [
    CommonModule,
    CommentCardsComponent,
    CommentPagerComponent,
    CommentTableComponent,
    NgIcon,
  ],
  providers: [provideIcons({ lucideList, lucideTable2 })],
  templateUrl: './comment-list.component.html',
})
export class CommentListComponent {
  @Input() comments: CommentItem[] = [];
  @Input() nextCursor: string | null = null;
  @Input() hasPreviousPage = false;
  @Input() sort = 'createdAt';
  @Input() descending = true;
  @Input() searchMode = false;
  @Input() highlightTerm = '';
  @Input() partialSearch = false;
  @Input() viewMode: 'cards' | 'table' = 'cards';
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Output() readonly sortChanged = new EventEmitter<string>();
  @Output() readonly pageChanged = new EventEmitter<'next' | 'previous'>();
  @Output() readonly viewModeChanged = new EventEmitter<'cards' | 'table'>();
  @Output() readonly replyRequested = new EventEmitter<string>();
  @Output() readonly loadRepliesRequested = new EventEmitter<string>();
  @Output() readonly loadAncestorsRequested = new EventEmitter<string>();
  @Output() readonly imageRequested = new EventEmitter<{
    id: string;
    name: string;
    contentType: string;
  }>();
  @Output() readonly textRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
  @Output() readonly submitted = new EventEmitter<CommentItem>();
  @Output() readonly cancelled = new EventEmitter<void>();

  protected setViewMode(mode: 'cards' | 'table') {
    this.viewMode = mode;
    this.viewModeChanged.emit(mode);
  }
}
