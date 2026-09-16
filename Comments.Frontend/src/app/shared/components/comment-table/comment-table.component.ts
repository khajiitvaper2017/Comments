import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentItem, Captcha } from '@app/core/models/comment.models';
import { HomePageLabelPipe } from '@app/shared/pipes/home-page-label.pipe';
import { HighlightSearchPipe } from '@app/shared/pipes/highlight-search.pipe';

@Component({
  selector: 'app-comment-table',
  standalone: true,
  imports: [CommonModule, HomePageLabelPipe, HighlightSearchPipe],
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

  protected get rootComments(): CommentItem[] {
    return this.comments.filter((comment) => comment.parentId == null);
  }
}
