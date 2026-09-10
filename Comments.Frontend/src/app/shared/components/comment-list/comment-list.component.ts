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
  @Input() page = 1;
  @Input() sort = 'createdAt';
  @Input() descending = true;
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  viewMode: 'cards' | 'table' = 'cards';
  @Output() readonly sortChanged = new EventEmitter<string>();
  @Output() readonly pageChanged = new EventEmitter<number>();
  @Output() readonly replyRequested = new EventEmitter<string>();
  @Output() readonly imageRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
  @Output() readonly submitted = new EventEmitter<void>();
  @Output() readonly cancelled = new EventEmitter<void>();

  protected setViewMode(mode: 'cards' | 'table') {
    this.viewMode = mode;
  }

  protected get replyCount(): number {
    return this.comments.reduce((count, comment) => count + this.countReplies(comment.replies), 0);
  }

  private countReplies(replies: CommentItem[]): number {
    return replies.reduce((count, reply) => count + 1 + this.countReplies(reply.replies), 0);
  }
}
