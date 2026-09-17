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
    } as unknown as CommentItem;

    expect(component.repliesAreVisible(comment)).toBe(true);
    component.toggleRepliesOrLoadMore(comment);
    expect(component.repliesAreVisible(comment)).toBe(false);
    component.toggleRepliesOrLoadMore(comment);
    expect(component.repliesAreVisible(comment)).toBe(true);

    const unloaded = {
      id: 'other',
      replyCount: 2,
      replies: [],
    } as unknown as CommentItem;
    let requested = '';
    component.loadRepliesRequested.subscribe((id: string) => (requested = id));
    component.toggleRepliesOrLoadMore(unloaded);
    expect(requested).toBe('other');
  });

  it('counts loaded descendants when deciding whether more replies are available', () => {
    const component = new CommentCardsComponent() as any;
    const comment = {
      id: 'root',
      replyCount: 2,
      replies: [
        { id: 'reply-1', replies: [{ id: 'nested-reply', replies: [] }] },
        { id: 'reply-2', replies: [] },
      ],
    } as unknown as CommentItem;

    component.searchMode = true;
    expect(component.hasUnloadedReplies(comment)).toBe(false);
  });

  it('keeps load-more available for a parent with its own unloaded descendants', () => {
    const component = new CommentCardsComponent() as any;
    component.searchMode = true;
    const deepest = {
      id: 'deepest',
      replyCount: 2,
      replies: [{ id: 'loaded', replies: [] }],
    } as unknown as CommentItem;
    const parent = {
      id: 'parent',
      replyCount: 3,
      replies: [deepest],
    } as unknown as CommentItem;

    expect(component.hasUnloadedReplies(parent)).toBe(true);
    expect(component.hasUnloadedReplies(deepest)).toBe(true);
  });

  it('shows load-more when a comment has some but not all of its own replies loaded', () => {
    const component = new CommentCardsComponent() as any;
    component.searchMode = true;
    const comment = {
      id: 'partially-loaded',
      replyCount: 3,
      replies: [{ id: 'loaded-reply', replies: [] }],
    } as unknown as CommentItem;

    expect(component.hasUnloadedReplies(comment)).toBe(true);
    expect(component.hasUnloadedReplies(comment)).toBe(true);
  });

  it('shows more only for the root and last node in the displayed chain', () => {
    const component = new CommentCardsComponent() as any;
    component.searchMode = true;
    const counts = [386, 10, 9, 8, 7, 6, 5];
    let chain: CommentItem = {
      id: 'last',
      replyCount: counts[counts.length - 1],
      replies: [],
    } as unknown as CommentItem;

    for (let index = counts.length - 2; index >= 0; index--) {
      chain = {
        id: `node-${index}`,
        replyCount: counts[index],
        replies: [chain],
      } as unknown as CommentItem;
    }

    const nodes: CommentItem[] = [];
    let current: CommentItem | undefined = chain;
    while (current) {
      nodes.push(current);
      current = current.replies[0];
    }

    expect(nodes.map((node, depth) => component.shouldShowLoadMoreButton(node, depth))).toEqual([
      true,
      false,
      false,
      false,
      false,
      false,
      true,
    ]);
  });

  it('only supplies a highlight term for selected fields and targets', () => {
    const component = new CommentCardsComponent() as any;
    component.highlightTerm = 'match';
    component.highlightText = true;
    component.highlightUserName = false;
    component.highlightComments = true;
    component.highlightReplies = false;

    const root = { parentId: null } as unknown as CommentItem;
    const reply = { parentId: 'root-id' } as unknown as CommentItem;

    expect(component.highlightTermFor(root, 'text')).toBe('match');
    expect(component.highlightTermFor(root, 'userName')).toBe('');
    expect(component.highlightTermFor(reply, 'text')).toBe('');
  });
});
