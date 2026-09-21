import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { CommentItem } from '@app/core/models/comment.models';
import { prependReply, prependRootComment } from '@app/core/utils/comment-tree';

export class CommentsPageRealtime {
  private readonly hub: HubConnection = new HubConnectionBuilder()
    .withUrl('/hubs/discussions')
    .withAutomaticReconnect()
    .build();
  private readonly pending = new Map<string, CommentItem>();
  private flushTimer?: number;
  private scrollEndTimer?: number;
  private scrolling = false;

  private readonly handleScroll = () => {
    this.scrolling = true;
    if (this.scrollEndTimer !== undefined) window.clearTimeout(this.scrollEndTimer);
    this.scrollEndTimer = window.setTimeout(() => {
      this.scrollEndTimer = undefined;
      this.scrolling = false;
      if (this.pending.size > 0 && this.flushTimer === undefined) this.flush();
    }, 150);
  };

  constructor(
    private readonly shouldApply: () => boolean,
    private readonly apply: (comments: CommentItem[]) => void,
  ) {}

  start() {
    this.hub.on('commentChanged', (comment: CommentItem) => this.queue(comment));
    void this.hub.start().catch(() => undefined);
    this.handleScroll();
    window.addEventListener('scroll', this.handleScroll, { passive: true, capture: true });
  }

  destroy() {
    if (this.flushTimer !== undefined) window.clearTimeout(this.flushTimer);
    if (this.scrollEndTimer !== undefined) window.clearTimeout(this.scrollEndTimer);
    window.removeEventListener('scroll', this.handleScroll, true);
    void this.hub.stop();
  }

  private queue(comment: CommentItem) {
    if (!this.shouldApply()) return;
    this.pending.set(comment.id, comment);
    if (this.flushTimer !== undefined) return;
    this.flushTimer = window.setTimeout(() => {
      this.flushTimer = undefined;
      if (!this.scrolling) this.flush();
    }, 1000);
  }

  private flush() {
    const pending = [...this.pending.values()];
    this.pending.clear();
    if (this.shouldApply()) this.apply(pending);
  }
}

export function prependRealtimeComments(comments: CommentItem[], pending: CommentItem[]) {
  return pending.reduce(
    (updated, comment) =>
      comment.parentId ? prependReply(updated, comment) : prependRootComment(updated, comment),
    comments,
  );
}
