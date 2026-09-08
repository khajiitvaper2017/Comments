import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommentApiService } from '@app/core/services/comment-api.service';
import { Captcha, CommentFormValue } from '@app/core/models/comment.models';

@Component({
  selector: 'app-comment-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './comment-form.component.html',
})
export class CommentFormComponent {
  @Input() captcha: Captcha | null = null;
  @Input() parentId = '';
  @Input() replyToName = '';
  @Output() submitted = new EventEmitter<void>();
  @Output() captchaChanged = new EventEmitter<Captcha>();
  @Output() errorChanged = new EventEmitter<string>();
  private readonly api = inject(CommentApiService);
  form: CommentFormValue = {
    userName: '',
    email: '',
    homePage: '',
    text: '',
    captchaAnswer: '',
    parentId: '',
  };
  file?: File;

  refreshCaptcha() {
    this.api.getCaptcha().subscribe((captcha) => this.captchaChanged.emit(captcha));
  }
  chooseFile(event: Event) {
    this.file = (event.target as HTMLInputElement).files?.[0];
  }
  submit() {
    this.errorChanged.emit('');
    if (!this.captcha) return;
    this.api
      .createComment({ ...this.form, parentId: this.parentId }, this.captcha.id, this.file)
      .subscribe(
        () => {
          this.form = {
            userName: '',
            email: '',
            homePage: '',
            text: '',
            captchaAnswer: '',
            parentId: '',
          };
          this.file = undefined;
          this.refreshCaptcha();
          this.submitted.emit();
        },
        (error) => {
          const message =
            typeof error.error === 'string'
              ? error.error
              : error.error?.error || error.error?.title || 'Could not submit comment.';
          this.errorChanged.emit(message);
          if (message.toLowerCase().includes('captcha')) {
            this.form.captchaAnswer = '';
            this.refreshCaptcha();
          }
        },
      );
  }
}
