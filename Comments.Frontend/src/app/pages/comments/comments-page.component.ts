import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMessageCirclePlus, lucideSearch } from '@ng-icons/lucide';
import { CommentItem } from '@app/core/models/comment.models';
import { CommentViewMode } from '@app/core/utils/comments-query-params';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { CommentListComponent } from '@app/shared/components/comment-list/comment-list.component';
import { ImageLightboxComponent } from '@app/shared/components/image-lightbox/image-lightbox.component';
import { TextPreviewComponent } from '@app/shared/components/text-preview/text-preview.component';
import { CommentsPageStore } from './comments-page.store';

@Component({
  selector: 'app-comments-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    CommentListComponent,
    CommentFormComponent,
    ImageLightboxComponent,
    TextPreviewComponent,
    NgIcon,
  ],
  providers: [CommentsPageStore, provideIcons({ lucideMessageCirclePlus, lucideSearch })],
  templateUrl: './comments-page.component.html',
})
export class CommentsPageComponent implements OnInit, OnDestroy {
  readonly store = inject(CommentsPageStore);

  ngOnInit() {
    this.store.initialize();
  }

  ngOnDestroy() {
    this.store.destroy();
  }

  openComposer() {
    this.store.openRootComposer();
    this.scrollToComposer('smooth', 'start');
  }

  setReplyParent(parentId: string) {
    this.store.openReplyComposer(parentId);
    this.scrollToComposer();
  }

  cancelComposer() {
    this.store.closeComposer();
  }

  handleSubmitted(comment: CommentItem) {
    this.store.applyCreatedComment(comment);
  }

  changeSort(sort: string) {
    if (sort === 'createdAt' || sort === 'userName' || sort === 'email') {
      this.store.changeSort(sort);
    }
  }

  changeViewMode(viewMode: CommentViewMode) {
    this.store.changeViewMode(viewMode);
  }

  private scrollToComposer(
    behavior: ScrollBehavior = 'smooth',
    block: ScrollLogicalPosition = 'end',
  ) {
    window.requestAnimationFrame(() => {
      window.requestAnimationFrame(() => {
        const composer = document.querySelector<HTMLElement>('.composer');
        composer?.scrollIntoView({ behavior, block });
        composer?.querySelector<HTMLTextAreaElement>('textarea')?.focus();
      });
    });
  }
}
