import { Component, OnInit, inject, signal } from '@angular/core';
import { CommentApiService } from '@app/core/services/comment-api.service';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { CommentListComponent } from '@app/shared/components/comment-list/comment-list.component';
import { ImageLightboxComponent } from '@app/shared/components/image-lightbox/image-lightbox.component';
import { TextPreviewComponent } from '@app/shared/components/text-preview/text-preview.component';

@Component({
  selector: 'app-comments-page',
  standalone: true,
  imports: [
    CommentListComponent,
    CommentFormComponent,
    ImageLightboxComponent,
    TextPreviewComponent,
  ],
  templateUrl: './comments-page.component.html',
})
export class CommentsPageComponent implements OnInit {
  private readonly api = inject(CommentApiService);
  comments = signal<CommentItem[]>([]);
  captcha = signal<Captcha | null>(null);
  selectedImage = signal<{ url: string; name: string } | null>(null);
  selectedText = signal<{ name: string; content: string } | null>(null);
  error = signal('');
  page = 1;
  total = 0;
  sort = 'createdAt';
  descending = true;
  replyParentId = '';
  showComposer = signal(false);
  ngOnInit() {
    this.refreshCaptcha();
    this.loadComments();
  }
  refreshCaptcha() {
    this.api.getCaptcha().subscribe((captcha) => this.captcha.set(captcha));
  }
  loadComments() {
    this.api.getComments(this.page, this.sort, this.descending).subscribe(
      (result) => {
        this.comments.set(result.items);
        this.total = result.totalCount;
      },
      () => this.error.set('Could not load comments.'),
    );
  }
  changeSort(sort: string) {
    this.descending = this.sort === sort ? !this.descending : sort === 'createdAt';
    this.sort = sort;
    this.page = 1;
    this.loadComments();
  }
  changePage(page: number) {
    this.page = page;
    this.loadComments();
  }
  setReplyParent(id: string) {
    this.replyParentId = id;
    this.showComposer.set(false);
    this.scrollToComposer();
  }
  openComposer() {
    this.replyParentId = '';
    this.showComposer.set(true);
    this.scrollToComposer();
  }
  cancelComposer() {
    this.replyParentId = '';
    this.showComposer.set(false);
    this.error.set('');
  }
  private scrollToComposer() {
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        const composer = document.querySelector<HTMLElement>('.composer');
        composer?.scrollIntoView({ behavior: 'smooth', block: 'end' });
        composer?.querySelector<HTMLTextAreaElement>('textarea')?.focus();
      });
    });
  }
  handleSubmitted() {
    this.error.set('');
    this.replyParentId = '';
    this.showComposer.set(false);
    this.page = 1;
    this.loadComments();
  }
  openImage(image: { id: string; name: string }) {
    this.selectedImage.set({ url: `/api/attachments/${image.id}`, name: image.name });
  }

  openText(text: { id: string; name: string }) {
    this.api.getAttachmentText(text.id).subscribe({
      next: (content) => this.selectedText.set({ name: text.name, content }),
      error: () => this.error.set('Could not preview text attachment.'),
    });
  }

  closeText() {
    this.selectedText.set(null);
  }
  closeImage() {
    this.selectedImage.set(null);
  }
}
