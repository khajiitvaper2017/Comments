import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
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
import { Captcha, CommentFormValue, CommentItem } from '@app/core/models/comment.models';
import { finalize } from 'rxjs';
import {
  validateHomePage,
  validateMarkup,
  validateText,
} from '@app/shared/validation/comment-validation';

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
  private static readonly profileStorageKey = 'comments.user-profile';
  @ViewChild('textInput') private textInput?: ElementRef<HTMLTextAreaElement>;
  @Input() captcha: Captcha | null = null;
  @Input() parentId = '';
  @Input() replyToName = '';
  @Output() readonly submitted = new EventEmitter<CommentItem>();
  @Output() readonly cancelled = new EventEmitter<void>();
  @Output() readonly captchaChanged = new EventEmitter<Captcha>();
  @Output() readonly errorChanged = new EventEmitter<string>();
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
  isSubmitting = false;
  formError = '';
  homePageError = '';

  constructor() {
    this.restoreUserProfile();
  }

  onHomePageChange(value: string) {
    this.homePageError = this.getHomePageError(value);
  }

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
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file?.name.toLowerCase().endsWith('.txt') && file.size > 100 * 1024) {
      this.file = undefined;
      input.value = '';
      this.formError = 'TXT files must be no larger than 100 KB.';
      return;
    }
    this.formError = '';
    this.file = file;
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
    if (mode === 'preview') {
      const markupError = validateMarkup(this.form.text);
      if (markupError) {
        this.formError = markupError;
        return;
      }
    }
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
      this.homePageError = this.getHomePageError(this.form.homePage);
      if (this.homePageError) return;
      const textError = validateText(this.form.text);
      if (textError) {
        this.formError = textError;
        return;
      }
      if (this.file?.name.toLowerCase().endsWith('.txt') && this.file.size > 100 * 1024) return;
      this.formError = '';
      this.openCaptcha();
      return;
    }
    this.formError = '';
    if (!this.captcha) return;
    if (this.isSubmitting) return;
    if (!this.form.captchaAnswer.trim()) {
      this.captchaError = 'Enter the CAPTCHA code.';
      return;
    }
    this.isSubmitting = true;
    this.api
      .createComment({ ...this.form, parentId: this.parentId }, this.captcha.id, this.file)
      .pipe(finalize(() => (this.isSubmitting = false)))
      .subscribe(
        (created) => {
          this.isSubmitting = false;
          this.saveUserProfile();
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
          this.formError = '';
          this.homePageError = '';
          this.submitted.emit(created);
        },
        (error: HttpErrorResponse) => {
          const message =
            error.status === 413
              ? 'The attachment is too large. Please choose a smaller file.'
              : typeof error.error === 'string'
                ? 'Could not submit comment.'
                : error.error?.error || error.error?.title || 'Could not submit comment.';
          this.errorChanged.emit(message);
          this.formError = message;
          if (message.toLowerCase().includes('captcha')) {
            this.form.captchaAnswer = '';
            this.refreshCaptcha();
            this.captchaError = message;
          } else {
            this.showCaptcha = false;
          }
        },
      );
  }

  private getHomePageError(value: string) {
    return validateHomePage(value);
  }

  private restoreUserProfile() {
    try {
      const stored = localStorage.getItem(CommentFormComponent.profileStorageKey);
      if (!stored) return;

      const profile = JSON.parse(stored) as Partial<
        Pick<CommentFormValue, 'userName' | 'email' | 'homePage'>
      >;
      this.form.userName = typeof profile.userName === 'string' ? profile.userName : '';
      this.form.email = typeof profile.email === 'string' ? profile.email : '';
      this.form.homePage = typeof profile.homePage === 'string' ? profile.homePage : '';
    } catch {
      // Ignore unavailable or invalid browser storage.
    }
  }

  private saveUserProfile() {
    try {
      localStorage.setItem(
        CommentFormComponent.profileStorageKey,
        JSON.stringify({
          userName: this.form.userName,
          email: this.form.email,
          homePage: this.form.homePage,
        }),
      );
    } catch {
      // Ignore unavailable browser storage.
    }
  }
}
