import { gql } from 'apollo-angular';
import { CommentItem, CommentPage } from '@app/core/models/comment.models';

export interface CommentsQuery {
  comments: CommentPage;
}

export interface RepliesQuery {
  replies: CommentItem[];
}

export interface AncestorsQuery {
  ancestors: CommentItem[];
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
  isSearchMatch
  ancestorIds
`;

function commentFieldsWithReplies(depth: number): string {
  if (depth <= 0) return COMMENT_FIELDS;
  return `${COMMENT_FIELDS} replies { ${commentFieldsWithReplies(depth - 1)} }`;
}

export const COMMENTS_QUERY = gql`
  query Comments($sort: String!, $descending: Boolean!, $cursor: String) {
    comments(sort: $sort, descending: $descending, cursor: $cursor) {
      items { ${COMMENT_FIELDS} replies { ${COMMENT_FIELDS} } }
      nextCursor
      sort
      descending
    }
  }
`;

export const REPLIES_QUERY = gql(`
  query Replies($parentId: UUID!) {
    replies(parentId: $parentId) { ${commentFieldsWithReplies(64)} }
  }
`);

export const ANCESTORS_QUERY = gql(`
  query Ancestors($ids: [UUID!]!) {
    ancestors(ids: $ids) { ${COMMENT_FIELDS} }
  }
`);

export const SEARCH_QUERY = gql(`
  query Search($query: String!, $partial: Boolean!, $searchText: Boolean!, $searchUserName: Boolean!, $searchComments: Boolean!, $searchReplies: Boolean!, $cursor: String) {
    search(query: $query, partial: $partial, searchText: $searchText, searchUserName: $searchUserName, searchComments: $searchComments, searchReplies: $searchReplies, cursor: $cursor) {
      items { ${COMMENT_FIELDS} }
      nextCursor
      sort
      descending
    }
  }
`);
