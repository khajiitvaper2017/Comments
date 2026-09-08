import { CommonModule } from '@angular/common';
import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild,
  inject,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCode2, lucideEye, lucidePaperclip, lucideRefreshCw } from '@ng-icons/lucide';
import { CommentApiService } from '@app/core/services/comment-api.service';
import { Captcha, CommentFormValue } from '@app/core/models/comment.models';

@Component({
  selector: 'app-comment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, NgIcon],
  providers: [provideIcons({ lucideCode2, lucideEye, lucidePaperclip, lucideRefreshCw })],
  templateUrl: './comment-form.component.html',
})
export class CommentFormComponent {
  @ViewChild('textInput') private textInput?: ElementRef<HTMLTextAreaElement>;
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
  textMode: 'code' | 'preview' = 'code';

  refreshCaptcha() {
    this.api.getCaptcha().subscribe((captcha) => this.captchaChanged.emit(captcha));
  }
  chooseFile(event: Event) {
    this.file = (event.target as HTMLInputElement).files?.[0];
  }
  insertTag(open: string, close = '') {
    const textarea = this.textInput?.nativeElement;
    if (!textarea) return;

    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selected = this.form.text.slice(start, end) || 'text';
    this.form.text =
      this.form.text.slice(0, start) + open + selected + close + this.form.text.slice(end);
    const cursor = start + open.length + selected.length + close.length;
    queueMicrotask(() => {
      textarea.focus();
      textarea.setSelectionRange(cursor, cursor);
    });
  }
  insertLink() {
    this.insertTag('<a href="https://example.com" title="Link">', '</a>');
  }
  setTextMode(mode: 'code' | 'preview') {
    this.textMode = mode;
    if (mode === 'code') {
      queueMicrotask(() => this.textInput?.nativeElement.focus());
    }
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
          this.textMode = 'code';
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
