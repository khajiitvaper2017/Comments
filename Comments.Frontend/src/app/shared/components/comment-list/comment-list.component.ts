import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideList, lucideTable2 } from '@ng-icons/lucide';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentCardsComponent } from '@app/shared/components/comment-cards/comment-cards.component';
import { CommentTableComponent } from '@app/shared/components/comment-table/comment-table.component';

@Component({
  selector: 'app-comment-list',
  standalone: true,
  imports: [CommonModule, CommentCardsComponent, CommentTableComponent, NgIcon],
  providers: [provideIcons({ lucideList, lucideTable2 })],
  templateUrl: './comment-list.component.html',
})
export class CommentListComponent {
  @Input() comments: CommentItem[] = [];
  @Input() total = 0;
  @Input() totalReplyCount = 0;
  @Input() page = 1;
  @Input() pageSize = 25;
  @Input() sort = 'createdAt';
  @Input() descending = true;
  @Input() searchMode = false;
  @Input() highlightTerm = '';
  @Input() viewMode: 'cards' | 'table' = 'cards';
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Output() readonly sortChanged = new EventEmitter<string>();
  @Output() readonly pageChanged = new EventEmitter<number>();
  @Output() readonly viewModeChanged = new EventEmitter<'cards' | 'table'>();
  @Output() readonly replyRequested = new EventEmitter<string>();
  @Output() readonly loadRepliesRequested = new EventEmitter<string>();
  @Output() readonly imageRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly textRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
  @Output() readonly submitted = new EventEmitter<void>();
  @Output() readonly cancelled = new EventEmitter<void>();

  protected setViewMode(mode: 'cards' | 'table') {
    this.viewMode = mode;
    this.viewModeChanged.emit(mode);
  }

  protected get replyCount(): number {
    return this.comments.reduce((count, comment) => count + this.countReplies(comment.replies), 0);
  }

  protected get pageCount(): number {
    return Math.max(1, Math.ceil(this.total / Math.max(1, this.pageSize)));
  }

  protected get pageItems(): Array<number | 'ellipsis'> {
    const lastPage = this.pageCount;
    if (lastPage <= 7) return Array.from({ length: lastPage }, (_, index) => index + 1);

    const items: Array<number | 'ellipsis'> = [1];
    if (this.page > 3) items.push('ellipsis');

    const firstVisible = Math.max(2, this.page - 1);
    const lastVisible = Math.min(lastPage - 1, this.page + 1);
    for (let current = firstVisible; current <= lastVisible; current++) items.push(current);

    if (lastVisible < lastPage - 1) items.push('ellipsis');
    items.push(lastPage);
    return items;
  }

  private countReplies(replies: CommentItem[]): number {
    return replies.reduce((count, reply) => count + 1 + this.countReplies(reply.replies), 0);
  }
}
