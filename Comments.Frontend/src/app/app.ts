import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  size: number;
}
interface Comment {
  id: string;
  parentId?: string;
  userName: string;
  email: string;
  homePage?: string;
  text: string;
  createdAtUtc: string;
  attachments: Attachment[];
  replies: Comment[];
}
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App implements OnInit {
  comments = signal<Comment[]>([]);
  captcha = signal<{ id: string; imageDataUrl: string } | null>(null);
  selectedImage = signal<{ url: string; name: string } | null>(null);
  imageScale = signal(1);
  error = signal('');
  page = 1;
  total = 0;
  sort = 'createdAt';
  descending = true;
  form = { userName: '', email: '', homePage: '', text: '', captchaAnswer: '', parentId: '' };
  file?: File;
  constructor(private http: HttpClient) {}
  ngOnInit() {
    this.refreshCaptcha();
    this.load();
  }
  refreshCaptcha() {
    this.http
      .get<{ id: string; imageDataUrl: string }>('/api/captcha')
      .subscribe((x) => this.captcha.set(x));
  }
  load() {
    this.http
      .get<any>(`/api/comments?page=${this.page}&sort=${this.sort}&descending=${this.descending}`)
      .subscribe(
        (x) => {
          this.comments.set(x.items);
          this.total = x.totalCount;
        },
        () => this.error.set('Could not load comments.'),
      );
  }
  changeSort(s: string) {
    this.sort === s
      ? (this.descending = !this.descending)
      : ((this.sort = s), (this.descending = s === 'createdAt'));
    this.page = 1;
    this.load();
  }
  chooseFile(e: Event) {
    this.file = (e.target as HTMLInputElement).files?.[0];
  }
  private messageFromError(e: any) {
    return typeof e.error === 'string'
      ? e.error
      : e.error?.error || e.error?.title || 'Could not submit comment.';
  }
  submit() {
    this.error.set('');
    const c = this.captcha();
    if (!c) return;
    const data = new FormData();
    Object.entries({ ...this.form, captchaId: c.id, parentId: this.form.parentId || '' }).forEach(
      ([k, v]) => data.append(k, v),
    );
    if (this.file) data.append('attachments', this.file);
    this.http.post<Comment>('/api/comments', data).subscribe(
      () => {
        this.error.set('');
        this.form = {
          userName: '',
          email: '',
          homePage: '',
          text: '',
          captchaAnswer: '',
          parentId: '',
        };
        this.file = undefined;
        this.page = 1;
        this.refreshCaptcha();
        this.load();
      },
      (e) => {
        this.error.set(this.messageFromError(e));
        if (this.error().toLowerCase().includes('captcha')) {
          this.form.captchaAnswer = '';
          this.refreshCaptcha();
        }
      },
    );
  }
  reply(id: string) {
    this.form.parentId = id;
    document.querySelector('textarea')?.focus();
  }
  openImage(id: string, name: string) {
    this.imageScale.set(1);
    this.selectedImage.set({ url: `/api/attachments/${id}`, name });
  }
  closeImage() {
    this.selectedImage.set(null);
  }
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
  zoomImage(amount: number) {
    this.imageScale.update((scale) => Math.min(3, Math.max(0.25, scale + amount)));
  }
  zoomWithWheel(event: WheelEvent) {
    event.preventDefault();
    this.zoomImage(event.deltaY < 0 ? 0.25 : -0.25);
  }
  track(_: number, c: Comment) {
    return c.id;
  }
}
