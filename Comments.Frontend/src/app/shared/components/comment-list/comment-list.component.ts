import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommentItem } from '@app/core/models/comment.models';
import { Captcha } from '@app/core/models/comment.models';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';

@Component({
  selector: 'app-comment-list',
  standalone: true,
  imports: [CommonModule, CommentFormComponent],
  templateUrl: './comment-list.component.html',
})
export class CommentListComponent {
  @Input() comments: CommentItem[] = [];
  @Input() total = 0;
  @Input() page = 1;
  @Input() sort = 'createdAt';
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Output() sortChanged = new EventEmitter<string>();
  @Output() pageChanged = new EventEmitter<number>();
  @Output() replyRequested = new EventEmitter<string>();
  @Output() imageRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() captchaChanged = new EventEmitter<Captcha>();
  @Output() errorChanged = new EventEmitter<string>();
  @Output() submitted = new EventEmitter<void>();

  homePageLabel(value: string) {
    try {
      return new URL(value).hostname.replace(/^www\./i, '');
    } catch {
      return value
        .replace(/^https?:\/\//i, '')
        .split('/')[0]
        .replace(/^www\./i, '');
    }
  }

  track(_: number, comment: CommentItem) {
    return comment.id;
  }
}
