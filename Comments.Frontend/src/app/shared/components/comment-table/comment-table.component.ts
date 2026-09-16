import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentItem, Captcha } from '@app/core/models/comment.models';
import { CommentCardsComponent } from '@app/shared/components/comment-cards/comment-cards.component';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { HomePageLabelPipe } from '@app/shared/pipes/home-page-label.pipe';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronDown, lucideGitBranch, lucideReply } from '@ng-icons/lucide';
import { HighlightSearchPipe } from '@app/shared/pipes/highlight-search.pipe';

@Component({
  selector: 'app-comment-table',
  standalone: true,
  imports: [
    CommonModule,
    CommentCardsComponent,
    CommentFormComponent,
    HomePageLabelPipe,
    HighlightSearchPipe,
    NgIcon,
  ],
  providers: [provideIcons({ lucideChevronDown, lucideGitBranch, lucideReply })],
  templateUrl: './comment-table.component.html',
})
export class CommentTableComponent {
  @Input() comments: CommentItem[] = [];
  @Input() searchMode = false;
  @Input() sort = 'createdAt';
  @Input() descending = true;
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Input() highlightTerm = '';
  @Input() partialSearch = false;
  @Output() readonly sortChanged = new EventEmitter<string>();
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
  protected readonly collapsedReplies = new Set<string>();
  private readonly loadedSearchReplies = new Set<string>();

  protected toggleReplies(commentId: string) {
    if (this.collapsedReplies.has(commentId)) {
      this.collapsedReplies.delete(commentId);
    } else {
      this.collapsedReplies.add(commentId);
    }
  }

  protected repliesAreVisible(commentId: string) {
    return (
      (!this.collapsedReplies.has(commentId) &&
        (this.findComment(this.comments, commentId)?.replies.length ?? 0) > 0) ||
      this.replyParentId === commentId ||
      (!this.collapsedReplies.has(commentId) && this.containsReply(commentId, this.replyParentId))
    );
  }

  protected needsMoreReplies(comment: CommentItem) {
    return (
      this.searchMode &&
      this.countLoadedReplies(comment.replies) < comment.replyCount &&
      !this.loadedSearchReplies.has(comment.id)
    );
  }

  protected isLoadMore(comment: CommentItem) {
    return comment.replyCount > 0 && (!comment.replies.length || this.needsMoreReplies(comment));
  }

  private countLoadedReplies(replies: CommentItem[]): number {
    return replies.reduce((count, reply) => count + 1 + this.countLoadedReplies(reply.replies), 0);
  }

  protected toggleOrLoadReplies(comment: CommentItem) {
    const needsSearchLoad = this.needsMoreReplies(comment);
    if ((!comment.replies.length || needsSearchLoad) && comment.replyCount > 0) {
      this.loadedSearchReplies.add(comment.id);
      this.collapsedReplies.delete(comment.id);
      this.loadRepliesRequested.emit(comment.id);
      return;
    }

    this.toggleReplies(comment.id);
  }

  private containsReply(commentId: string, targetId: string): boolean {
    const comment = this.findComment(this.comments, commentId);
    return (
      comment?.replies.some(
        (reply) => reply.id === targetId || this.containsReplyIn(reply, targetId),
      ) ?? false
    );
  }

  private containsReplyIn(comment: CommentItem, targetId: string): boolean {
    return comment.replies.some(
      (reply) => reply.id === targetId || this.containsReplyIn(reply, targetId),
    );
  }

  private findComment(comments: CommentItem[], id: string): CommentItem | undefined {
    for (const comment of comments) {
      if (comment.id === id) return comment;
      const nested = this.findComment(comment.replies, id);
      if (nested) return nested;
    }
    return undefined;
  }
}
