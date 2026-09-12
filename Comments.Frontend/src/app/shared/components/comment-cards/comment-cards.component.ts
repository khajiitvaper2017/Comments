import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronDown, lucideReply } from '@ng-icons/lucide';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { HomePageLabelPipe } from '@app/shared/pipes/home-page-label.pipe';
import { HighlightSearchPipe } from '@app/shared/pipes/highlight-search.pipe';

@Component({
  selector: 'app-comment-cards',
  standalone: true,
  imports: [CommonModule, CommentFormComponent, HomePageLabelPipe, HighlightSearchPipe, NgIcon],
  providers: [provideIcons({ lucideChevronDown, lucideReply })],
  templateUrl: './comment-cards.component.html',
})
export class CommentCardsComponent {
  @Input() comments: CommentItem[] = [];
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Input() baseDepth = 0;
  @Input() highlightTerm = '';
  @Output() readonly replyRequested = new EventEmitter<string>();
  @Output() readonly loadRepliesRequested = new EventEmitter<string>();
  @Output() readonly imageRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly textRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
  @Output() readonly submitted = new EventEmitter<void>();
  @Output() readonly cancelled = new EventEmitter<void>();
  protected readonly collapsedReplies = new Set<string>();

  protected toggleOrLoadReplies(comment: CommentItem) {
    if (!comment.replies.length && comment.hasMoreReplies) {
      this.collapsedReplies.delete(comment.id);
      this.loadRepliesRequested.emit(comment.id);
      return;
    }

    if (this.collapsedReplies.has(comment.id)) {
      this.collapsedReplies.delete(comment.id);
    } else {
      this.collapsedReplies.add(comment.id);
    }
  }

  protected repliesAreVisible(comment: CommentItem) {
    return comment.replies.length > 0 && !this.collapsedReplies.has(comment.id);
  }
}
