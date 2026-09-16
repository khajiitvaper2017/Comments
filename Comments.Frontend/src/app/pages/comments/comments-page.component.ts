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
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideMessageCirclePlus, lucideSearch } from '@ng-icons/lucide';
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
    NgIcon,
  ],
  providers: [provideIcons({ lucideMessageCirclePlus, lucideSearch })],
  templateUrl: './comments-page.component.html',
})
export class CommentsPageComponent implements OnInit, OnDestroy {
  private static readonly autoLoadReplyLimit = 5;
  private readonly api = inject(CommentApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  comments = signal<CommentItem[]>([]);
  captcha = signal<Captcha | null>(null);
  selectedImage = signal<{ url: string; name: string; downloadName: string } | null>(null);
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
  private readonly loadedReplies = new Map<string, CommentItem[]>();
  private readonly loadingReplies = new Set<string>();
  private readonly loadingAncestors = new Set<string>();
  private readonly pendingRealtimeComments = new Map<string, CommentItem>();
  private realtimeFlushTimer?: number;
  private scrollEndTimer?: number;
  private isScrolling = false;
  private pendingCursorNavigation: string | null | undefined;
  private querySubscription?: Subscription;
  private readonly handleWindowScroll = () => {
    this.isScrolling = true;
    if (this.scrollEndTimer !== undefined) window.clearTimeout(this.scrollEndTimer);
    this.scrollEndTimer = window.setTimeout(() => {
      this.scrollEndTimer = undefined;
      this.isScrolling = false;
      if (this.pendingRealtimeComments.size > 0 && this.realtimeFlushTimer === undefined) {
        this.flushRealtimeComments();
      }
    }, 150);
  };
  ngOnInit() {
    this.hub.on('commentChanged', (comment: CommentItem) => this.handleRealtimeComment(comment));
    void this.hub.start().catch(() => undefined);
    this.handleWindowScroll();
    window.addEventListener('scroll', this.handleWindowScroll, { passive: true, capture: true });
    this.querySubscription = this.route.queryParamMap.subscribe((params) =>
      this.loadFromQuery(params),
    );
  }

  ngOnDestroy() {
    this.querySubscription?.unsubscribe();
    if (this.realtimeFlushTimer !== undefined) window.clearTimeout(this.realtimeFlushTimer);
    if (this.scrollEndTimer !== undefined) window.clearTimeout(this.scrollEndTimer);
    window.removeEventListener('scroll', this.handleWindowScroll, true);
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
    this.viewMode = this.activeSearchQuery
      ? 'cards'
      : params.get('view') === 'table'
        ? 'table'
        : 'cards';

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

  loadComments() {
    this.loading.set(true);
    this.api
      .getComments(this.sort, this.descending, this.cursor)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          const comments = this.restoreLoadedReplies(result.items);
          this.comments.set(comments);
          this.nextCursor = result.nextCursor ?? null;
          this.hasPreviousPage = this.cursorHistory.length > 0;
          this.autoLoadSmallReplyTrees(comments);
        },
        error: () => this.error.set('Could not load comments.'),
      });
  }

  private handleRealtimeComment(comment: CommentItem) {
    if (!this.isDefaultFirstPage()) return;

    this.pendingRealtimeComments.set(comment.id, comment);
    if (this.realtimeFlushTimer !== undefined) return;

    this.realtimeFlushTimer = window.setTimeout(() => {
      this.realtimeFlushTimer = undefined;
      if (this.isScrolling) return;
      this.flushRealtimeComments();
    }, 1000);
  }

  private flushRealtimeComments() {
    const pending = [...this.pendingRealtimeComments.values()];
    this.pendingRealtimeComments.clear();
    if (pending.length === 0 || !this.isDefaultFirstPage()) return;

    this.comments.update((comments) => {
      let updated = comments;
      for (const comment of pending) {
        updated = comment.parentId
          ? this.attachRealtimeReply(updated, comment)
          : [comment, ...updated.filter((item) => item.id !== comment.id)];
      }
      return updated;
    });
  }

  private attachRealtimeReply(comments: CommentItem[], reply: CommentItem): CommentItem[] {
    return comments.map((comment) => {
      if (comment.id === reply.parentId) {
        if (comment.replies.some((item) => item.id === reply.id)) return comment;

        return {
          ...comment,
          replyCount: comment.replyCount + 1,
          replies: [reply, ...comment.replies.filter((item) => item.id !== reply.id)],
        };
      }

      return {
        ...comment,
        replies: this.attachRealtimeReply(comment.replies ?? [], reply),
      };
    });
  }

  private isDefaultFirstPage(): boolean {
    return (
      !this.searchActive() && this.cursor === null && this.sort === 'createdAt' && this.descending
    );
  }

  search() {
    this.activeSearchQuery = this.searchQuery.trim();
    this.viewMode = 'cards';
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
      let current: CommentItem = { ...hit, ancestorIds: [] };
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
    this.scrollToComposer('smooth', 'start');
  }
  cancelComposer() {
    this.replyParentId = '';
    this.showComposer.set(false);
    this.error.set('');
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
  handleSubmitted(created: CommentItem) {
    const parentId = this.replyParentId;
    const wasReply = Boolean(parentId);
    this.error.set('');
    this.replyParentId = '';
    this.showComposer.set(false);
    if (wasReply) {
      if (this.findComment(this.comments(), parentId)) {
        this.comments.update((comments) => this.attachRealtimeReply(comments, created));
        const loadedReplies = this.loadedReplies.get(parentId);
        if (loadedReplies) {
          this.loadedReplies.set(parentId, [
            created,
            ...loadedReplies.filter((reply) => reply.id !== created.id),
          ]);
        }
      } else {
        this.loadReplies(parentId);
      }
    } else if (this.isDefaultFirstPage()) {
      this.comments.update((comments) => [
        created,
        ...comments.filter((comment) => comment.id !== created.id),
      ]);
    }
  }
  openImage(image: { id: string; name: string; contentType: string }) {
    this.selectedImage.set({
      url: `/api/attachments/${image.id}`,
      name: image.name,
      downloadName: this.getImageDownloadName(image.name, image.contentType),
    });
  }

  private getImageDownloadName(name: string, contentType: string): string {
    const extension =
      contentType.toLowerCase() === 'image/webp'
        ? '.webp'
        : contentType.toLowerCase() === 'image/png'
          ? '.png'
          : contentType.toLowerCase() === 'image/jpeg'
            ? '.jpg'
            : '.gif';
    return `${name.replace(/\.[^.]+$/, '')}${extension}`;
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
