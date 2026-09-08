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
import { FormsModule, NgForm } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  lucideBold,
  lucideCode2,
  lucideEye,
  lucideItalic,
  lucideLink,
  lucidePaperclip,
  lucideRefreshCw,
} from '@ng-icons/lucide';
import { CommentApiService } from '@app/core/services/comment-api.service';
import { Captcha, CommentFormValue } from '@app/core/models/comment.models';

@Component({
  selector: 'app-comment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, NgIcon],
  providers: [
    provideIcons({
      lucideBold,
      lucideCode2,
      lucideEye,
      lucideItalic,
      lucideLink,
      lucidePaperclip,
      lucideRefreshCw,
    }),
  ],
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
  showCaptcha = false;
  captchaError = '';
  submittedForm = false;
  captchaLoading = false;

  refreshCaptcha() {
    this.captchaError = '';
    this.captchaLoading = true;
    this.api.getCaptcha().subscribe((captcha) => {
      this.captchaLoading = false;
      this.captchaChanged.emit(captcha);
    });
  }
  openCaptcha() {
    this.captchaError = '';
    this.form.captchaAnswer = '';
    this.showCaptcha = true;
    this.refreshCaptcha();
  }
  closeCaptcha() {
    this.showCaptcha = false;
    this.captchaError = '';
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
  submit(commentForm?: NgForm) {
    this.errorChanged.emit('');
    if (!this.showCaptcha) {
      this.submittedForm = true;
      if (!commentForm || commentForm.invalid) {
        commentForm?.form.markAllAsTouched();
        return;
      }
      this.openCaptcha();
      return;
    }
    if (!this.captcha) return;
    if (!this.form.captchaAnswer.trim()) {
      this.captchaError = 'Enter the CAPTCHA code.';
      return;
    }
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
          this.submittedForm = false;
          this.showCaptcha = false;
          this.captchaError = '';
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
            this.captchaError = message;
          }
        },
      );
  }
}
