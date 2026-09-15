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
  private static readonly autoLoadReplyLimit = 5;
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
  partialSearch = false;
  searchText = true;
  searchUserName = true;
  activeSearchQuery = '';
  searchActive = signal(false);
  searchLoading = signal(false);
  private readonly hub: HubConnection = new HubConnectionBuilder()
    .withUrl('/hubs/discussions')
    .withAutomaticReconnect()
    .build();
  cursor: string | null = null;
  nextCursor: string | null = null;
  hasPreviousPage = false;
  private cursorHistory: Array<string | null> = [];
  sort = 'createdAt';
  descending = true;
  viewMode: 'cards' | 'table' = 'cards';
  replyParentId = '';
  showComposer = signal(false);
  private realtimeRefreshTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly loadedReplies = new Map<string, CommentItem[]>();
  private readonly loadingReplies = new Set<string>();
  private readonly loadingAncestors = new Set<string>();
  private pendingCursorNavigation: string | null | undefined;
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
    this.partialSearch = params.get('partial') === 'true';
    this.searchText = params.get('text') !== 'false';
    this.searchUserName = params.get('user') !== 'false';
    this.activeSearchQuery = this.searchQuery;
    const requestedCursor = params.get('cursor');
    const isCursorNavigation = this.pendingCursorNavigation === requestedCursor;
    this.pendingCursorNavigation = undefined;
    this.cursor = requestedCursor;
    if (!isCursorNavigation) this.cursorHistory = [];
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

  private updateUrl() {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: this.activeSearchQuery || null,
        partial: this.partialSearch ? true : null,
        text: this.searchText ? null : false,
        user: this.searchUserName ? null : false,
        sort: this.sort === 'createdAt' ? null : this.sort,
        descending: this.descending ? null : false,
        view: this.viewMode === 'cards' ? null : this.viewMode,
        cursor: this.cursor,
      },
    });
  }

  loadComments(showLoading = true, cursor = this.cursor) {
    if (showLoading) this.loading.set(true);
    this.api
      .getComments(this.sort, this.descending, cursor)
      .pipe(finalize(() => showLoading && this.loading.set(false)))
      .subscribe({
        next: (result) => {
          const comments = this.restoreLoadedReplies(result.items);
          this.comments.set(comments);
          this.cursor = cursor;
          this.nextCursor = result.nextCursor ?? null;
          this.hasPreviousPage = this.cursorHistory.length > 0;
          this.autoLoadSmallReplyTrees(comments);
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
    this.cursor = null;
    this.nextCursor = null;
    this.cursorHistory = [];
    this.updateUrl();
  }

  private loadSearchResults() {
    this.searchLoading.set(true);
    this.api
      .searchComments(
        this.activeSearchQuery,
        this.partialSearch,
        this.searchText,
        this.searchUserName,
        this.cursor,
      )
      .pipe(finalize(() => this.searchLoading.set(false)))
      .subscribe({
        next: (result) => {
          this.comments.set(this.restoreLoadedReplies(result.items));
          this.nextCursor = result.nextCursor ?? null;
          this.hasPreviousPage = this.cursorHistory.length > 0;
        },
        error: () => this.error.set('Search is currently unavailable.'),
      });
  }

  loadAncestors(commentId: string) {
    if (this.loadingAncestors.has(commentId)) return;
    const comment = this.findComment(this.comments(), commentId);
    const ids = comment?.ancestorIds ?? [];
    if (!comment || ids.length === 0) return;

    this.loadingAncestors.add(commentId);
    this.api
      .getAncestors(ids)
      .pipe(finalize(() => this.loadingAncestors.delete(commentId)))
      .subscribe({
        next: (ancestors) => {
          const path = this.attachSearchAncestors([comment], ancestors)[0];
          this.comments.update((comments) =>
            comments.map((item) => (item.id === commentId ? path : item)),
          );
        },
        error: () => this.error.set('Could not load comment ancestors.'),
      });
  }

  private attachSearchAncestors(items: CommentItem[], ancestors: CommentItem[]): CommentItem[] {
    const byId = new Map(ancestors.map((ancestor) => [ancestor.id, ancestor]));
    return items.map((hit) => {
      let current = hit;
      for (const ancestorId of [...(hit.ancestorIds ?? [])].reverse()) {
        const ancestor = byId.get(ancestorId);
        if (ancestor) current = { ...ancestor, replies: [current] };
      }
      return current;
    });
  }

  private findComment(comments: CommentItem[], id: string): CommentItem | undefined {
    for (const comment of comments) {
      if (comment.id === id) return comment;
      const nested = this.findComment(comment.replies, id);
      if (nested) return nested;
    }
    return undefined;
  }

  changeSort(sort: string) {
    this.descending = this.sort === sort ? !this.descending : sort === 'createdAt';
    this.sort = sort;
    this.cursor = null;
    this.nextCursor = null;
    this.cursorHistory = [];
    this.updateUrl();
  }

  changePage(direction: 'next' | 'previous') {
    if (direction === 'next' && !this.nextCursor) return;
    if (direction === 'previous' && this.cursorHistory.length === 0) return;

    const requestedCursor =
      direction === 'next' ? this.nextCursor : (this.cursorHistory.pop() ?? null);
    if (direction === 'next') this.cursorHistory.push(this.cursor);
    this.cursor = requestedCursor;
    this.pendingCursorNavigation = requestedCursor;
    // Long smooth-scroll animations become janky on pages containing many replies.
    window.scrollTo({ top: 0, behavior: 'smooth' });
    this.updateUrl();
  }

  changeViewMode(viewMode: 'cards' | 'table') {
    this.viewMode = viewMode;
    this.updateUrl();
  }

  loadReplies(parentId: string) {
    if (this.loadingReplies.has(parentId)) return;
    this.loadingReplies.add(parentId);
    this.api
      .getReplies(parentId)
      .pipe(finalize(() => this.loadingReplies.delete(parentId)))
      .subscribe({
        next: (replies) => {
          this.loadedReplies.set(parentId, replies);
          this.comments.update((comments) => this.attachReplies(comments, parentId, replies));
          this.autoLoadSmallReplyTrees(replies);
        },
        error: () => this.error.set('Could not load replies.'),
      });
  }

  private autoLoadSmallReplyTrees(comments: CommentItem[]) {
    // Small threads are cheaper to load completely than to make the user expand manually.
    for (const comment of comments) {
      if (
        comment.replyCount > 0 &&
        comment.replyCount < CommentsPageComponent.autoLoadReplyLimit &&
        comment.replies.length < comment.replyCount &&
        !this.loadedReplies.has(comment.id) &&
        !this.loadingReplies.has(comment.id)
      ) {
        this.loadReplies(comment.id);
      }

      this.autoLoadSmallReplyTrees(comment.replies);
    }
  }

  private attachReplies(
    comments: CommentItem[],
    parentId: string,
    replies: CommentItem[],
  ): CommentItem[] {
    return comments.map((comment) => {
      if (comment.id === parentId) {
        return { ...comment, replies };
      }

      return {
        ...comment,
        replies: this.attachReplies(comment.replies ?? [], parentId, replies),
      };
    });
  }

  private restoreLoadedReplies(comments: CommentItem[]): CommentItem[] {
    return comments.map((comment) => {
      const savedReplies = this.loadedReplies.get(comment.id);
      const serverReplies = comment.replies ?? [];
      const replies = savedReplies ? this.mergeReplies(serverReplies, savedReplies) : serverReplies;
      return {
        ...comment,
        replies: this.restoreLoadedReplies(replies),
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
    const parentId = this.replyParentId;
    const wasReply = Boolean(parentId);
    this.error.set('');
    this.replyParentId = '';
    this.showComposer.set(false);
    if (wasReply) {
      this.loadReplies(parentId);
    } else {
      this.cursor = null;
      this.nextCursor = null;
      this.cursorHistory = [];
      this.updateUrl();
    }
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
