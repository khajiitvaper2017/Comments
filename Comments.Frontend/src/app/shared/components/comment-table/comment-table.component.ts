import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentItem } from '@app/core/models/comment.models';
import { CommentSort } from '@app/core/utils/comments-query-params';
import { HomePageLabelPipe } from '@app/shared/pipes/home-page-label.pipe';

@Component({
  selector: 'app-comment-table',
  standalone: true,
  imports: [CommonModule, HomePageLabelPipe],
  templateUrl: './comment-table.component.html',
})
export class CommentTableComponent {
  @Input() comments: CommentItem[] = [];
  @Input() sort: CommentSort = 'createdAt';
  @Input() descending = true;
  @Output() readonly sortChanged = new EventEmitter<CommentSort>();
  @Output() readonly imageRequested = new EventEmitter<{
    id: string;
    name: string;
    contentType: string;
  }>();
  @Output() readonly textRequested = new EventEmitter<{ id: string; name: string }>();

  protected get rootComments(): CommentItem[] {
    return this.comments.filter((comment) => comment.parentId == null);
  }
}
