import { describe, expect, it } from 'vitest';
import { CommentCardsComponent } from './comment-cards.component';
import { CommentItem } from '@app/core/models/comment.models';

describe('CommentCardsComponent reply switch', () => {
  it('toggles loaded replies and requests unloaded replies', () => {
    const component = new CommentCardsComponent() as any;
    const comment = {
      id: 'root',
      replyCount: 2,
      replies: [{ id: 'reply' }],
      hasMoreReplies: false,
    } as unknown as CommentItem;

    expect(component.repliesAreVisible(comment)).toBe(true);
    component.toggleOrLoadReplies(comment);
    expect(component.repliesAreVisible(comment)).toBe(false);
    component.toggleOrLoadReplies(comment);
    expect(component.repliesAreVisible(comment)).toBe(true);

    const unloaded = {
      id: 'other',
      replyCount: 2,
      replies: [],
      hasMoreReplies: true,
    } as unknown as CommentItem;
    let requested = '';
    component.loadRepliesRequested.subscribe((id: string) => (requested = id));
    component.toggleOrLoadReplies(unloaded);
    expect(requested).toBe('other');
  });
});
