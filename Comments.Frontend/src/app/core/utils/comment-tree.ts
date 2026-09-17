import { CommentItem } from '@app/core/models/comment.models';

export function findComment(comments: CommentItem[], id: string): CommentItem | undefined {
  for (const comment of comments) {
    if (comment.id === id) return comment;
    const nested = findComment(comment.replies, id);
    if (nested) return nested;
  }
  return undefined;
}

export function replaceReplies(
  comments: CommentItem[],
  parentId: string,
  replies: CommentItem[],
): CommentItem[] {
  return comments.map((comment) =>
    comment.id === parentId
      ? { ...comment, replies }
      : { ...comment, replies: replaceReplies(comment.replies, parentId, replies) },
  );
}

export function prependReply(comments: CommentItem[], reply: CommentItem): CommentItem[] {
  return comments.map((comment) => {
    if (comment.id === reply.parentId) {
      const alreadyPresent = comment.replies.some((item) => item.id === reply.id);
      return alreadyPresent
        ? comment
        : {
            ...comment,
            replyCount: comment.replyCount + 1,
            replies: [reply, ...comment.replies],
          };
    }
    return { ...comment, replies: prependReply(comment.replies, reply) };
  });
}

export function prependRootComment(comments: CommentItem[], comment: CommentItem): CommentItem[] {
  return [comment, ...comments.filter((item) => item.id !== comment.id)];
}

export function attachAncestorPath(hit: CommentItem, ancestors: CommentItem[]): CommentItem {
  const ancestorsById = new Map(ancestors.map((ancestor) => [ancestor.id, ancestor]));
  let current: CommentItem = { ...hit, ancestorIds: [] };
  for (const ancestorId of [...(hit.ancestorIds ?? [])].reverse()) {
    const ancestor = ancestorsById.get(ancestorId);
    if (ancestor) current = { ...ancestor, replies: [current] };
  }
  return current;
}
