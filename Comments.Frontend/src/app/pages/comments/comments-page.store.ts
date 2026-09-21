import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { Captcha, CommentItem } from '@app/core/models/comment.models';
import { CommentApiService } from '@app/core/services/comment-api.service';
import { imageDownloadName } from '@app/core/utils/attachment-naming';
import {
  CommentSort,
  CommentViewMode,
  CommentsQuery,
  SearchCriteria,
  defaultCommentsQuery,
  defaultSearchCriteria,
  parseCommentsQuery,
  toCommentsQueryParams,
} from '@app/core/utils/comments-query-params';
import {
  attachAncestorPath,
  findComment,
  prependReply,
  prependRootComment,
  replaceReplies,
} from '@app/core/utils/comment-tree';
import { CommentsPageRealtime, prependRealtimeComments } from './comments-page-realtime';

type ComposerState = { kind: 'closed' } | { kind: 'root' } | { kind: 'reply'; parentId: string };
type Preview =
  | { kind: 'image'; url: string; name: string; downloadName: string }
  | { kind: 'text'; name: string; content: string }
  | null;

@Injectable()
export class CommentsPageStore {
  private static readonly autoLoadReplyLimit = 5;
  private readonly api = inject(CommentApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly realtime = new CommentsPageRealtime(
    () => this.isDefaultFirstPage(),
    (pending) => this.comments.update((comments) => prependRealtimeComments(comments, pending)),
  );
  private readonly cursorHistory = signal<Array<string | null>>([]);
  private readonly loadedReplies = new Map<string, CommentItem[]>();
  private readonly loadingReplies = new Set<string>();
  private readonly loadingAncestors = new Set<string>();
  private pendingCursorNavigation: string | null | undefined;
  private requestVersion = 0;

  constructor() {
    this.initialize();
    this.destroyRef.onDestroy(() => this.destroy());
  }

  readonly comments = signal<CommentItem[]>([]);
  readonly captcha = signal<Captcha | null>(null);
  readonly error = signal('');
  readonly query = signal<CommentsQuery>(defaultCommentsQuery());
  readonly searchDraft = signal<SearchCriteria>(defaultSearchCriteria());
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(false);
  readonly composer = signal<ComposerState>({ kind: 'closed' });
  readonly replyParentId = computed(() => {
    const composer = this.composer();
    return composer.kind === 'reply' ? composer.parentId : '';
  });
  readonly preview = signal<Preview>(null);
  readonly searchActive = computed(() => this.query().search !== null);
  readonly searchLoading = computed(() => this.searchActive() && this.loading());
  readonly commentsLoading = computed(() => !this.searchActive() && this.loading());
  readonly hasPreviousPage = computed(() => this.cursorHistory().length > 0);
  readonly selectedImage = computed(() => {
    const preview = this.preview();
    return preview?.kind === 'image' ? preview : null;
  });
  readonly selectedText = computed(() => {
    const preview = this.preview();
    return preview?.kind === 'text' ? preview : null;
  });

  private initialize() {
    this.realtime.start();
    this.route.queryParamMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((params) => this.applyRouteQuery(parseCommentsQuery(params)));
  }

  private destroy() {
    this.realtime.destroy();
  }

  setSearchQuery(query: string) {
    this.searchDraft.update((draft) => ({ ...draft, query }));
  }

  setPartialSearch(partial: boolean) {
    this.searchDraft.update((draft) => ({ ...draft, partial }));
  }

  setSearchField(field: keyof SearchCriteria['fields'], enabled: boolean) {
    this.searchDraft.update((draft) => ({
      ...draft,
      fields: { ...draft.fields, [field]: enabled },
    }));
  }

  setSearchTarget(target: keyof SearchCriteria['targets'], enabled: boolean) {
    this.searchDraft.update((draft) => ({
      ...draft,
      targets: { ...draft.targets, [target]: enabled },
    }));
  }

  submitSearch() {
    const search = this.normalizedSearch(this.searchDraft());
    if (
      !search.query ||
      (!search.fields.text && !search.fields.userName) ||
      (!search.targets.comments && !search.targets.replies)
    )
      return;
    this.navigate(this.resetPagination({ ...this.query(), search, viewMode: 'cards' }));
  }

  changeSort(field: CommentSort) {
    const current = this.query();
    const descending =
      current.sort.field === field ? !current.sort.descending : field === 'createdAt';
    this.navigate(this.resetPagination({ ...current, sort: { field, descending } }));
  }

  changeViewMode(viewMode: CommentViewMode) {
    this.navigate({ ...this.query(), viewMode });
  }

  changePage(direction: 'next' | 'previous') {
    const current = this.query();
    if (direction === 'next') {
      const next = this.nextCursor();
      if (!next) return;
      this.cursorHistory.update((history) => [...history, current.cursor]);
      this.pendingCursorNavigation = next;
      this.navigate({ ...current, cursor: next });
      return;
    }
    const history = this.cursorHistory();
    const previous = history.at(-1);
    if (previous === undefined) return;
    this.cursorHistory.set(history.slice(0, -1));
    this.pendingCursorNavigation = previous;
    this.navigate({ ...current, cursor: previous });
  }

  openRootComposer() {
    this.composer.set({ kind: 'root' });
  }

  openReplyComposer(parentId: string) {
    this.composer.set({ kind: 'reply', parentId });
  }

  closeComposer(clearError = true) {
    this.composer.set({ kind: 'closed' });
    if (clearError) this.error.set('');
  }

  loadReplies(parentId: string) {
    if (this.loadingReplies.has(parentId)) return;
    this.loadingReplies.add(parentId);
    this.api
      .getReplies(parentId)
      .pipe(finalize(() => this.loadingReplies.delete(parentId)))
      .subscribe({
        next: (replies) => {
          if (!findComment(this.comments(), parentId)) return;
          this.loadedReplies.set(parentId, replies);
          this.comments.update((comments) => replaceReplies(comments, parentId, replies));
          this.autoLoadSmallReplyTrees(replies);
        },
        error: () => this.error.set('Could not load replies.'),
      });
  }

  loadAncestors(commentId: string) {
    if (this.loadingAncestors.has(commentId)) return;
    const comment = findComment(this.comments(), commentId);
    const ids = comment?.ancestorIds ?? [];
    if (!comment || ids.length === 0) return;

    this.loadingAncestors.add(commentId);
    this.api
      .getAncestors(ids)
      .pipe(finalize(() => this.loadingAncestors.delete(commentId)))
      .subscribe({
        next: (ancestors) => {
          const path = attachAncestorPath(comment, ancestors);
          this.comments.update((comments) =>
            comments.map((item) => (item.id === commentId ? path : item)),
          );
        },
        error: () => this.error.set('Could not load comment ancestors.'),
      });
  }

  applyCreatedComment(created: CommentItem) {
    const composer = this.composer();
    this.closeComposer();
    if (composer.kind === 'reply') {
      if (findComment(this.comments(), composer.parentId)) {
        this.comments.update((comments) => prependReply(comments, created));
      } else {
        this.loadReplies(composer.parentId);
      }
    } else if (composer.kind === 'root' && this.isDefaultFirstPage()) {
      this.comments.update((comments) => prependRootComment(comments, created));
    }
  }

  openImage(image: { id: string; name: string; contentType: string }) {
    this.preview.set({
      kind: 'image',
      url: `/api/attachments/${image.id}`,
      name: image.name,
      downloadName: imageDownloadName(image.name, image.contentType),
    });
  }

  openText(text: { id: string; name: string }) {
    this.api.getAttachmentText(text.id).subscribe({
      next: (content) => this.preview.set({ kind: 'text', name: text.name, content }),
      error: () => this.error.set('Could not preview text attachment.'),
    });
  }

  closePreview() {
    this.preview.set(null);
  }

  private applyRouteQuery(query: CommentsQuery) {
    const preservesHistory = this.pendingCursorNavigation === query.cursor;
    this.pendingCursorNavigation = undefined;
    if (!preservesHistory) this.cursorHistory.set([]);
    this.query.set(query);
    this.searchDraft.set(query.search ?? defaultSearchCriteria());
    this.load(query);
  }

  private navigate(query: CommentsQuery) {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toCommentsQueryParams(query),
    });
  }

  private resetPagination(query: CommentsQuery): CommentsQuery {
    this.cursorHistory.set([]);
    this.nextCursor.set(null);
    return { ...query, cursor: null };
  }

  private load(query: CommentsQuery) {
    const version = ++this.requestVersion;
    this.loading.set(true);
    this.error.set('');
    const request = query.search
      ? this.api.searchComments(
          query.search.query,
          query.search.partial,
          query.search.fields.text,
          query.search.fields.userName,
          query.search.targets.comments,
          query.search.targets.replies,
          query.cursor,
        )
      : this.api.getComments(query.sort.field, query.sort.descending, query.cursor);

    request
      .pipe(finalize(() => version === this.requestVersion && this.loading.set(false)))
      .subscribe({
        next: (result) => {
          if (version !== this.requestVersion) return;
          const items = this.restoreLoadedReplies(result.items);
          this.comments.set(items);
          this.nextCursor.set(result.nextCursor ?? null);
          this.autoLoadSmallReplyTrees(items);
        },
        error: () => {
          if (version === this.requestVersion) {
            this.error.set(
              query.search ? 'Search is currently unavailable.' : 'Could not load comments.',
            );
          }
        },
      });
  }

  private autoLoadSmallReplyTrees(comments: CommentItem[]) {
    for (const comment of comments) {
      if (
        comment.replyCount > 0 &&
        comment.replyCount < CommentsPageStore.autoLoadReplyLimit &&
        comment.replies.length < comment.replyCount &&
        !this.loadedReplies.has(comment.id) &&
        !this.loadingReplies.has(comment.id)
      ) {
        this.loadReplies(comment.id);
      }
      this.autoLoadSmallReplyTrees(comment.replies);
    }
  }

  private restoreLoadedReplies(comments: CommentItem[]): CommentItem[] {
    return comments.map((comment) => {
      const serverReplies = comment.replies ?? [];
      const loaded = this.loadedReplies.get(comment.id);
      const replies = loaded ? this.mergeReplies(serverReplies, loaded) : serverReplies;
      return { ...comment, replies: this.restoreLoadedReplies(replies) };
    });
  }

  private mergeReplies(serverReplies: CommentItem[], loadedReplies: CommentItem[]): CommentItem[] {
    const loadedById = new Map(loadedReplies.map((reply) => [reply.id, reply]));
    const merged = serverReplies.map((reply) => loadedById.get(reply.id) ?? reply);
    const serverIds = new Set(serverReplies.map((reply) => reply.id));
    return [...merged, ...loadedReplies.filter((reply) => !serverIds.has(reply.id))];
  }

  private isDefaultFirstPage() {
    const query = this.query();
    return (
      !query.search && !query.cursor && query.sort.field === 'createdAt' && query.sort.descending
    );
  }

  private normalizedSearch(search: SearchCriteria): SearchCriteria {
    return { ...search, query: search.query.trim() };
  }
}
