import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentItem, Captcha } from '@app/core/models/comment.models';
import { CommentCardsComponent } from '@app/shared/components/comment-cards/comment-cards.component';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { HomePageLabelPipe } from '@app/shared/pipes/home-page-label.pipe';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronDown, lucideReply } from '@ng-icons/lucide';

@Component({
  selector: 'app-comment-table',
  standalone: true,
  imports: [CommonModule, CommentCardsComponent, CommentFormComponent, HomePageLabelPipe, NgIcon],
  providers: [provideIcons({ lucideChevronDown, lucideReply })],
  templateUrl: './comment-table.component.html',
})
export class CommentTableComponent {
  @Input() comments: CommentItem[] = [];
  @Input() sort = 'createdAt';
  @Input() descending = true;
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Output() readonly sortChanged = new EventEmitter<string>();
  @Output() readonly replyRequested = new EventEmitter<string>();
  @Output() readonly imageRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly textRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
  @Output() readonly submitted = new EventEmitter<void>();
  @Output() readonly cancelled = new EventEmitter<void>();
  protected readonly expandedReplies = new Set<string>();

  protected toggleReplies(commentId: string) {
    if (this.expandedReplies.has(commentId)) {
      this.expandedReplies.delete(commentId);
    } else {
      this.expandedReplies.add(commentId);
    }
  }

  protected repliesAreVisible(commentId: string) {
    return this.expandedReplies.has(commentId) || this.replyParentId === commentId;
  }
}
