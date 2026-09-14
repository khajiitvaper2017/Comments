import { gql } from 'apollo-angular';
import { CommentItem, CommentPage } from '@app/core/models/comment.models';

export interface CommentsQuery {
  comments: CommentPage;
}

export interface RepliesQuery {
  replies: CommentItem[];
}

export interface SearchQuery {
  search: CommentPage;
}

const COMMENT_FIELDS = `
  id
  parentId
  userName
  email
  homePage
  text
  createdAtUtc
  attachments { id fileName contentType size }
  replyCount
  hasMoreReplies
  isSearchMatch
`;

function commentFieldsWithReplies(depth: number): string {
  if (depth <= 0) return COMMENT_FIELDS;
  return `${COMMENT_FIELDS} replies { ${commentFieldsWithReplies(depth - 1)} }`;
}

export const COMMENTS_QUERY = gql`
  query Comments($page: Int!, $sort: String!, $descending: Boolean!) {
    comments(page: $page, sort: $sort, descending: $descending) {
      items { ${COMMENT_FIELDS} replies { ${COMMENT_FIELDS} } }
      page
      pageSize
      totalCount
      sort
      descending
      totalReplyCount
    }
  }
`;

export const REPLIES_QUERY = gql(`
  query Replies($parentId: UUID!) {
    replies(parentId: $parentId) { ${commentFieldsWithReplies(24)} }
  }
`);

export const SEARCH_QUERY = gql(`
  query Search($query: String!, $page: Int!) {
    search(query: $query, page: $page) {
      items { ${commentFieldsWithReplies(24)} }
      page
      pageSize
      totalCount
      sort
      descending
      totalReplyCount
    }
  }
`);
