import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { CommentApiService } from '@app/core/services/comment-api.service';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentFormComponent } from '@app/shared/components/comment-form/comment-form.component';
import { CommentListComponent } from '@app/shared/components/comment-list/comment-list.component';
import { ImageLightboxComponent } from '@app/shared/components/image-lightbox/image-lightbox.component';
import { TextPreviewComponent } from '@app/shared/components/text-preview/text-preview.component';
import { finalize, Subscription } from 'rxjs';

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
  ],
  templateUrl: './comments-page.component.html',
})
export class CommentsPageComponent implements OnInit, OnDestroy {
  private readonly api = inject(CommentApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  comments = signal<CommentItem[]>([]);
  captcha = signal<Captcha | null>(null);
  selectedImage = signal<{ url: string; name: string } | null>(null);
  selectedText = signal<{ name: string; content: string } | null>(null);
  error = signal('');
  loading = signal(false);
  searchQuery = '';
  activeSearchQuery = '';
  searchActive = signal(false);
  searchLoading = signal(false);
  private readonly hub: HubConnection = new HubConnectionBuilder()
    .withUrl('/hubs/discussions')
    .withAutomaticReconnect()
    .build();
  page = 1;
  pageSize = 25;
  total = 0;
  totalReplyCount = 0;
  sort = 'createdAt';
  descending = true;
  viewMode: 'cards' | 'table' = 'cards';
  replyParentId = '';
  showComposer = signal(false);
  private realtimeRefreshTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly loadedReplies = new Map<string, CommentItem[]>();
  private querySubscription?: Subscription;
  ngOnInit() {
    this.hub.on('commentChanged', () => this.scheduleRealtimeRefresh());
    void this.hub.start().catch(() => undefined);
    this.querySubscription = this.route.queryParamMap.subscribe((params) =>
      this.loadFromQuery(params),
    );
  }

  ngOnDestroy() {
    this.querySubscription?.unsubscribe();
    if (this.realtimeRefreshTimer) clearTimeout(this.realtimeRefreshTimer);
    void this.hub.stop();
  }

  private loadFromQuery(params: ParamMap) {
    this.searchQuery = params.get('q')?.trim() ?? '';
    this.activeSearchQuery = this.searchQuery;
    this.page = this.parsePositiveInt(params.get('page'), 1);
    const sort = params.get('sort');
    this.sort = sort === 'userName' || sort === 'email' ? sort : 'createdAt';
    this.descending = params.get('descending') !== 'false';
    this.viewMode = params.get('view') === 'table' ? 'table' : 'cards';

    if (this.activeSearchQuery) {
      this.searchActive.set(true);
      this.loadSearchResults();
    } else {
      this.searchActive.set(false);
      this.loadComments();
    }
  }

  private parsePositiveInt(value: string | null, fallback: number): number {
    const parsed = Number(value);
    return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
  }

  private updateUrl() {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: this.activeSearchQuery || null,
        page: this.page > 1 ? this.page : null,
        sort: this.sort === 'createdAt' ? null : this.sort,
        descending: this.descending ? null : false,
        view: this.viewMode === 'cards' ? null : this.viewMode,
      },
    });
  }

  loadComments(showLoading = true) {
    if (showLoading) this.loading.set(true);
    this.api
      .getComments(this.page, this.sort, this.descending)
      .pipe(finalize(() => showLoading && this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.comments.set(this.restoreLoadedReplies(result.items));
          this.pageSize = result.pageSize;
          this.total = result.totalCount;
          this.totalReplyCount = result.totalReplyCount;
        },
        error: () => this.error.set('Could not load comments.'),
      });
  }

  private scheduleRealtimeRefresh() {
    if (this.realtimeRefreshTimer || this.searchActive()) return;

    this.realtimeRefreshTimer = setTimeout(() => {
      this.realtimeRefreshTimer = undefined;
      this.loadComments(false);
    }, 1000);
  }

  search() {
    this.activeSearchQuery = this.searchQuery.trim();
    this.page = 1;
    this.updateUrl();
  }

  private loadSearchResults() {
    this.searchLoading.set(true);
    this.api
      .searchComments(this.activeSearchQuery, this.page)
      .pipe(finalize(() => this.searchLoading.set(false)))
      .subscribe({
        next: (result) => {
          this.comments.set(this.restoreLoadedReplies(result.items));
          this.pageSize = result.pageSize;
          this.total = result.totalCount;
          this.totalReplyCount = result.totalReplyCount;
          this.page = result.page;
        },
        error: () => this.error.set('Search is currently unavailable.'),
      });
  }

  changeSort(sort: string) {
    this.descending = this.sort === sort ? !this.descending : sort === 'createdAt';
    this.sort = sort;
    this.page = 1;
    this.updateUrl();
  }

  changePage(page: number) {
    if (page === this.page) return;
    this.page = page;
    // Long smooth-scroll animations become janky on pages containing many replies.
    window.scrollTo({ top: 0, behavior: 'smooth' });
    this.updateUrl();
  }

  changeViewMode(viewMode: 'cards' | 'table') {
    this.viewMode = viewMode;
    this.updateUrl();
  }

  loadReplies(parentId: string) {
    this.api.getReplies(parentId).subscribe({
      next: (replies) => {
        this.loadedReplies.set(parentId, replies);
        this.comments.update((comments) => this.attachReplies(comments, parentId, replies));
      },
      error: () => this.error.set('Could not load replies.'),
    });
  }

  private attachReplies(
    comments: CommentItem[],
    parentId: string,
    replies: CommentItem[],
  ): CommentItem[] {
    return comments.map((comment) => {
      if (comment.id === parentId) {
        return { ...comment, replies, hasMoreReplies: false };
      }

      return {
        ...comment,
        replies: this.attachReplies(comment.replies, parentId, replies),
      };
    });
  }

  private restoreLoadedReplies(comments: CommentItem[]): CommentItem[] {
    return comments.map((comment) => {
      const savedReplies = this.loadedReplies.get(comment.id);
      const replies = savedReplies
        ? this.mergeReplies(comment.replies, savedReplies)
        : comment.replies;
      return {
        ...comment,
        replies: this.restoreLoadedReplies(replies),
        hasMoreReplies: savedReplies ? false : comment.hasMoreReplies,
      };
    });
  }

  private mergeReplies(serverReplies: CommentItem[], loadedReplies: CommentItem[]): CommentItem[] {
    const loadedById = new Map(loadedReplies.map((reply) => [reply.id, reply]));
    const merged = serverReplies.map((reply) => loadedById.get(reply.id) ?? reply);
    const serverIds = new Set(serverReplies.map((reply) => reply.id));
    return [...merged, ...loadedReplies.filter((reply) => !serverIds.has(reply.id))];
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
    this.updateUrl();
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
