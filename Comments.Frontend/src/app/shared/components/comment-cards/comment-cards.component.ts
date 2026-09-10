import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideReply } from '@ng-icons/lucide';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { HomePageLabelPipe } from '@app/shared/pipes/home-page-label.pipe';

@Component({
  selector: 'app-comment-cards',
  standalone: true,
  imports: [CommonModule, CommentFormComponent, HomePageLabelPipe, NgIcon],
  providers: [provideIcons({ lucideReply })],
  templateUrl: './comment-cards.component.html',
})
export class CommentCardsComponent {
  @Input() comments: CommentItem[] = [];
  @Input() captcha: Captcha | null = null;
  @Input() replyParentId = '';
  @Input() baseDepth = 0;
  @Output() readonly replyRequested = new EventEmitter<string>();
  @Output() readonly imageRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly textRequested = new EventEmitter<{ id: string; name: string }>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
  @Output() readonly submitted = new EventEmitter<void>();
  @Output() readonly cancelled = new EventEmitter<void>();
}
