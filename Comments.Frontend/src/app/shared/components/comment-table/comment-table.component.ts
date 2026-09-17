import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentItem } from '@app/core/models/comment.models';
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
  @Input() sort = 'createdAt';
  @Input() descending = true;
  @Input() highlightTerm = '';
  @Input() partialSearch = false;
  @Output() readonly sortChanged = new EventEmitter<string>();
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
