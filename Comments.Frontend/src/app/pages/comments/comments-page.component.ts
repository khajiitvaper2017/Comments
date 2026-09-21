import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMessageCirclePlus, lucideSearch } from '@ng-icons/lucide';
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
export class CommentsPageComponent {
  readonly store = inject(CommentsPageStore);

  openComposer() {
    this.store.openRootComposer();
    this.scrollToComposer('smooth', 'start');
  }

  setReplyParent(parentId: string) {
    this.store.openReplyComposer(parentId);
    this.scrollToComposer();
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
